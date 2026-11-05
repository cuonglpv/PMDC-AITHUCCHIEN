-- The stable benefit taxonomy. Needed in every environment.

insert into benefit_category (code, name, description) values
  ('CONSULTATION',       'Consultation',       'Doctor visits and examinations'),
  ('PROCEDURE',          'Procedure',          'Outpatient procedures'),
  ('LAB_TEST',           'Laboratory tests',   'Blood, urine and other laboratory tests'),
  ('DIAGNOSTIC_IMAGING', 'Diagnostic imaging', 'Ultrasound, X-ray, CT and MRI'),
  ('PRESCRIPTION_DRUG',  'Prescription drugs', 'Medication prescribed for the visit'),
  ('COSMETIC',           'Cosmetic products',  'Cosmetic products and services');
