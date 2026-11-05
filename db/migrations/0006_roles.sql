-- The app connects as claimlens_app. Migrations run as the owner.
-- The password is set by the migration runner, from the app's connection string.

do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'claimlens_app') then
    create role claimlens_app login;
  end if;
end;
$$;

grant usage on schema public to claimlens_app;
grant select, insert, update, delete on all tables in schema public to claimlens_app;
grant usage, select on all sequences in schema public to claimlens_app;

-- Audit is insert-only (BR-12).
revoke update, delete on audit_event from claimlens_app;

-- The runner's own bookkeeping is not the app's business.
revoke all on schema_migration from claimlens_app;
