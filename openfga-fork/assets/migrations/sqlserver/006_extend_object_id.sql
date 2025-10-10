-- +goose Up
-- SQL Server requires dropping constraints/indexes before altering columns
-- Drop indexes that reference object_id
DROP INDEX idx_reverse_lookup_user ON tuple;

-- Drop the primary key temporarily
ALTER TABLE tuple DROP CONSTRAINT PK_tuple;

-- Alter the column (must be NOT NULL to be part of primary key)
ALTER TABLE tuple ALTER COLUMN object_id VARCHAR(255) NOT NULL;

-- Recreate the primary key
ALTER TABLE tuple ADD CONSTRAINT PK_tuple PRIMARY KEY (store, object_type, object_id, relation, _user);

-- Recreate the index
CREATE INDEX idx_reverse_lookup_user ON tuple (store, object_type, relation, _user);

-- +goose Down
DROP INDEX idx_reverse_lookup_user ON tuple;
ALTER TABLE tuple DROP CONSTRAINT PK_tuple;
ALTER TABLE tuple ALTER COLUMN object_id VARCHAR(128) NOT NULL;
ALTER TABLE tuple ADD CONSTRAINT PK_tuple PRIMARY KEY (store, object_type, object_id, relation, _user);
CREATE INDEX idx_reverse_lookup_user ON tuple (store, object_type, relation, _user);
