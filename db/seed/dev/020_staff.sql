-- Local and test accounts only. The seed command hashes Seed:DevPassword into $dev_password_hash$.
-- Two reviewers and two approvers, to test CLAIM_ALREADY_ASSIGNED and separation of duties.

insert into staff_account (id, email, password_hash, full_name, initials, role) values
  ('10000000-0000-4000-8000-000000000001', 'intake@claimlens.test',    '$dev_password_hash$', 'Nguyễn Minh Anh', 'MA', 'INTAKE_OFFICER'),
  ('10000000-0000-4000-8000-000000000002', 'ha.tran@claimlens.test',   '$dev_password_hash$', 'Trần Thu Hà',     'TH', 'CLAIMS_REVIEWER'),
  ('10000000-0000-4000-8000-000000000003', 'duc.pham@claimlens.test',  '$dev_password_hash$', 'Phạm Anh Đức',    'PD', 'CLAIMS_REVIEWER'),
  ('10000000-0000-4000-8000-000000000004', 'quan.le@claimlens.test',   '$dev_password_hash$', 'Lê Minh Quân',    'LQ', 'SENIOR_APPROVER'),
  ('10000000-0000-4000-8000-000000000005', 'mai.vo@claimlens.test',    '$dev_password_hash$', 'Võ Thanh Mai',    'VM', 'SENIOR_APPROVER'),
  ('10000000-0000-4000-8000-000000000006', 'admin@claimlens.test',     '$dev_password_hash$', 'Đỗ Hải Nam',      'DN', 'ADMINISTRATOR');
