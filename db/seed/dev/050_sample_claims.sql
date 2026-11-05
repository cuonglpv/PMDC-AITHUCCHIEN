-- Synthetic claim for rule-set test runs (US20). It uses the golden case's five lines.

insert into sample_claim (code, name, plan_id, service_date, first_enrolled_on, consumed_before, lines, created_by) values
  ('SMP-GOLDEN-01', 'Golden case: excluded line, capped by the annual limit', '20000000-0000-4000-8000-000000000001',
   '2026-10-12', '2024-01-01', 4200000,
   '[
      {"line_no": 1, "description": "Khám nội tổng quát",            "category_code": "CONSULTATION",       "claimed_amount": 300000},
      {"line_no": 2, "description": "Xét nghiệm công thức máu",      "category_code": "LAB_TEST",           "claimed_amount": 250000},
      {"line_no": 3, "description": "Amoxicillin 500mg × 20 viên",   "category_code": "PRESCRIPTION_DRUG",  "claimed_amount": 150000},
      {"line_no": 4, "description": "Kem dưỡng phục hồi da Obagi",   "category_code": "COSMETIC",           "claimed_amount": 300000},
      {"line_no": 5, "description": "Siêu âm ổ bụng",                "category_code": "DIAGNOSTIC_IMAGING", "claimed_amount": 400000}
    ]'::jsonb,
   '10000000-0000-4000-8000-000000000006');
