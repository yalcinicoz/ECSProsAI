SELECT json_build_object(
'roles',(SELECT coalesce(json_agg(r ORDER BY r."Id"),'[]') FROM iam.iam_roles r),
'permissions',(SELECT coalesce(json_agg(p ORDER BY p."Id"),'[]') FROM iam.iam_permissions p),
'users',(SELECT coalesce(json_agg(to_jsonb(u)-'Preferences'-'LastLoginAt' ORDER BY u."Id"),'[]') FROM iam.iam_users u),
'userRoles',(SELECT coalesce(json_agg(r ORDER BY r."Id"),'[]') FROM iam.iam_user_roles r),
'rolePermissions',(SELECT coalesce(json_agg(r ORDER BY r."Id"),'[]') FROM iam.iam_role_permissions r),
'userPermissions',(SELECT coalesce(json_agg(r ORDER BY r."Id"),'[]') FROM iam.iam_user_permissions r),
'firms',(SELECT coalesce(json_agg(json_build_object('Id',"Id",'Code',"Code") ORDER BY "Id"),'[]') FROM core.core_firms WHERE NOT "IsDeleted"),
'channels',(SELECT coalesce(json_agg(json_build_object('Id',"Id",'Code',"Code",'FirmId',"FirmId") ORDER BY "Id"),'[]') FROM core.core_firm_platforms WHERE NOT "IsDeleted"));
