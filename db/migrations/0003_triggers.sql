-- The database repeats the critical rules (C3, rule 6). Every failure below names a ck_ constraint,
-- which is how the Claims API turns it into an error code (ConstraintErrorMap).

-- Claim lifecycle: docs/03-use-cases.md and docs/06-data-model.md, section 7.
create function claim_status_guard() returns trigger language plpgsql as $$
declare
  latest recommendation%rowtype;
begin
  if tg_op = 'INSERT' then
    if new.status <> 'DRAFT' then
      raise exception 'A claim starts as DRAFT, not %', new.status
        using errcode = 'check_violation', constraint = 'ck_claim_lifecycle';
    end if;
    return new;
  end if;

  if new.status = old.status then
    return new;
  end if;

  if not (
       (old.status = 'DRAFT'              and new.status = 'PROCESSING')
    or (old.status = 'PROCESSING'         and new.status in ('NEEDS_VERIFICATION', 'READY_FOR_REVIEW'))
    or (old.status = 'NEEDS_VERIFICATION' and new.status = 'READY_FOR_REVIEW')
    or (old.status = 'READY_FOR_REVIEW'   and new.status = 'IN_REVIEW')
    or (old.status = 'IN_REVIEW'          and new.status = 'PENDING_APPROVAL')
    or (old.status = 'PENDING_APPROVAL'   and new.status in ('IN_REVIEW', 'APPROVED', 'PARTIALLY_APPROVED', 'REJECTED'))
  ) then
    raise exception 'A claim cannot move from % to %', old.status, new.status
      using errcode = 'check_violation', constraint = 'ck_claim_lifecycle';
  end if;

  if old.status = 'PENDING_APPROVAL' then
    select * into latest from recommendation
      where claim_id = new.id
      order by created_at desc, id desc
      limit 1;

    -- With no recommendation these comparisons are NULL, and that must refuse the move too.
    if not coalesce(
         (new.status = 'IN_REVIEW'          and latest.status = 'RETURNED')
      or (new.status = 'APPROVED'           and latest.status = 'DECIDED' and latest.decision_action = 'APPROVE')
      or (new.status = 'PARTIALLY_APPROVED' and latest.status = 'DECIDED' and latest.decision_action = 'PARTIALLY_APPROVE')
      or (new.status = 'REJECTED'           and latest.status = 'DECIDED' and latest.decision_action = 'REJECT'),
      false
    ) then
      raise exception 'Status % needs a matching decision by a Senior Approver (BR-01)', new.status
        using errcode = 'check_violation', constraint = 'ck_claim_final_needs_decision';
    end if;
  end if;

  new.status_changed_at := now();
  return new;
end;
$$;

create trigger tg_claim_status_guard
  before insert or update of status on claim
  for each row execute function claim_status_guard();

-- Published rule sets are frozen: docs/06-data-model.md, section 10, and US19.
create function rule_set_version_guard() returns trigger language plpgsql as $$
begin
  if tg_op = 'DELETE' then
    if old.status = 'PUBLISHED' then
      raise exception 'A published rule set cannot be deleted'
        using errcode = 'check_violation', constraint = 'ck_rule_set_frozen';
    end if;
    return old;
  end if;

  if tg_op = 'UPDATE' and old.status = 'PUBLISHED' then
    raise exception 'A published rule set cannot be changed'
      using errcode = 'check_violation', constraint = 'ck_rule_set_frozen';
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

create trigger tg_rule_set_version_guard
  before insert or update or delete on rule_set_version
  for each row execute function rule_set_version_guard();

create function benefit_rule_guard() returns trigger language plpgsql as $$
declare
  version_id uuid := coalesce(new.rule_set_version_id, old.rule_set_version_id);
begin
  if (select status from rule_set_version where id = version_id) <> 'DRAFT' then
    raise exception 'Only a draft rule set can change'
      using errcode = 'check_violation', constraint = 'ck_rule_set_frozen';
  end if;
  return coalesce(new, old);
end;
$$;

create trigger tg_benefit_rule_guard
  before insert or update or delete on benefit_rule
  for each row execute function benefit_rule_guard();

-- Approval and usage stay in step: BR-10. The decision is written first, then the policy.
create function policy_usage_guard() returns trigger language plpgsql as $$
declare
  approved bigint;
begin
  select coalesce(sum(r.final_payable), 0) into approved
    from recommendation r
    join claim c on c.id = r.claim_id
    where c.policy_id = new.id
      and r.decision_action in ('APPROVE', 'PARTIALLY_APPROVE');

  if new.consumed_amount <> new.opening_consumed + approved then
    raise exception 'Usage must equal the opening amount plus approved payables'
      using errcode = 'check_violation', constraint = 'ck_policy_usage_in_step';
  end if;
  return new;
end;
$$;

create trigger tg_policy_usage_guard
  before update of consumed_amount on policy
  for each row execute function policy_usage_guard();
