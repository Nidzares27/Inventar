SET NOCOUNT ON;

PRINT 'EF migration history';
SELECT MigrationId
FROM __EFMigrationsHistory
ORDER BY MigrationId;

PRINT 'Core table counts';
SELECT 'Tepisi' AS [TableName], COUNT(*) AS [RowCount] FROM dbo.Tepisi
UNION ALL
SELECT 'Prodaje', COUNT(*) FROM dbo.Prodaje
UNION ALL
SELECT 'AspNetUsers', COUNT(*) FROM dbo.AspNetUsers
UNION ALL
SELECT 'AspNetRoles', COUNT(*) FROM dbo.AspNetRoles
UNION ALL
SELECT 'AspNetUserRoles', COUNT(*) FROM dbo.AspNetUserRoles
UNION ALL
SELECT 'AspNetUserClaims', COUNT(*) FROM dbo.AspNetUserClaims;

PRINT 'Check for required user profile columns';
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'AspNetUsers'
  AND COLUMN_NAME IN ('FirstName', 'LastName')
ORDER BY COLUMN_NAME;

PRINT 'Legacy Tepisi columns before/after upgrade';
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'Tepisi'
  AND COLUMN_NAME IN (
      'IsPublished',
      'OnlinePrice',
      'ReservedQuantity',
      'RowVersion',
      'SeoDescription',
      'SeoTitle',
      'ShortDescription',
      'Slug',
      'BroaderCategory',
      'NarrowerCategory',
      'PoMjeri',
      'UnID')
ORDER BY COLUMN_NAME;

PRINT 'Legacy Prodaje columns before/after upgrade';
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'Prodaje'
  AND COLUMN_NAME IN ('ConsumedLength', 'CustomLength', 'CustomWidth')
ORDER BY COLUMN_NAME;

PRINT 'Commerce tables before/after upgrade';
SELECT
    TABLE_SCHEMA,
    TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'commerce'
ORDER BY TABLE_NAME;

PRINT 'Storefront account schema before/after upgrade';
SELECT
    TABLE_SCHEMA,
    TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'commerce'
  AND TABLE_NAME IN ('StorefrontCustomers', 'StorefrontLoginCodes')
ORDER BY TABLE_NAME;

SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'commerce'
  AND TABLE_NAME = 'WebOrders'
  AND COLUMN_NAME = 'StorefrontCustomerId';
