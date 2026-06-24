SET NOCOUNT ON;

PRINT 'Core table counts after upgrade';
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
SELECT 'AspNetUserClaims', COUNT(*) FROM dbo.AspNetUserClaims
UNION ALL
SELECT 'commerce.WebOrders', COUNT(*) FROM commerce.WebOrders
UNION ALL
SELECT 'commerce.WebOrderItems', COUNT(*) FROM commerce.WebOrderItems
UNION ALL
SELECT 'commerce.InventoryReservations', COUNT(*) FROM commerce.InventoryReservations
UNION ALL
SELECT 'commerce.ProductImages', COUNT(*) FROM commerce.ProductImages;

PRINT 'Verify storefront defaults on legacy Tepisi rows';
SELECT
    SUM(CASE WHEN OnlinePrice IS NULL THEN 1 ELSE 0 END) AS MissingOnlinePrice,
    SUM(CASE WHEN BroaderCategory IS NULL OR LTRIM(RTRIM(BroaderCategory)) = '' THEN 1 ELSE 0 END) AS MissingBroaderCategory,
    SUM(CASE WHEN NarrowerCategory IS NULL OR LTRIM(RTRIM(NarrowerCategory)) = '' THEN 1 ELSE 0 END) AS MissingNarrowerCategory,
    SUM(CASE WHEN IsPublished = 1 THEN 1 ELSE 0 END) AS PublishedProducts,
    SUM(CASE WHEN ReservedQuantity <> 0 THEN 1 ELSE 0 END) AS NonZeroReservedQuantity,
    SUM(CASE WHEN PoMjeri = 1 THEN 1 ELSE 0 END) AS PoMjeriProducts,
    SUM(CASE WHEN UnID IS NOT NULL THEN 1 ELSE 0 END) AS ProductsWithUnID
FROM dbo.Tepisi;

PRINT 'Sample storefront category values';
SELECT TOP (20)
    Id,
    Name,
    ProductNumber,
    BroaderCategory,
    NarrowerCategory,
    OnlinePrice,
    IsPublished
FROM dbo.Tepisi
ORDER BY Id;

PRINT 'Verify commerce schema exists';
SELECT
    TABLE_SCHEMA,
    TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'commerce'
ORDER BY TABLE_NAME;

PRINT 'Verify storefront account schema exists';
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
