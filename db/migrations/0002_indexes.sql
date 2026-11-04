-- Rules the DBML cannot express (docs/06-data-model.md, section 10).

create unique index ux_recommendation_open
  on recommendation (claim_id)
  where status in ('DRAFT', 'SUBMITTED');

create unique index ux_rule_set_published_effective
  on rule_set_version (plan_id, effective_from)
  where status = 'PUBLISHED';

create unique index ux_rule_set_one_draft
  on rule_set_version (plan_id)
  where status = 'DRAFT';
