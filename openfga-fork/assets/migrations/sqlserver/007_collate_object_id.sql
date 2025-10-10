-- +goose Up
-- Note: SQL Server uses case-sensitive collation (Latin1_General_CS_AS) by default for comparisons
-- The LOCK syntax from MySQL is not needed in SQL Server

-- Drop existing indexes and constraints before altering column
DROP INDEX idx_reverse_lookup_user ON tuple;
ALTER TABLE tuple DROP CONSTRAINT PK_tuple;

-- Update object_id column to ensure it's VARCHAR(255) with case-sensitive collation
ALTER TABLE tuple ALTER COLUMN object_id VARCHAR(255) COLLATE Latin1_General_CS_AS NOT NULL;

-- Recreate primary key
ALTER TABLE tuple ADD CONSTRAINT PK_tuple PRIMARY KEY (store, object_type, object_id, relation, _user);

-- Create new optimized index for user lookups
CREATE INDEX idx_user_lookup ON tuple (store, _user, relation, object_type, object_id);

-- +goose Down
-- Drop optimized index
DROP INDEX idx_user_lookup ON tuple;

-- Drop primary key
ALTER TABLE tuple DROP CONSTRAINT PK_tuple;

-- Revert object_id column
ALTER TABLE tuple ALTER COLUMN object_id VARCHAR(255) NOT NULL;

-- Recreate primary key
ALTER TABLE tuple ADD CONSTRAINT PK_tuple PRIMARY KEY (store, object_type, object_id, relation, _user);

-- Restore old index
CREATE INDEX idx_reverse_lookup_user ON tuple (store, object_type, relation, _user);
