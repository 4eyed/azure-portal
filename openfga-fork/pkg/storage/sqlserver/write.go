package sqlserver

import (
	"context"
	"database/sql"
	"errors"
	"sort"
	"strings"
	"time"

	sq "github.com/Masterminds/squirrel"
	"github.com/oklog/ulid/v2"
	"google.golang.org/protobuf/proto"

	openfgav1 "github.com/openfga/api/proto/openfga/v1"

	"github.com/openfga/openfga/pkg/storage"
	"github.com/openfga/openfga/pkg/storage/sqlcommon"
	tupleUtils "github.com/openfga/openfga/pkg/tuple"
)

// tupleLockKey represents the composite key we lock on.
type tupleLockKey struct {
	objectType string
	objectID   string
	relation   string
	user       string
	userType   string
}

// makeTupleLockKeys flattens deletes+writes into a deduped, sorted slice to ensure stable lock order.
func makeTupleLockKeys(deletes storage.Deletes, writes storage.Writes) []tupleLockKey {
	keys := make([]tupleLockKey, 0, len(deletes)+len(writes))

	seen := make(map[string]struct{}, cap(keys))
	add := func(tk *openfgav1.TupleKey) {
		objectType, objectID := tupleUtils.SplitObject(tk.GetObject())
		userType := tupleUtils.GetUserTypeFromUser(tk.GetUser())
		k := tupleLockKey{
			objectType: objectType,
			objectID:   objectID,
			relation:   tk.GetRelation(),
			user:       tk.GetUser(),
			userType:   string(userType),
		}
		s := strings.Join([]string{k.objectType, k.objectID, k.relation, k.user, k.userType}, "\x00")
		if _, ok := seen[s]; ok {
			return
		}
		seen[s] = struct{}{}
		keys = append(keys, k)
	}

	for _, tk := range deletes {
		add(tupleUtils.TupleKeyWithoutConditionToTupleKey(tk))
	}
	for _, tk := range writes {
		add(tk)
	}

	// Sort deterministically by the composite key to keep lock order stable.
	sort.Slice(keys, func(i, j int) bool {
		a, b := keys[i], keys[j]
		if a.objectType != b.objectType {
			return a.objectType < b.objectType
		}
		if a.objectID != b.objectID {
			return a.objectID < b.objectID
		}
		if a.relation != b.relation {
			return a.relation < b.relation
		}
		if a.user != b.user {
			return a.user < b.user
		}
		return a.userType < b.userType
	})

	return keys
}

// selectExistingRowsForWrite selects existing rows for the given keys and locks them WITH (UPDLOCK, ROWLOCK).
// SQL Server version that avoids row-constructor IN syntax (not supported) and uses proper @pN placeholders.
func (s *Datastore) selectExistingRowsForWrite(ctx context.Context, store string, keys []tupleLockKey, txn *sql.Tx, existing map[string]*openfgav1.Tuple) error {
	if len(keys) == 0 {
		return nil
	}

	// SQL Server doesn't support row constructor IN: (a,b,c) IN ((1,2,3),(4,5,6))
	// Build OR conditions instead: (a=1 AND b=2 AND c=3) OR (a=4 AND b=5 AND c=6)
	orConditions := make(sq.Or, 0, len(keys))
	for _, k := range keys {
		orConditions = append(orConditions, sq.Eq{
			"object_type": k.objectType,
			"object_id":   k.objectID,
			"relation":    k.relation,
			"_user":       k.user,
			"user_type":   k.userType,
		})
	}

	// WITH (UPDLOCK, ROWLOCK) must be immediately after table name in SQL Server
	selectBuilder := s.stbl.
		Select(sqlcommon.SQLIteratorColumns()...).
		From("tuple WITH (UPDLOCK, ROWLOCK)").
		Where(sq.Eq{"store": store}).
		Where(orConditions).
		RunWith(txn)

	iter := sqlcommon.NewSQLTupleIterator(selectBuilder, s.dbInfo.HandleSQLError)
	defer iter.Stop()

	items, _, err := iter.ToArray(ctx, storage.PaginationOptions{PageSize: len(keys)})
	if err != nil {
		return err
	}

	for _, tuple := range items {
		existing[tupleUtils.TupleKeyToString(tuple.GetKey())] = tuple
	}

	return nil
}

// write provides the SQL Server-specific implementation that handles row constructor incompatibility.
func (s *Datastore) write(
	ctx context.Context,
	store string,
	deletes storage.Deletes,
	writes storage.Writes,
	opts storage.TupleWriteOptions,
	now time.Time,
) error {
	// 1. Begin Transaction ( Isolation Level = READ COMMITTED )
	txn, err := s.db.BeginTx(ctx, &sql.TxOptions{Isolation: sql.LevelReadCommitted})
	if err != nil {
		return HandleSQLError(err)
	}
	defer func() { _ = txn.Rollback() }()

	// 2. Compile a SELECT … WITH (UPDLOCK, ROWLOCK) statement to read the tuples for writes and lock tuples for deletes
	// Build a deduped, sorted list of keys to lock.
	lockKeys := makeTupleLockKeys(deletes, writes)
	total := len(lockKeys)
	if total == 0 {
		// Nothing to do.
		return nil
	}

	existing := make(map[string]*openfgav1.Tuple, total)

	// 3. If list compiled in step 2 is not empty, execute SELECT … WITH (UPDLOCK, ROWLOCK) statement
	for start := 0; start < total; start += storage.DefaultMaxTuplesPerWrite {
		end := start + storage.DefaultMaxTuplesPerWrite
		if end > total {
			end = total
		}
		keys := lockKeys[start:end]

		if err = s.selectExistingRowsForWrite(ctx, store, keys, txn, existing); err != nil {
			return err
		}
	}

	changeLogItems := make([][]interface{}, 0, len(deletes)+len(writes))

	// ensures increasingly unique values within a single thread
	entropy := ulid.DefaultEntropy()

	deleteConditions := sq.Or{}

	// 4. For deletes
	for _, tk := range deletes {
		if _, ok := existing[tupleUtils.TupleKeyToString(tk)]; !ok {
			// If the tuple does not exist, we can not delete it.
			switch opts.OnMissingDelete {
			case storage.OnMissingDeleteIgnore:
				continue
			case storage.OnMissingDeleteError:
				fallthrough
			default:
				return storage.InvalidWriteInputError(
					tk,
					openfgav1.TupleOperation_TUPLE_OPERATION_DELETE,
				)
			}
		}

		id := ulid.MustNew(ulid.Timestamp(now), entropy).String()
		objectType, objectID := tupleUtils.SplitObject(tk.GetObject())

		deleteConditions = append(deleteConditions, sq.Eq{
			"object_type": objectType,
			"object_id":   objectID,
			"relation":    tk.GetRelation(),
			"_user":       tk.GetUser(),
			"user_type":   tupleUtils.GetUserTypeFromUser(tk.GetUser()),
		})

		changeLogItems = append(changeLogItems, []interface{}{
			store,
			objectType,
			objectID,
			tk.GetRelation(),
			tk.GetUser(),
			"",
			nil, // Redact condition info for deletes since we only need the base triplet (object, relation, user).
			openfgav1.TupleOperation_TUPLE_OPERATION_DELETE,
			id,
			s.dbInfo.NowExpr(),
		})
	}

	writeItems := make([][]interface{}, 0, len(writes))

	// 5. For writes
	for _, tk := range writes {
		if existingTuple, ok := existing[tupleUtils.TupleKeyToString(tk)]; ok {
			// If the tuple exists, we can not write it.
			switch opts.OnDuplicateInsert {
			case storage.OnDuplicateInsertIgnore:
				// If the tuple exists and the condition is the same, we can ignore it.
				if proto.Equal(existingTuple.GetKey().GetCondition(), tk.GetCondition()) {
					continue
				}
				// If tuple conditions are different, we throw an error.
				return storage.TupleConditionConflictError(tk)
			case storage.OnDuplicateInsertError:
				fallthrough
			default:
				return storage.InvalidWriteInputError(
					tk,
					openfgav1.TupleOperation_TUPLE_OPERATION_WRITE,
				)
			}
		}

		id := ulid.MustNew(ulid.Timestamp(now), entropy).String()
		objectType, objectID := tupleUtils.SplitObject(tk.GetObject())

		conditionName, conditionContext, err := sqlcommon.MarshalRelationshipCondition(tk.GetCondition())
		if err != nil {
			return err
		}

		// TODO: SQL Server may need CAST for condition_context VARBINARY if non-nil
		// For now it works because seed data has nil conditions
		writeItems = append(writeItems, []interface{}{
			store,
			objectType,
			objectID,
			tk.GetRelation(),
			tk.GetUser(),
			tupleUtils.GetUserTypeFromUser(tk.GetUser()),
			conditionName,
			conditionContext,
			id,
			s.dbInfo.NowExpr(),
		})

		changeLogItems = append(changeLogItems, []interface{}{
			store,
			objectType,
			objectID,
			tk.GetRelation(),
			tk.GetUser(),
			conditionName,
			conditionContext,
			openfgav1.TupleOperation_TUPLE_OPERATION_WRITE,
			id,
			s.dbInfo.NowExpr(),
		})
	}

	// Execute deletes
	for start, totalDeletes := 0, len(deleteConditions); start < totalDeletes; start += storage.DefaultMaxTuplesPerWrite {
		end := start + storage.DefaultMaxTuplesPerWrite
		if end > totalDeletes {
			end = totalDeletes
		}

		deleteConditionsBatch := deleteConditions[start:end]

		res, err := s.stbl.Delete("tuple").Where(sq.Eq{"store": store}).
			Where(deleteConditionsBatch).
			RunWith(txn).
			ExecContext(ctx)
		if err != nil {
			return HandleSQLError(err)
		}

		rowsAffected, err := res.RowsAffected()
		if err != nil {
			return HandleSQLError(err)
		}

		if rowsAffected != int64(len(deleteConditionsBatch)) {
			// If we deleted fewer rows than planned (after read before write), means we hit a race condition.
			return storage.ErrWriteConflictOnDelete
		}
	}

	// Execute writes
	for start, totalWrites := 0, len(writeItems); start < totalWrites; start += storage.DefaultMaxTuplesPerWrite {
		end := start + storage.DefaultMaxTuplesPerWrite
		if end > totalWrites {
			end = totalWrites
		}

		writesBatch := writeItems[start:end]

		insertBuilder := s.stbl.
			Insert("tuple").
			Columns(
				"store",
				"object_type",
				"object_id",
				"relation",
				"_user",
				"user_type",
				"condition_name",
				"condition_context",
				"ulid",
				"inserted_at",
			)

		for _, item := range writesBatch {
			insertBuilder = insertBuilder.Values(item...)
		}

		_, err = insertBuilder.
			RunWith(txn).
			ExecContext(ctx)
		if err != nil {
			dberr := HandleSQLError(err)
			if errors.Is(dberr, storage.ErrCollision) {
				// ErrCollision is returned on duplicate write (constraint violation).
				return storage.ErrWriteConflictOnInsert
			}
			return dberr
		}
	}

	// 6. Execute INSERT changelog statements
	for start, totalItems := 0, len(changeLogItems); start < totalItems; start += storage.DefaultMaxTuplesPerWrite {
		end := start + storage.DefaultMaxTuplesPerWrite
		if end > totalItems {
			end = totalItems
		}

		changeLogBatch := changeLogItems[start:end]

		changelogBuilder := s.stbl.
			Insert("changelog").
			Columns(
				"store",
				"object_type",
				"object_id",
				"relation",
				"_user",
				"condition_name",
				"condition_context",
				"operation",
				"ulid",
				"inserted_at",
			)

		for _, item := range changeLogBatch {
			changelogBuilder = changelogBuilder.Values(item...)
		}

		_, err = changelogBuilder.RunWith(txn).ExecContext(ctx)
		if err != nil {
			return HandleSQLError(err)
		}
	}

	// 7. Commit Transaction
	if err := txn.Commit(); err != nil {
		return HandleSQLError(err)
	}

	return nil
}
