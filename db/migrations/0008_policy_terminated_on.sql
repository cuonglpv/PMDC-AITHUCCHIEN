-- A policy that is no longer active records the first day it is not in force (docs/11-audit-and-roadmap.md, Q-A).
-- ELIG-01 treats the policy as not in force on and after this date, even inside its policy year.
-- Every policy so far is ACTIVE, so no row needs a date; a LAPSED or TERMINATED row without one stops this migration.

alter table policy add column terminated_on date;

alter table policy add constraint ck_policy_terminated_on check ((status = 'ACTIVE') = (terminated_on is null));

comment on column policy.terminated_on is 'First day a LAPSED or TERMINATED policy is not in force; null while ACTIVE (Q-A)';
