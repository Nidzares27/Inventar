IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240927143240_InitialCreate'
)
BEGIN
    CREATE TABLE [Tepisi] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Quantity] int NOT NULL,
        [QRCodeUrl] nvarchar(max) NULL,
        CONSTRAINT [PK_Tepisi] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240927143240_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240927143240_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20241006202058_AddedDateTimeField'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Name');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [Name] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20241006202058_AddedDateTimeField'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [DateTime] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20241006202058_AddedDateTimeField'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20241006202058_AddedDateTimeField', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20241031192338_ConnectingDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20241031192338_ConnectingDB', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217073815_ExpandedTepihModel'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Color] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217073815_ExpandedTepihModel'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [M3] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217073815_ExpandedTepihModel'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Price] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217073815_ExpandedTepihModel'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Size] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217073815_ExpandedTepihModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250217073815_ExpandedTepihModel', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217090704_SalesAdded'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250217090704_SalesAdded', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217090950_SalesAdded2'
)
BEGIN
    CREATE TABLE [Prodaje] (
        [Id] int NOT NULL IDENTITY,
        [TepihId] int NOT NULL,
        [Quantity] int NOT NULL,
        [CustomerFullName] nvarchar(max) NOT NULL,
        [VrijemeProdaje] datetime2 NOT NULL,
        CONSTRAINT [PK_Prodaje] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217090950_SalesAdded2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250217090950_SalesAdded2', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217092352_SalesForeignKey'
)
BEGIN
    CREATE INDEX [IX_Prodaje_TepihId] ON [Prodaje] ([TepihId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217092352_SalesForeignKey'
)
BEGIN
    ALTER TABLE [Prodaje] ADD CONSTRAINT [FK_Prodaje_Tepisi_TepihId] FOREIGN KEY ([TepihId]) REFERENCES [Tepisi] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217092352_SalesForeignKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250217092352_SalesForeignKey', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250312004816_NewTepihModel'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'M3');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Tepisi] DROP COLUMN [M3];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250312004816_NewTepihModel'
)
BEGIN
    EXEC sp_rename N'[Tepisi].[Size]', N'PriceUnit', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250312004816_NewTepihModel'
)
BEGIN
    EXEC sp_rename N'[Tepisi].[Price]', N'PriceM2', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250312004816_NewTepihModel'
)
BEGIN
    EXEC sp_rename N'[Tepisi].[Description]', N'ProductNumber', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250312004816_NewTepihModel'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Length] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250312004816_NewTepihModel'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Model] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250312004816_NewTepihModel'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Width] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250312004816_NewTepihModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250312004816_NewTepihModel', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250316162625_UpdatedSales'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [PriceM2] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250316162625_UpdatedSales'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [PriceUnit] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250316162625_UpdatedSales'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250316162625_UpdatedSales', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250317073557_AddedBuyer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250317073557_AddedBuyer', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250317083315_AddedBuyerr'
)
BEGIN
    CREATE TABLE [Kupci] (
        [Id] int NOT NULL IDENTITY,
        [CustomerFullName] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Kupci] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250317083315_AddedBuyerr'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250317083315_AddedBuyerr', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'PriceM2');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Tepisi] DROP COLUMN [PriceM2];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'PriceUnit');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Tepisi] DROP COLUMN [PriceUnit];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'PriceM2');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [Prodaje] DROP COLUMN [PriceM2];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'PriceUnit');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [Prodaje] DROP COLUMN [PriceUnit];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Width');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var6 + ';');
    EXEC(N'UPDATE [Tepisi] SET [Width] = 0 WHERE [Width] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [Width] int NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT 0 FOR [Width];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'ProductNumber');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var7 + ';');
    EXEC(N'UPDATE [Tepisi] SET [ProductNumber] = N'''' WHERE [ProductNumber] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [ProductNumber] nvarchar(max) NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT N'' FOR [ProductNumber];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Name');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var8 + ';');
    EXEC(N'UPDATE [Tepisi] SET [Name] = N'''' WHERE [Name] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [Name] nvarchar(max) NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT N'' FOR [Name];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Model');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var9 + ';');
    EXEC(N'UPDATE [Tepisi] SET [Model] = N'''' WHERE [Model] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [Model] nvarchar(max) NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT N'' FOR [Model];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Length');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var10 + ';');
    EXEC(N'UPDATE [Tepisi] SET [Length] = 0 WHERE [Length] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [Length] int NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT 0 FOR [Length];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Color');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var11 + ';');
    EXEC(N'UPDATE [Tepisi] SET [Color] = N'''' WHERE [Color] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [Color] nvarchar(max) NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT N'' FOR [Color];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [PerM2] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Price] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [Price] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250329113158_PerM2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250329113158_PerM2', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250405143454_Payment'
)
BEGIN
    ALTER TABLE [Kupci] ADD [LeftToPay] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250405143454_Payment'
)
BEGIN
    CREATE TABLE [Placanja] (
        [Id] int NOT NULL IDENTITY,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Placanja] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250405143454_Payment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250405143454_Payment', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250405151908_PaymentAddedBuyerName'
)
BEGIN
    ALTER TABLE [Placanja] ADD [CustomerName] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250405151908_PaymentAddedBuyerName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250405151908_PaymentAddedBuyerName', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250420104726_PlannedPaymentType'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [PlannedPaymentType] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250420104726_PlannedPaymentType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250420104726_PlannedPaymentType', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250420120707_PaymentType'
)
BEGIN
    ALTER TABLE [Placanja] ADD [PaymentType] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250420120707_PaymentType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250420120707_PaymentType', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(max) NOT NULL,
        [LastName] nvarchar(max) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250422004315_AppUser'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250422004315_AppUser', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250424125245_AddedProdavac'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [Prodavac] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250424125245_AddedProdavac'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250424125245_AddedProdavac', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Width');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [Width] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'ProductNumber');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [ProductNumber] nvarchar(20) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Name');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [Name] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Model');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [Model] nvarchar(30) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Length');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [Length] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Color');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [Color] nvarchar(40) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Description] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'Prodavac');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [Prodaje] ALTER COLUMN [Prodavac] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'PlannedPaymentType');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [Prodaje] ALTER COLUMN [PlannedPaymentType] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'CustomerFullName');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [Prodaje] ALTER COLUMN [CustomerFullName] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Placanja]') AND [c].[name] = N'PaymentType');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Placanja] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [Placanja] ALTER COLUMN [PaymentType] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Placanja]') AND [c].[name] = N'CustomerName');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [Placanja] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [Placanja] ALTER COLUMN [CustomerName] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Kupci]') AND [c].[name] = N'CustomerFullName');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [Kupci] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [Kupci] ALTER COLUMN [CustomerFullName] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'LastName');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [AspNetUsers] ALTER COLUMN [LastName] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'FirstName');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [AspNetUsers] ALTER COLUMN [FirstName] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250525180613_AddedDescription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250525180613_AddedDescription', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250612101149_DodatDisabled'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Disabled] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250612101149_DodatDisabled'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [Disabled] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250612101149_DodatDisabled'
)
BEGIN
    ALTER TABLE [Placanja] ADD [Disabled] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250612101149_DodatDisabled'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250612101149_DodatDisabled', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250619182703_PropertiesObavezna'
)
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'Disabled');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var26 + ';');
    EXEC(N'UPDATE [Tepisi] SET [Disabled] = CAST(0 AS bit) WHERE [Disabled] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [Disabled] bit NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT CAST(0 AS bit) FOR [Disabled];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250619182703_PropertiesObavezna'
)
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'Prodavac');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var27 + ';');
    EXEC(N'UPDATE [Prodaje] SET [Prodavac] = N'''' WHERE [Prodavac] IS NULL');
    ALTER TABLE [Prodaje] ALTER COLUMN [Prodavac] nvarchar(50) NOT NULL;
    ALTER TABLE [Prodaje] ADD DEFAULT N'' FOR [Prodavac];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250619182703_PropertiesObavezna'
)
BEGIN
    DECLARE @var28 nvarchar(max);
    SELECT @var28 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'PlannedPaymentType');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var28 + ';');
    EXEC(N'UPDATE [Prodaje] SET [PlannedPaymentType] = N'''' WHERE [PlannedPaymentType] IS NULL');
    ALTER TABLE [Prodaje] ALTER COLUMN [PlannedPaymentType] nvarchar(20) NOT NULL;
    ALTER TABLE [Prodaje] ADD DEFAULT N'' FOR [PlannedPaymentType];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250619182703_PropertiesObavezna'
)
BEGIN
    DECLARE @var29 nvarchar(max);
    SELECT @var29 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'Disabled');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var29 + ';');
    EXEC(N'UPDATE [Prodaje] SET [Disabled] = CAST(0 AS bit) WHERE [Disabled] IS NULL');
    ALTER TABLE [Prodaje] ALTER COLUMN [Disabled] bit NOT NULL;
    ALTER TABLE [Prodaje] ADD DEFAULT CAST(0 AS bit) FOR [Disabled];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250619182703_PropertiesObavezna'
)
BEGIN
    DECLARE @var30 nvarchar(max);
    SELECT @var30 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Placanja]') AND [c].[name] = N'Disabled');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [Placanja] DROP CONSTRAINT ' + @var30 + ';');
    EXEC(N'UPDATE [Placanja] SET [Disabled] = CAST(0 AS bit) WHERE [Disabled] IS NULL');
    ALTER TABLE [Placanja] ALTER COLUMN [Disabled] bit NOT NULL;
    ALTER TABLE [Placanja] ADD DEFAULT CAST(0 AS bit) FOR [Disabled];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250619182703_PropertiesObavezna'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250619182703_PropertiesObavezna', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250709103923_AddingDebt'
)
BEGIN
    ALTER TABLE [Kupci] ADD [Debt] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250709103923_AddingDebt'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250709103923_AddingDebt', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250709172253_DebtModel'
)
BEGIN
    DECLARE @var31 nvarchar(max);
    SELECT @var31 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Kupci]') AND [c].[name] = N'Debt');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [Kupci] DROP CONSTRAINT ' + @var31 + ';');
    ALTER TABLE [Kupci] DROP COLUMN [Debt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250709172253_DebtModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250709172253_DebtModel', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250709172628_DebtModelAdded'
)
BEGIN
    CREATE TABLE [Dugovanja] (
        [Id] int NOT NULL IDENTITY,
        [CustomerFullName] nvarchar(50) NOT NULL,
        [DebtAmount] decimal(18,2) NOT NULL,
        [DebtTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Dugovanja] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250709172628_DebtModelAdded'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250709172628_DebtModelAdded', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250710181938_DugovanjaZaPublish'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250710181938_DugovanjaZaPublish', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250719112318_LanguageChangeUpdated'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250719112318_LanguageChangeUpdated', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251123172456_AddedRabatToProdaja'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [Rabat] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251123172456_AddedRabatToProdaja'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251123172456_AddedRabatToProdaja', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251205165851_AddedLogForHomeErrorRedirect'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251205165851_AddedLogForHomeErrorRedirect', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    IF SCHEMA_ID(N'commerce') IS NULL EXEC(N'CREATE SCHEMA [commerce];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [IsPublished] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [OnlinePrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [ReservedQuantity] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [RowVersion] rowversion NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [SeoDescription] nvarchar(320) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [SeoTitle] nvarchar(160) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [ShortDescription] nvarchar(240) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [Slug] nvarchar(160) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE TABLE [commerce].[ProductImages] (
        [Id] int NOT NULL IDENTITY,
        [TepihId] int NOT NULL,
        [CloudinaryPublicId] nvarchar(200) NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [ThumbnailUrl] nvarchar(500) NULL,
        [AltText] nvarchar(160) NULL,
        [IsPrimary] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [Disabled] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Tepisi_TepihId] FOREIGN KEY ([TepihId]) REFERENCES [Tepisi] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE TABLE [commerce].[WebOrders] (
        [Id] int NOT NULL IDENTITY,
        [OrderNumber] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CustomerFirstName] nvarchar(50) NOT NULL,
        [CustomerLastName] nvarchar(50) NOT NULL,
        [CustomerEmail] nvarchar(254) NOT NULL,
        [CustomerPhone] nvarchar(30) NULL,
        [ShippingAddressLine1] nvarchar(200) NOT NULL,
        [ShippingAddressLine2] nvarchar(200) NULL,
        [ShippingCity] nvarchar(100) NOT NULL,
        [ShippingPostalCode] nvarchar(20) NULL,
        [ShippingCountry] nvarchar(100) NOT NULL,
        [BillingAddressLine1] nvarchar(200) NULL,
        [BillingAddressLine2] nvarchar(200) NULL,
        [BillingCity] nvarchar(100) NULL,
        [BillingPostalCode] nvarchar(20) NULL,
        [BillingCountry] nvarchar(100) NULL,
        [Currency] nvarchar(30) NOT NULL,
        [ItemsTotal] decimal(18,2) NOT NULL,
        [ShippingTotal] decimal(18,2) NOT NULL,
        [DiscountTotal] decimal(18,2) NOT NULL,
        [GrandTotal] decimal(18,2) NOT NULL,
        [PaymentStatus] nvarchar(40) NOT NULL,
        [FulfillmentStatus] nvarchar(40) NOT NULL,
        [PaymentProvider] nvarchar(100) NULL,
        [PaymentReference] nvarchar(200) NULL,
        [CreatedUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [PaidUtc] datetime2 NULL,
        [CancelledUtc] datetime2 NULL,
        [CompletedUtc] datetime2 NULL,
        [CustomerNote] nvarchar(max) NULL,
        [InternalNote] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_WebOrders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE TABLE [commerce].[InventoryReservations] (
        [Id] int NOT NULL IDENTITY,
        [WebOrderId] int NOT NULL,
        [TepihId] int NOT NULL,
        [Quantity] int NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [ExpiresUtc] datetime2 NULL,
        [ReleasedUtc] datetime2 NULL,
        [Reason] nvarchar(100) NULL,
        CONSTRAINT [PK_InventoryReservations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryReservations_Tepisi_TepihId] FOREIGN KEY ([TepihId]) REFERENCES [Tepisi] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryReservations_WebOrders_WebOrderId] FOREIGN KEY ([WebOrderId]) REFERENCES [commerce].[WebOrders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE TABLE [commerce].[WebOrderItems] (
        [Id] int NOT NULL IDENTITY,
        [WebOrderId] int NOT NULL,
        [TepihId] int NOT NULL,
        [ProductName] nvarchar(50) NOT NULL,
        [ProductNumber] nvarchar(20) NOT NULL,
        [Model] nvarchar(30) NULL,
        [Color] nvarchar(40) NULL,
        [Length] int NULL,
        [Width] int NULL,
        [PerM2] bit NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [PrimaryImageUrl] nvarchar(500) NULL,
        CONSTRAINT [PK_WebOrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WebOrderItems_Tepisi_TepihId] FOREIGN KEY ([TepihId]) REFERENCES [Tepisi] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WebOrderItems_WebOrders_WebOrderId] FOREIGN KEY ([WebOrderId]) REFERENCES [commerce].[WebOrders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE TABLE [commerce].[WebOrderStatusHistory] (
        [Id] int NOT NULL IDENTITY,
        [WebOrderId] int NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [ChangedBy] nvarchar(50) NULL,
        [Note] nvarchar(500) NULL,
        [ChangedUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_WebOrderStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WebOrderStatusHistory_WebOrders_WebOrderId] FOREIGN KEY ([WebOrderId]) REFERENCES [commerce].[WebOrders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Tepisi_Slug] ON [Tepisi] ([Slug]) WHERE [Slug] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE INDEX [IX_InventoryReservations_TepihId_Status] ON [commerce].[InventoryReservations] ([TepihId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE INDEX [IX_InventoryReservations_WebOrderId_Status] ON [commerce].[InventoryReservations] ([WebOrderId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE INDEX [IX_ProductImages_TepihId_IsPrimary] ON [commerce].[ProductImages] ([TepihId], [IsPrimary]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE INDEX [IX_ProductImages_TepihId_SortOrder] ON [commerce].[ProductImages] ([TepihId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE INDEX [IX_WebOrderItems_TepihId] ON [commerce].[WebOrderItems] ([TepihId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE INDEX [IX_WebOrderItems_WebOrderId] ON [commerce].[WebOrderItems] ([WebOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WebOrders_OrderNumber] ON [commerce].[WebOrders] ([OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE INDEX [IX_WebOrders_Status_CreatedUtc] ON [commerce].[WebOrders] ([Status], [CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    CREATE INDEX [IX_WebOrderStatusHistory_WebOrderId_ChangedUtc] ON [commerce].[WebOrderStatusHistory] ([WebOrderId], [ChangedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321144713_AddStorefrontProductFieldsAndCommerceTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321144713_AddStorefrontProductFieldsAndCommerceTables', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321154500_BackfillStorefrontCatalogData'
)
BEGIN
    UPDATE T
    SET T.OnlinePrice = T.Price
    FROM Tepisi AS T
    WHERE T.OnlinePrice IS NULL;

    ;WITH SlugSeed AS
    (
        SELECT
            T.Id,
            BaseSlug = LOWER(
                CONCAT_WS(
                    '-',
                    NULLIF(LTRIM(RTRIM(T.Name)), ''),
                    NULLIF(LTRIM(RTRIM(T.ProductNumber)), ''),
                    CASE
                        WHEN T.Width IS NOT NULL AND T.Length IS NOT NULL THEN CONCAT(T.Width, 'x', T.Length)
                        WHEN T.Width IS NOT NULL THEN CONVERT(nvarchar(20), T.Width)
                        WHEN T.Length IS NOT NULL THEN CONVERT(nvarchar(20), T.Length)
                        ELSE NULL
                    END,
                    NULLIF(LTRIM(RTRIM(T.Color)), ''),
                    CONVERT(nvarchar(20), T.Id)
                )
            )
        FROM Tepisi AS T
        WHERE T.Slug IS NULL OR LTRIM(RTRIM(T.Slug)) = ''
    ),
    SanitizedSlug AS
    (
        SELECT
            S.Id,
            CleanSlug = LEFT(
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    TRANSLATE(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(S.BaseSlug, NCHAR(273), 'dj'),
                                        NCHAR(353), 's'),
                                    NCHAR(269), 'c'),
                                NCHAR(263), 'c'),
                            NCHAR(382), 'z'),
                        N' /\,.:;()[]{}+_&|''"?!',
                        N'---------------------'
                    ),
                    '--', '-'),
                    '--', '-'),
                    '--', '-'),
                    '--', '-'),
                    '--', '-'),
                160
            )
        FROM SlugSeed AS S
    )
    UPDATE T
    SET T.Slug = CASE
        WHEN SS.CleanSlug IS NULL OR SS.CleanSlug = '' THEN CONCAT('tepih-', T.Id)
        ELSE SS.CleanSlug
    END
    FROM Tepisi AS T
    INNER JOIN SanitizedSlug AS SS
        ON SS.Id = T.Id
    WHERE T.Slug IS NULL OR LTRIM(RTRIM(T.Slug)) = '';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321154500_BackfillStorefrontCatalogData'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321154500_BackfillStorefrontCatalogData', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330205635_Kategorije'
)
BEGIN
    IF COL_LENGTH(N'dbo.Tepisi', N'BroaderCategory') IS NULL
    BEGIN
        ALTER TABLE [Tepisi] ADD [BroaderCategory] nvarchar(100) NULL;
    END;

    IF COL_LENGTH(N'dbo.Tepisi', N'NarrowerCategory') IS NULL
    BEGIN
        ALTER TABLE [Tepisi] ADD [NarrowerCategory] nvarchar(100) NULL;
    END;

    EXEC(N'
    UPDATE [Tepisi]
    SET [BroaderCategory] = N''default''
    WHERE [BroaderCategory] IS NULL OR LTRIM(RTRIM([BroaderCategory])) = N'''';
    ');

    EXEC(N'
    UPDATE [Tepisi]
    SET [NarrowerCategory] = N''default''
    WHERE [NarrowerCategory] IS NULL OR LTRIM(RTRIM([NarrowerCategory])) = N'''';
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330205635_Kategorije'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260330205635_Kategorije', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330210249_Kategorije optional'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260330210249_Kategorije optional', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330210520_Kategorije optional2'
)
BEGIN
    IF COL_LENGTH(N'dbo.Tepisi', N'BroaderCategory') IS NULL
    BEGIN
        ALTER TABLE [Tepisi] ADD [BroaderCategory] nvarchar(100) NULL;
    END;

    IF COL_LENGTH(N'dbo.Tepisi', N'NarrowerCategory') IS NULL
    BEGIN
        ALTER TABLE [Tepisi] ADD [NarrowerCategory] nvarchar(100) NULL;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330210520_Kategorije optional2'
)
BEGIN
    DECLARE @var32 nvarchar(max);
    SELECT @var32 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'NarrowerCategory');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var32 + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [NarrowerCategory] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330210520_Kategorije optional2'
)
BEGIN
    DECLARE @var33 nvarchar(max);
    SELECT @var33 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'BroaderCategory');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var33 + ';');
    ALTER TABLE [Tepisi] ALTER COLUMN [BroaderCategory] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330210520_Kategorije optional2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260330210520_Kategorije optional2', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330213455_Kategorije optional3'
)
BEGIN
    IF COL_LENGTH(N'dbo.Tepisi', N'BroaderCategory') IS NULL
    BEGIN
        ALTER TABLE [Tepisi] ADD [BroaderCategory] nvarchar(100) NULL;
    END;

    IF COL_LENGTH(N'dbo.Tepisi', N'NarrowerCategory') IS NULL
    BEGIN
        ALTER TABLE [Tepisi] ADD [NarrowerCategory] nvarchar(100) NULL;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330213455_Kategorije optional3'
)
BEGIN
    DECLARE @var34 nvarchar(max);
    SELECT @var34 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'NarrowerCategory');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var34 + ';');
    EXEC(N'UPDATE [Tepisi] SET [NarrowerCategory] = N'''' WHERE [NarrowerCategory] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [NarrowerCategory] nvarchar(100) NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT N'' FOR [NarrowerCategory];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330213455_Kategorije optional3'
)
BEGIN
    DECLARE @var35 nvarchar(max);
    SELECT @var35 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tepisi]') AND [c].[name] = N'BroaderCategory');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [Tepisi] DROP CONSTRAINT ' + @var35 + ';');
    EXEC(N'UPDATE [Tepisi] SET [BroaderCategory] = N'''' WHERE [BroaderCategory] IS NULL');
    ALTER TABLE [Tepisi] ALTER COLUMN [BroaderCategory] nvarchar(100) NOT NULL;
    ALTER TABLE [Tepisi] ADD DEFAULT N'' FOR [BroaderCategory];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330213455_Kategorije optional3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260330213455_Kategorije optional3', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504170000_AddPoMjeriProducts'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [ConsumedLength] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504170000_AddPoMjeriProducts'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [CustomLength] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504170000_AddPoMjeriProducts'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [CustomWidth] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504170000_AddPoMjeriProducts'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [PoMjeri] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504170000_AddPoMjeriProducts'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [UnID] nvarchar(6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504170000_AddPoMjeriProducts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Tepisi_UnID] ON [Tepisi] ([UnID]) WHERE [UnID] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504170000_AddPoMjeriProducts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504170000_AddPoMjeriProducts', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504181301_20260504193000_SyncPendingModelChanges'
)
BEGIN
    DECLARE @var36 nvarchar(max);
    SELECT @var36 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[commerce].[WebOrders]') AND [c].[name] = N'CustomerEmail');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [commerce].[WebOrders] DROP CONSTRAINT ' + @var36 + ';');
    ALTER TABLE [commerce].[WebOrders] ALTER COLUMN [CustomerEmail] nvarchar(254) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504181301_20260504193000_SyncPendingModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504181301_20260504193000_SyncPendingModelChanges', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505174022_AddStorefrontPoMjeriReservations'
)
BEGIN
    ALTER TABLE [commerce].[InventoryReservations] ADD [CutLength] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505174022_AddStorefrontPoMjeriReservations'
)
BEGIN
    ALTER TABLE [commerce].[InventoryReservations] ADD [CutWidth] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505174022_AddStorefrontPoMjeriReservations'
)
BEGIN
    ALTER TABLE [commerce].[InventoryReservations] ADD [ConsumedLengthPerUnit] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505174022_AddStorefrontPoMjeriReservations'
)
BEGIN
    ALTER TABLE [commerce].[InventoryReservations] ADD [WebOrderItemId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505174022_AddStorefrontPoMjeriReservations'
)
BEGIN
    ALTER TABLE [commerce].[WebOrderItems] ADD [PoMjeri] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505174022_AddStorefrontPoMjeriReservations'
)
BEGIN
    CREATE INDEX [IX_InventoryReservations_WebOrderItemId] ON [commerce].[InventoryReservations] ([WebOrderItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505174022_AddStorefrontPoMjeriReservations'
)
BEGIN
    ALTER TABLE [commerce].[InventoryReservations] ADD CONSTRAINT [FK_InventoryReservations_WebOrderItems_WebOrderItemId] FOREIGN KEY ([WebOrderItemId]) REFERENCES [commerce].[WebOrderItems] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505174022_AddStorefrontPoMjeriReservations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505174022_AddStorefrontPoMjeriReservations', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601184708_AddProductImageMediaType'
)
BEGIN
    ALTER TABLE [commerce].[ProductImages] ADD [MediaType] nvarchar(20) NOT NULL DEFAULT N'image';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601184708_AddProductImageMediaType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260601184708_AddProductImageMediaType', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260609190000_BackfillLegacyProductionDefaults'
)
BEGIN
    UPDATE T
    SET T.OnlinePrice = T.Price
    FROM Tepisi AS T
    WHERE T.OnlinePrice IS NULL;

    UPDATE T
    SET T.BroaderCategory = N'Tepih'
    FROM Tepisi AS T
    WHERE T.BroaderCategory IS NULL
        OR LTRIM(RTRIM(T.BroaderCategory)) = N''
        OR T.BroaderCategory = N'default';

    UPDATE T
    SET T.NarrowerCategory = N'Tepih'
    FROM Tepisi AS T
    WHERE T.NarrowerCategory IS NULL
        OR LTRIM(RTRIM(T.NarrowerCategory)) = N''
        OR T.NarrowerCategory = N'default';

    UPDATE T
    SET T.ShortDescription = NULL
    FROM Tepisi AS T
    WHERE T.ShortDescription IS NOT NULL
        AND LTRIM(RTRIM(T.ShortDescription)) = N'';

    UPDATE T
    SET T.SeoTitle = NULL
    FROM Tepisi AS T
    WHERE T.SeoTitle IS NOT NULL
        AND LTRIM(RTRIM(T.SeoTitle)) = N'';

    UPDATE T
    SET T.SeoDescription = NULL
    FROM Tepisi AS T
    WHERE T.SeoDescription IS NOT NULL
        AND LTRIM(RTRIM(T.SeoDescription)) = N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260609190000_BackfillLegacyProductionDefaults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260609190000_BackfillLegacyProductionDefaults', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613130056_AddDirectSalePlaceholderSupport'
)
BEGIN
    ALTER TABLE [Tepisi] ADD [CreatedForDirectSale] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613130056_AddDirectSalePlaceholderSupport'
)
BEGIN
    DECLARE @var37 nvarchar(max);
    SELECT @var37 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prodaje]') AND [c].[name] = N'Price');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [Prodaje] DROP CONSTRAINT ' + @var37 + ';');
    ALTER TABLE [Prodaje] ALTER COLUMN [Price] decimal(18,4) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613130056_AddDirectSalePlaceholderSupport'
)
BEGIN
    ALTER TABLE [Prodaje] ADD [DirectSaleOriginalTotal] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260613130056_AddDirectSalePlaceholderSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260613130056_AddDirectSalePlaceholderSupport', N'10.0.9');
END;

COMMIT;
GO



IF SCHEMA_ID(N'commerce') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA commerce');
END
GO

IF OBJECT_ID(N'commerce.StorefrontCustomers', N'U') IS NULL
BEGIN
    CREATE TABLE commerce.StorefrontCustomers
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StorefrontCustomers PRIMARY KEY,
        Email NVARCHAR(254) NOT NULL,
        NormalizedEmail NVARCHAR(254) NOT NULL,
        FirstName NVARCHAR(50) NULL,
        LastName NVARCHAR(50) NULL,
        Phone NVARCHAR(30) NULL,
        AddressLine1 NVARCHAR(200) NULL,
        AddressLine2 NVARCHAR(200) NULL,
        City NVARCHAR(100) NULL,
        PostalCode NVARCHAR(20) NULL,
        Country NVARCHAR(100) NOT NULL CONSTRAINT DF_StorefrontCustomers_Country DEFAULT N'Crna Gora',
        CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_StorefrontCustomers_CreatedUtc DEFAULT SYSUTCDATETIME(),
        UpdatedUtc DATETIME2 NOT NULL CONSTRAINT DF_StorefrontCustomers_UpdatedUtc DEFAULT SYSUTCDATETIME(),
        LastLoginUtc DATETIME2 NULL,
        EmailVerifiedUtc DATETIME2 NULL,
        Disabled BIT NOT NULL CONSTRAINT DF_StorefrontCustomers_Disabled DEFAULT 0
    );

    CREATE UNIQUE INDEX IX_StorefrontCustomers_NormalizedEmail
        ON commerce.StorefrontCustomers (NormalizedEmail);
END
GO

IF OBJECT_ID(N'commerce.StorefrontLoginCodes', N'U') IS NULL
BEGIN
    CREATE TABLE commerce.StorefrontLoginCodes
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StorefrontLoginCodes PRIMARY KEY,
        Email NVARCHAR(254) NOT NULL,
        NormalizedEmail NVARCHAR(254) NOT NULL,
        Purpose NVARCHAR(20) NOT NULL,
        CodeHash NVARCHAR(128) NOT NULL,
        RememberMe BIT NOT NULL CONSTRAINT DF_StorefrontLoginCodes_RememberMe DEFAULT 0,
        FailedAttemptCount INT NOT NULL CONSTRAINT DF_StorefrontLoginCodes_FailedAttemptCount DEFAULT 0,
        CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_StorefrontLoginCodes_CreatedUtc DEFAULT SYSUTCDATETIME(),
        ExpiresUtc DATETIME2 NOT NULL,
        UsedUtc DATETIME2 NULL
    );

    CREATE INDEX IX_StorefrontLoginCodes_EmailPurposeState
        ON commerce.StorefrontLoginCodes (NormalizedEmail, Purpose, UsedUtc, ExpiresUtc);
END
GO

IF COL_LENGTH(N'commerce.WebOrders', N'StorefrontCustomerId') IS NULL
BEGIN
    ALTER TABLE commerce.WebOrders
        ADD StorefrontCustomerId INT NULL;
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_WebOrders_StorefrontCustomerId'
      AND object_id = OBJECT_ID(N'commerce.WebOrders')
)
BEGIN
    CREATE INDEX IX_WebOrders_StorefrontCustomerId
        ON commerce.WebOrders (StorefrontCustomerId);
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_WebOrders_StorefrontCustomers_StorefrontCustomerId'
)
BEGIN
    ALTER TABLE commerce.WebOrders
        ADD CONSTRAINT FK_WebOrders_StorefrontCustomers_StorefrontCustomerId
        FOREIGN KEY (StorefrontCustomerId)
        REFERENCES commerce.StorefrontCustomers (Id)
        ON DELETE SET NULL;
END
GO
