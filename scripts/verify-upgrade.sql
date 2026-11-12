-- AutoCheck SRS 1.1: check main data survived migrations without recalculating history.
do $verify$ begin
 if not exists (select 1 from autocheck."Vehicle" where "Id"='30000000-0000-0000-0000-000000000005' and "Plate"='UPGRADE-01') then
   raise exception 'Upgrade lost vehicle data';
 end if;
 if not exists (select 1 from autocheck."Inspection" where "Id"='30000000-0000-0000-0000-000000000008' and "Result"='Aprobado' and "RuleVersion"='V1' and "MaxMinor"=3 and "Defects"='[]'::jsonb) then
   raise exception 'Upgrade changed or lost closed history';
 end if;
 if not exists (select 1 from autocheck."AuditEvent" where "Id"='30000000-0000-0000-0000-000000000009' and "Outcome"='CERRADA') then
   raise exception 'Upgrade changed or lost audit evidence';
 end if;
end $verify$;
