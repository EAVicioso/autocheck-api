-- AutoCheck SRS 1.1: representative synthetic main data, isolated CI database only.
begin;
insert into autocheck."Center" ("Id", "Name", "Address", "Active") values ('30000000-0000-0000-0000-000000000001', 'Upgrade fixture', 'Synthetic', true);
insert into autocheck."UserAccount" ("Id", "AuthId", "Email", "Name", "Phone", "Role", "Permissions", "CenterId", "Active") values
 ('30000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000012','upgrade-owner@example.test','Synthetic owner','','Ciudadano',array[]::text[],null,true),
 ('30000000-0000-0000-0000-000000000003','30000000-0000-0000-0000-000000000013','upgrade-inspector@example.test','Synthetic inspector','','Inspector',array[]::text[],'30000000-0000-0000-0000-000000000001',true);
insert into autocheck."Ramp" ("Id","CenterId","Name","Active") values ('30000000-0000-0000-0000-000000000004','30000000-0000-0000-0000-000000000001','Upgrade R1',true);
insert into autocheck."Vehicle" ("Id","OwnerId","Plate","Vin","Year","ModelId","ColorId") values
 ('30000000-0000-0000-0000-000000000005','30000000-0000-0000-0000-000000000002','UPGRADE-01','123456789ABCDEFGH',2020,'20000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000003');
insert into autocheck."PaymentAttempt" ("Id","UserId","VehicleId","CenterId","RampId","Starts","Expires","State","Scenario") values
 ('30000000-0000-0000-0000-000000000006','30000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000005','30000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000004','2026-11-11T14:00:00Z','2026-11-11T13:05:00Z','APROBADO','APROBADO');
insert into autocheck."Appointment" ("Id","AttemptId","UserId","VehicleId","CenterId","RampId","InspectorId","Starts","State") values
 ('30000000-0000-0000-0000-000000000007','30000000-0000-0000-0000-000000000006','30000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000005','30000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000004','30000000-0000-0000-0000-000000000003','2026-11-11T14:00:00Z','COMPLETADA');
insert into autocheck."Inspection" ("Id","AppointmentId","OwnerId","VehicleId","CenterId","RampId","InspectorId","Started","Closed","State","Result","RuleVersion","MaxMinor","Defects","Minor","Medium","Serious","Revision","MongoId","VehicleSnapshot","CenterSnapshot") values
 ('30000000-0000-0000-0000-000000000008','30000000-0000-0000-0000-000000000007','30000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000005','30000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000004','30000000-0000-0000-0000-000000000003','2026-11-11T14:00:00Z','2026-11-11T14:20:00Z','CERRADA','Aprobado','V1',3,'[]',0,0,0,2,'300000000000000000000008','{"matricula":"UPGRADE-01","vin":"123456789ABCDEFGH","anio":2020}','{"nombre":"Upgrade fixture","direccion":"Synthetic"}');
insert into autocheck."AuditEvent" ("Id","ActorId","At","Operation","Entity","Outcome","Correlation") values
 ('30000000-0000-0000-0000-000000000009','30000000-0000-0000-0000-000000000003','2026-11-11T14:20:00Z','UPGRADE_FIXTURE','30000000-0000-0000-0000-000000000008','CERRADA','upgrade-fixture');
commit;
