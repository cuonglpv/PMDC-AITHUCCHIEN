-- Synthetic members. The golden case's balance is 5,000,000 - 4,200,000 = 800,000 VND (docs/06-data-model.md, section 9).

insert into member (id, member_code, full_name, national_id, date_of_birth) values
  ('30000000-0000-4000-8000-000000000001', 'MB-0007731', 'Nguyễn Văn An',      '000000000001', '1988-04-12'),
  ('30000000-0000-4000-8000-000000000002', 'MB-0007732', 'Phạm Thị Bích Ngọc', '000000000002', '1993-09-27');

insert into policy
  (id, policy_number, member_id, plan_id, status, coverage_start, coverage_end, first_enrolled_on, opening_consumed, consumed_amount, balance_version)
values
  ('31000000-0000-4000-8000-000000000001', 'PL-2026-00731', '30000000-0000-4000-8000-000000000001', '20000000-0000-4000-8000-000000000001',
   'ACTIVE', '2026-01-01', '2026-12-31', '2024-01-01', 4200000, 4200000, 1),
  ('31000000-0000-4000-8000-000000000002', 'PL-2026-00732', '30000000-0000-4000-8000-000000000002', '20000000-0000-4000-8000-000000000001',
   'ACTIVE', '2026-01-01', '2026-12-31', '2026-01-01', 0, 0, 0);
