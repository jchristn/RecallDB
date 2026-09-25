-- 002_inspect_damaged_collections.sql
--
-- Read-only inspection to run BEFORE upgrading to the release that fixes the per-collection index-name collision
-- (see CHANGELOG "Unreleased"). It reports collections whose backing schema is incomplete because two collections
-- created in the same millisecond shared index names under the old scheme, leaving the loser without some or all of
-- its indexes and, in the concurrent case, without its labels/tags tables.
--
-- Nothing here modifies data. The first server start after the upgrade repairs what these queries surface:
-- it renames existing indexes to the new full-id scheme, builds any that are missing, and creates missing tables.
-- A collection with duplicate document_key values keeps working without the unique index until the duplicates are
-- resolved by an operator (they cannot be resolved automatically without choosing which rows to drop).
--
-- Usage: psql "<connection string>" -f migrations/002_inspect_damaged_collections.sql

\echo '== Collection ids that share an old-scheme index identifier (the historical millisecond-only component) =='
SELECT split_part(id, '_', 2) AS old_index_identifier, count(*) AS collections
FROM collections
WHERE id LIKE 'col\_%' ESCAPE '\'
GROUP BY 1
HAVING count(*) > 1
ORDER BY collections DESC, old_index_identifier;

\echo ''
\echo '== Per-collection health: index count on the documents table, and whether the side tables exist =='
\echo '   (a healthy collection has 8 indexes on its documents table and both a _labels and a _tags table)'
SELECT c.id,
       c.name,
       (SELECT count(*) FROM pg_indexes i WHERE i.tablename = 'collection_' || lower(c.id))            AS documents_indexes,
       to_regclass('collection_' || lower(c.id) || '_labels') IS NOT NULL                              AS has_labels_table,
       to_regclass('collection_' || lower(c.id) || '_tags')   IS NOT NULL                              AS has_tags_table
FROM collections c
WHERE c.id LIKE 'col\_%' ESCAPE '\'
  AND (
        (SELECT count(*) FROM pg_indexes i WHERE i.tablename = 'collection_' || lower(c.id)) < 8
     OR to_regclass('collection_' || lower(c.id) || '_labels') IS NULL
     OR to_regclass('collection_' || lower(c.id) || '_tags')   IS NULL
      )
ORDER BY documents_indexes, c.id;

\echo ''
\echo '== Documents tables that carry duplicate document_key values (the unique index cannot be built until resolved) =='
DO $$
DECLARE
    r          record;
    duplicates bigint;
BEGIN
    FOR r IN
        SELECT id FROM collections WHERE id LIKE 'col\_%' ESCAPE '\'
    LOOP
        IF to_regclass('collection_' || lower(r.id)) IS NULL THEN
            CONTINUE;
        END IF;
        EXECUTE format(
            'SELECT count(*) FROM (SELECT 1 FROM %I GROUP BY document_key HAVING count(*) > 1) d',
            'collection_' || lower(r.id))
        INTO duplicates;
        IF duplicates > 0 THEN
            RAISE NOTICE 'collection % has % duplicate document_key value(s)', r.id, duplicates;
        END IF;
    END LOOP;
END $$;
