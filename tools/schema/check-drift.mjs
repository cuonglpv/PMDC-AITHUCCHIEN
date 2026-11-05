// Builds one empty database from db/migrations and one from the DBML export, and fails if anything in the DBML is
// missing or different in the migrated one (docs/10-code-structure.md, section 7). Needs no server: PGlite runs
// PostgreSQL in this process.
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PGlite } from '@electric-sql/pglite';
import { pg_trgm } from '@electric-sql/pglite/contrib/pg_trgm';
import { unaccent } from '@electric-sql/pglite/contrib/unaccent';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');

async function fromMigrations() {
  const db = new PGlite({ extensions: { pg_trgm, unaccent } });
  // The runner creates its journal before the first script.
  await db.exec('create table schema_migration (schemaversionsid serial primary key)');
  const folder = join(root, 'db/migrations');
  for (const file of readdirSync(folder).sort()) {
    await db.exec(readFileSync(join(folder, file), 'utf8'));
  }
  return db;
}

async function fromDbml() {
  const work = mkdtempSync(join(tmpdir(), 'claimlens-schema-'));
  const output = join(work, 'schema.sql');
  execFileSync('npx', ['--no-install', 'dbml2sql', 'docs/diagrams/claimlens.dbml', '--postgres', '-o', output], {
    cwd: root,
    stdio: 'ignore',
    shell: process.platform === 'win32',
  });
  const db = new PGlite();
  await db.exec(readFileSync(output, 'utf8'));
  rmSync(work, { recursive: true, force: true });
  rmSync(join(root, 'dbml-error.log'), { force: true });
  return db;
}

async function rows(db, sql) {
  return new Set((await db.query(sql)).rows.map((row) => Object.values(row).join(' | ')));
}

const CATALOG = {
  columns: `select table_name, column_name, data_type, udt_name, is_nullable,
                   coalesce(column_default, '') as column_default,
                   coalesce(character_maximum_length, 0) as max_length,
                   coalesce(numeric_precision, 0) as precision,
                   coalesce(numeric_scale, 0) as scale
            from information_schema.columns where table_schema = 'public'`,
  enums: `select t.typname, e.enumlabel from pg_enum e join pg_type t on t.oid = e.enumtypid`,
  constraints: `select c.conrelid::regclass::text, c.conname, c.contype::text, pg_get_constraintdef(c.oid)
                from pg_constraint c where c.connamespace = 'public'::regnamespace`,
  indexes: `select indexname, indexdef from pg_indexes where schemaname = 'public'`,
};

const [migrated, dbml] = await Promise.all([fromMigrations(), fromDbml()]);
let problems = 0;

for (const [kind, sql] of Object.entries(CATALOG)) {
  const wanted = await rows(dbml, sql);
  const have = await rows(migrated, sql);
  for (const item of wanted) {
    if (!have.has(item)) {
      console.error(`In the DBML but not in db/migrations (${kind}): ${item}`);
      problems += 1;
    }
  }
  console.log(`${kind}: ${wanted.size} from the DBML, ${have.size} after migrations`);
}

await Promise.all([migrated.close(), dbml.close()]);

if (problems > 0) {
  console.error(`${problems} difference(s). Add a migration, or update docs/diagrams/claimlens.dbml.`);
  process.exit(1);
}
console.log('db/migrations contains everything in the DBML.');
