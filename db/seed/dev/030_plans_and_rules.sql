-- The golden case's plan and rule set (docs/06-data-model.md, section 9). All data is synthetic.
-- A version starts as a draft and is then published, so the same guards as the app apply.

insert into benefit_plan (id, code, name, description) values
  ('20000000-0000-4000-8000-000000000001', 'GOLD-OP', 'Gold Outpatient', 'Synthetic outpatient plan for the golden case');

insert into rule_set_version (id, plan_id, version_no, status, effective_from, change_note, created_by) values
  ('21000000-0000-4000-8000-000000000003', '20000000-0000-4000-8000-000000000001', 3, 'DRAFT', '2026-01-01',
   'Seeded version used by the golden case', '10000000-0000-4000-8000-000000000006');

insert into benefit_rule
  (rule_set_version_id, rule_code, rule_type, category_code, clause_text, clause_ref, apply_order, rate_bp, limit_amount, limit_scope, waiting_days)
values
  ('21000000-0000-4000-8000-000000000003', 'ELIG-01',    'ELIGIBILITY',    null,                 'The policy must be in force on the service date.',                  '1.1', 10,  null, null,    null,        null),
  ('21000000-0000-4000-8000-000000000003', 'WAIT-30',    'WAITING_PERIOD', null,                 'Claims are covered 30 days after first enrolment.',                 '2.1', 20,  null, null,    null,        30),
  ('21000000-0000-4000-8000-000000000003', 'EX-04',      'EXCLUSION',      'COSMETIC',           'Cosmetic products and services are not covered.',                   '4.4', 30,  null, null,    null,        null),
  ('21000000-0000-4000-8000-000000000003', 'COV-01',     'COVERAGE',       'CONSULTATION',       'Outpatient consultations are covered.',                             '3.1', 40,  null, null,    null,        null),
  ('21000000-0000-4000-8000-000000000003', 'COV-02',     'COVERAGE',       'PROCEDURE',          'Outpatient procedures are covered.',                                '3.2', 41,  null, null,    null,        null),
  ('21000000-0000-4000-8000-000000000003', 'COV-03',     'COVERAGE',       'LAB_TEST',           'Laboratory tests ordered by the doctor are covered.',               '3.3', 42,  null, null,    null,        null),
  ('21000000-0000-4000-8000-000000000003', 'COV-04',     'COVERAGE',       'DIAGNOSTIC_IMAGING', 'Diagnostic imaging ordered by the doctor is covered.',              '3.4', 43,  null, null,    null,        null),
  ('21000000-0000-4000-8000-000000000003', 'COV-05',     'COVERAGE',       'PRESCRIPTION_DRUG',  'Medication prescribed for the visit is covered.',                   '3.5', 44,  null, null,    null,        null),
  ('21000000-0000-4000-8000-000000000003', 'COINS-80',   'COINSURANCE',    null,                 'The insurer pays 80% of eligible expenses; the insured pays 20%.',  '5.1', 50,  8000, null,    null,        null),
  ('21000000-0000-4000-8000-000000000003', 'LIM-VISIT',  'LIMIT',          null,                 'At most 2,000,000 VND per visit.',                                  '6.1', 60,  null, 2000000, 'PER_VISIT', null),
  ('21000000-0000-4000-8000-000000000003', 'LIM-ANNUAL', 'LIMIT',          null,                 'At most 5,000,000 VND per policy year.',                            '6.2', 61,  null, 5000000, 'ANNUAL',    null);

update rule_set_version
  set status = 'PUBLISHED',
      published_by = '10000000-0000-4000-8000-000000000006',
      published_at = now()
  where id = '21000000-0000-4000-8000-000000000003';
