-- Multi-test only. User explicitly authorized reports + stock view.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='20s';
LOCK TABLE iam.iam_users,iam.iam_permissions,iam.iam_user_permissions IN SHARE ROW EXCLUSIVE MODE;
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM iam.iam_users WHERE "Id"='942655ef-c621-4dea-9aaa-fea0a13b4651' AND "Username"='test1' AND "IsActive" AND NOT "IsDeleted" AND NOT "IsSuperAdmin") THEN
  RAISE EXCEPTION 'Unexpected test1 identity/status';
 END IF;
END $$;
INSERT INTO iam.iam_permissions
("Id","Code","NameI18n","DescriptionI18n","Module","PermissionType","IsActive","SortOrder","Kind","PageCode","ChannelScoped","ChannelScopedOverridden","IsCodeDefined","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),'reports.ai.use','{"tr":"AI Raporlama"}'::jsonb,
'{"tr":"Rapor tariflerini çalıştırma; ayrıca ilgili veri alanının görüntüleme yetkisi gerekir."}'::jsonb,
'Raporlar','manage',true,65,'page','AI Raporlama',false,false,true,now(),false
WHERE NOT EXISTS (SELECT 1 FROM iam.iam_permissions WHERE "Code"='reports.ai.use');
DO $$ BEGIN
 IF (SELECT count(*) FROM iam.iam_permissions WHERE "Code" IN ('reports.ai.use','inventory.view') AND "IsActive" AND NOT "IsDeleted" AND NOT "ChannelScoped")<>2 THEN
  RAISE EXCEPTION 'Requested permission unavailable or incorrectly scoped';
 END IF;
END $$;
INSERT INTO iam.iam_user_permissions
("Id","UserId","PermissionId","GrantType","ChannelIds","FirmId","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),'942655ef-c621-4dea-9aaa-fea0a13b4651',"Id",'grant',NULL,NULL,now(),false
FROM iam.iam_permissions WHERE "Code" IN ('reports.ai.use','inventory.view') AND NOT "IsDeleted"
ON CONFLICT ("UserId","PermissionId") DO UPDATE SET
 "GrantType"='grant',"ChannelIds"=NULL,"IsDeleted"=false,"DeletedAt"=NULL,"DeletedBy"=NULL,"UpdatedAt"=now();
DO $$ BEGIN
 IF (SELECT count(*) FROM iam.iam_user_permissions up JOIN iam.iam_permissions p ON p."Id"=up."PermissionId"
 WHERE up."UserId"='942655ef-c621-4dea-9aaa-fea0a13b4651' AND NOT up."IsDeleted" AND up."GrantType"='grant'
 AND p."Code" IN ('reports.ai.use','inventory.view'))<>2 THEN RAISE EXCEPTION 'Grant verification failed'; END IF;
END $$;
INSERT INTO iam.iam_audit_logs ("Id","UserId","EntityType","EntityId","Action","NewValues","Context","UserAgent","CreatedAt")
VALUES (gen_random_uuid(),NULL,'yetki.kullanici.istisna','942655ef-c621-4dea-9aaa-fea0a13b4651','istisna',
'{"deger":"reports.ai.use, inventory.view: grant"}'::jsonb,
'{"ozet":"Kullanıcı onayıyla test1 hesabına yalnız AI raporlama ve stok görüntüleme izni verildi.","hedefKullaniciId":"942655ef-c621-4dea-9aaa-fea0a13b4651"}'::jsonb,
'authorized-maintenance',now());
-- Caller appends ROLLBACK or COMMIT explicitly.
