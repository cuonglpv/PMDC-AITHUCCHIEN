-- 1. Tables and sequences that later migrations create are the app's to use, as 0006 made the existing ones.
--    Migrations run as the owner (ConnectionStrings:Owner), so these defaults cover whatever the owner creates next.
--    They change nothing that exists: audit_event stays insert-only and schema_migration stays private (0006).
--    The runner revokes the seed journal, schema_seed, after seeding, because it is created after this script.

alter default privileges for role current_user in schema public
  grant select, insert, update, delete on tables to claimlens_app;

alter default privileges for role current_user in schema public
  grant usage, select on sequences to claimlens_app;

-- 0006 granted every sequence, the journal's id sequence included. The journal is not the app's business.
do $$
declare
  journal_sequence text := pg_get_serial_sequence('schema_migration', 'schemaversionsid');
begin
  if journal_sequence is not null then
    execute format('revoke all on sequence %s from claimlens_app', journal_sequence);
  end if;
end;
$$;

-- 2. A rule set version is born a draft and moves once: DRAFT -> PUBLISHED or DRAFT -> DISCARDED.
--    0003 froze only PUBLISHED, so a DISCARDED version could return to DRAFT or be published.
--    Both names map to RULE_SET_NOT_DRAFT (Http/ConstraintErrorMap.cs).

create or replace function rule_set_version_guard() returns trigger language plpgsql as $$
begin
  if tg_op = 'DELETE' then
    if old.status = 'PUBLISHED' then
      raise exception 'A published rule set cannot be deleted'
        using errcode = 'check_violation', constraint = 'ck_rule_set_frozen';
    end if;
    return old;
  end if;

  if tg_op = 'INSERT' and new.status <> 'DRAFT' then
    raise exception 'A rule set version starts as a draft'
      using errcode = 'check_violation', constraint = 'ck_rule_set_status_transition';
  end if;

  if tg_op = 'UPDATE' then
    if old.status = 'PUBLISHED' then
      raise exception 'A published rule set cannot be changed'
        using errcode = 'check_violation', constraint = 'ck_rule_set_frozen';
    end if;

    if new.status <> old.status and not (old.status = 'DRAFT' and new.status in ('PUBLISHED', 'DISCARDED')) then
      raise exception 'A rule set moves only from draft to published or discarded'
        using errcode = 'check_violation', constraint = 'ck_rule_set_status_transition';
    end if;

    if old.status = 'DISCARDED' then
      raise exception 'A discarded rule set cannot be changed'
        using errcode = 'check_violation', constraint = 'ck_rule_set_frozen';
    end if;
  end if;

  if new.status = 'PUBLISHED' then
    if exists (
      select 1 from rule_set_version v
      where v.plan_id = new.plan_id
        and v.status = 'PUBLISHED'
        and v.id <> new.id
        and v.effective_from >= new.effective_from
    ) then
      raise exception 'The effective date must be later than the current version''s'
        using errcode = 'check_violation', constraint = 'ck_rule_set_effective_later';
    end if;

    if exists (
      select 1 from benefit_category c
      where c.is_active
        and not exists (
          select 1 from benefit_rule r
          where r.rule_set_version_id = new.id
            and r.category_code = c.code
            and r.rule_type in ('COVERAGE', 'EXCLUSION')
        )
    ) then
      raise exception 'Every active category must be covered or excluded'
        using errcode = 'check_violation', constraint = 'ck_rule_set_categories';
    end if;
  end if;

  return new;
end;
$$;
