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
