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
    WHERE [MigrationId] = N'20251014044034_InitialCreate'
)
BEGIN
    CREATE TABLE [Templates] (
        [TemplateID] int NOT NULL IDENTITY,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(255) NOT NULL,
        [Version] nvarchar(20) NOT NULL,
        [Objetivo] nvarchar(max) NULL,
        [Proceso] nvarchar(max) NULL,
        [CuandoSeUsa] nvarchar(max) NULL,
        [QuienLoLlena] nvarchar(max) NULL,
        [HeaderFields] nvarchar(max) NULL,
        [TableColumns] nvarchar(max) NULL,
        [Firmas] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Templates] PRIMARY KEY ([TemplateID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251014044034_InitialCreate'
)
BEGIN
    CREATE TABLE [FilledForms] (
        [FormID] int NOT NULL IDENTITY,
        [TemplateID] int NOT NULL,
        [HeaderData] nvarchar(max) NULL,
        [FirmasData] nvarchar(max) NULL,
        [Observaciones] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_FilledForms] PRIMARY KEY ([FormID]),
        CONSTRAINT [FK_FilledForms_Templates_TemplateID] FOREIGN KEY ([TemplateID]) REFERENCES [Templates] ([TemplateID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251014044034_InitialCreate'
)
BEGIN
    CREATE TABLE [TableRows] (
        [RowID] int NOT NULL IDENTITY,
        [FormID] int NOT NULL,
        [RowData] nvarchar(max) NULL,
        CONSTRAINT [PK_TableRows] PRIMARY KEY ([RowID]),
        CONSTRAINT [FK_TableRows_FilledForms_FormID] FOREIGN KEY ([FormID]) REFERENCES [FilledForms] ([FormID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251014044034_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FilledForms_TemplateID] ON [FilledForms] ([TemplateID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251014044034_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TableRows_FormID] ON [TableRows] ([FormID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251014044034_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Templates_Codigo] ON [Templates] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251014044034_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251014044034_InitialCreate', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251015002759_AddFilledFormAndTableRowModels'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251015002759_AddFilledFormAndTableRowModels', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101013028_UpdatedTemplateAndFormModels'
)
BEGIN
    DROP TABLE [TableRows];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101013028_UpdatedTemplateAndFormModels'
)
BEGIN
    EXEC sp_rename N'[Templates].[TableColumns]', N'BodyElements', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101013028_UpdatedTemplateAndFormModels'
)
BEGIN
    ALTER TABLE [FilledForms] ADD [BodyData] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101013028_UpdatedTemplateAndFormModels'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251101013028_UpdatedTemplateAndFormModels', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216034641_AddTemplateVersioning'
)
BEGIN
    ALTER TABLE [FilledForms] ADD [TemplateSnapshot] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216034641_AddTemplateVersioning'
)
BEGIN
    ALTER TABLE [FilledForms] ADD [TemplateVersion] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216034641_AddTemplateVersioning'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251216034641_AddTemplateVersioning', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260103024403_AddIsMasterFormColumn'
)
BEGIN
    ALTER TABLE [Templates] ADD [IsMasterForm] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260103024403_AddIsMasterFormColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260103024403_AddIsMasterFormColumn', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Templates]') AND [c].[name] = N'IsMasterForm');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Templates] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [Templates] DROP COLUMN [IsMasterForm];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    ALTER TABLE [Templates] ADD [Area] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    ALTER TABLE [Templates] ADD [Frecuencia] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    CREATE TABLE [AlertConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [EnableMissingFormAlerts] bit NOT NULL,
        [DailyCheckTime] nvarchar(max) NOT NULL,
        [MissingFormRecipients] nvarchar(max) NOT NULL,
        [EnableSignatureAlerts] bit NOT NULL,
        [SignatureAlertDelay] int NOT NULL,
        [SignatureRecipients] nvarchar(max) NOT NULL,
        [SenderEmail] nvarchar(max) NOT NULL,
        [SenderName] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_AlertConfigurations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    CREATE TABLE [Alerts] (
        [Id] int NOT NULL IDENTITY,
        [Type] nvarchar(max) NOT NULL,
        [Priority] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [TargetEmail] nvarchar(max) NOT NULL,
        [FormId] int NULL,
        [FormCode] nvarchar(max) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Alerts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Alerts_FilledForms_FormId] FOREIGN KEY ([FormId]) REFERENCES [FilledForms] ([FormID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    CREATE TABLE [Signatures] (
        [Id] int NOT NULL IDENTITY,
        [FilledFormId] int NOT NULL,
        [SignatureImage] nvarchar(max) NOT NULL,
        [SignedBy] nvarchar(max) NOT NULL,
        [SignedDate] datetime2 NOT NULL,
        [Comments] nvarchar(max) NULL,
        [IsModifiedBySGI] bit NOT NULL,
        [OriginalSignedDate] datetime2 NULL,
        CONSTRAINT [PK_Signatures] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Signatures_FilledForms_FilledFormId] FOREIGN KEY ([FilledFormId]) REFERENCES [FilledForms] ([FormID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    CREATE INDEX [IX_Alerts_FormId] ON [Alerts] ([FormId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    CREATE INDEX [IX_Signatures_FilledFormId] ON [Signatures] ([FilledFormId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211210810_AddSignaturesAlertsModules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260211210810_AddSignaturesAlertsModules', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217214903_AddCreadoPorModificadoPorToFilledForm'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260217214903_AddCreadoPorModificadoPorToFilledForm', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217222117_AddFilledByAuditFields'
)
BEGIN
    ALTER TABLE [FilledForms] ADD [FilledBy] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217222117_AddFilledByAuditFields'
)
BEGIN
    ALTER TABLE [FilledForms] ADD [FilledByEmail] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217222117_AddFilledByAuditFields'
)
BEGIN
    ALTER TABLE [FilledForms] ADD [FilledByRole] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217222117_AddFilledByAuditFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260217222117_AddFilledByAuditFields', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218004810_AddIsDraftColumn'
)
BEGIN
    ALTER TABLE [Templates] ADD [IsDraft] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218004810_AddIsDraftColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260218004810_AddIsDraftColumn', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218044702_AgregarCatalogoFirmas'
)
BEGIN
    CREATE TABLE [CatalogoFirmas] (
        [CatalogoFirmaID] int NOT NULL IDENTITY,
        [Puesto] nvarchar(100) NOT NULL,
        [NombreCompleto] nvarchar(200) NULL,
        [Area] nvarchar(100) NULL,
        [Activo] bit NOT NULL,
        [FechaCreacion] datetime2 NOT NULL,
        CONSTRAINT [PK_CatalogoFirmas] PRIMARY KEY ([CatalogoFirmaID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218044702_AgregarCatalogoFirmas'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260218044702_AgregarCatalogoFirmas', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218173802_AgregarCorreoCatalogoFirmas'
)
BEGIN
    ALTER TABLE [CatalogoFirmas] ADD [Correo] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218173802_AgregarCorreoCatalogoFirmas'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260218173802_AgregarCorreoCatalogoFirmas', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219003225_AgregarTipoProducto'
)
BEGIN
    ALTER TABLE [FilledForms] ADD [TipoProducto] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219003225_AgregarTipoProducto'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260219003225_AgregarTipoProducto', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260225202154_AgregarFirmaImageUrlCatalogo'
)
BEGIN
    ALTER TABLE [CatalogoFirmas] ADD [FirmaImageUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260225202154_AgregarFirmaImageUrlCatalogo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260225202154_AgregarFirmaImageUrlCatalogo', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260225211439_AgregarFormDrafts'
)
BEGIN
    CREATE TABLE [FormDrafts] (
        [DraftID] int NOT NULL IDENTITY,
        [TemplateID] int NOT NULL,
        [TemplateName] nvarchar(300) NULL,
        [TemplateCodigo] nvarchar(50) NULL,
        [UserName] nvarchar(200) NULL,
        [UserEmail] nvarchar(200) NULL,
        [UserRole] nvarchar(100) NULL,
        [HeaderData] nvarchar(max) NULL,
        [BodyData] nvarchar(max) NULL,
        [FirmasData] nvarchar(max) NULL,
        [TemplateSnapshot] nvarchar(max) NULL,
        [Progress] int NOT NULL,
        [Nota] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_FormDrafts] PRIMARY KEY ([DraftID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260225211439_AgregarFormDrafts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260225211439_AgregarFormDrafts', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260225221544_AgregarUsaApi'
)
BEGIN
    ALTER TABLE [Templates] ADD [UsaApi] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260225221544_AgregarUsaApi'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260225221544_AgregarUsaApi', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226020020_ReemplazarObjetivoConSupervisa'
)
BEGIN
    EXEC sp_rename N'[TemplateVersions].[Objetivo]', N'Supervisa', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226020020_ReemplazarObjetivoConSupervisa'
)
BEGIN
    EXEC sp_rename N'[Templates].[Objetivo]', N'Supervisa', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226020020_ReemplazarObjetivoConSupervisa'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260226020020_ReemplazarObjetivoConSupervisa', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302174716_AddIsObsoleteToTemplate'
)
BEGIN
    ALTER TABLE [Templates] ADD [IsObsolete] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302174716_AddIsObsoleteToTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260302174716_AddIsObsoleteToTemplate', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302182731_AddAutoSumColumnsToTemplate'
)
BEGIN
    ALTER TABLE [Templates] ADD [AutoSumColumns] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302182731_AddAutoSumColumnsToTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260302182731_AddAutoSumColumnsToTemplate', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302191717_AddSignatureRejections'
)
BEGIN
    CREATE TABLE [SignatureRejections] (
        [Id] int NOT NULL IDENTITY,
        [FilledFormId] int NOT NULL,
        [RejectedBy] nvarchar(max) NOT NULL,
        [RejectedDate] datetime2 NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [Status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_SignatureRejections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SignatureRejections_FilledForms_FilledFormId] FOREIGN KEY ([FilledFormId]) REFERENCES [FilledForms] ([FormID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302191717_AddSignatureRejections'
)
BEGIN
    CREATE INDEX [IX_SignatureRejections_FilledFormId] ON [SignatureRejections] ([FilledFormId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302191717_AddSignatureRejections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260302191717_AddSignatureRejections', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408195000_AddTemplateChangeLogs'
)
BEGIN
    CREATE TABLE [TemplateChangeLogs] (
        [Id] int NOT NULL IDENTITY,
        [TemplateID] int NOT NULL,
        [Fecha] datetime2 NOT NULL,
        [Version] nvarchar(50) NOT NULL,
        [CambioRealizado] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TemplateChangeLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TemplateChangeLogs_Templates_TemplateID] FOREIGN KEY ([TemplateID]) REFERENCES [Templates] ([TemplateID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408195000_AddTemplateChangeLogs'
)
BEGIN
    CREATE INDEX [IX_TemplateChangeLogs_TemplateID] ON [TemplateChangeLogs] ([TemplateID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408195000_AddTemplateChangeLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408195000_AddTemplateChangeLogs', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515201947_AddLotesTrazabilidadTable'
)
BEGIN
    CREATE TABLE [LotesTrazabilidad] (
        [Id] int NOT NULL IDENTITY,
        [FormID] int NOT NULL,
        [LoteOrigen] nvarchar(100) NOT NULL,
        [LoteDestino] nvarchar(100) NULL,
        [Proceso] nvarchar(200) NOT NULL,
        [Producto] nvarchar(200) NULL,
        [CantidadEntrada] decimal(18,4) NOT NULL,
        [CantidadSalida] decimal(18,4) NOT NULL,
        [FechaRegistro] datetime2 NOT NULL,
        CONSTRAINT [PK_LotesTrazabilidad] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LotesTrazabilidad_FilledForms_FormID] FOREIGN KEY ([FormID]) REFERENCES [FilledForms] ([FormID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515201947_AddLotesTrazabilidadTable'
)
BEGIN
    CREATE INDEX [IX_LotesTrazabilidad_FormID] ON [LotesTrazabilidad] ([FormID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515201947_AddLotesTrazabilidadTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515201947_AddLotesTrazabilidadTable', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519050755_AddLotesInventarioTable'
)
BEGIN
    CREATE TABLE [LotesInventario] (
        [Id] int NOT NULL IDENTITY,
        [NumeroLote] nvarchar(100) NOT NULL,
        [Proceso] nvarchar(200) NOT NULL,
        [Producto] nvarchar(200) NULL,
        [Clasificacion] nvarchar(100) NULL,
        [PesoEntrada] decimal(18,4) NOT NULL,
        [Desperdicio] decimal(18,4) NOT NULL,
        [TipoDesperdicio] nvarchar(100) NULL,
        [PesoNeto] decimal(18,4) NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [LotePadre] nvarchar(100) NULL,
        [FormId] int NULL,
        [TemplateId] nvarchar(50) NULL,
        [Fecha] date NULL,
        [Notas] nvarchar(500) NULL,
        [CreadoEn] datetime2 NOT NULL,
        [ActualizadoEn] datetime2 NOT NULL,
        CONSTRAINT [PK_LotesInventario] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519050755_AddLotesInventarioTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519050755_AddLotesInventarioTable', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630031749_AddSummaryFrequencyDays'
)
BEGIN
    ALTER TABLE [AlertConfigurations] ADD [SummaryFrequencyDays] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630031749_AddSummaryFrequencyDays'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260630031749_AddSummaryFrequencyDays', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706043452_AddTemplateChangeAlertsConfig'
)
BEGIN
    ALTER TABLE [AlertConfigurations] ADD [EnableTemplateChangeAlerts] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706043452_AddTemplateChangeAlertsConfig'
)
BEGIN
    ALTER TABLE [AlertConfigurations] ADD [TemplateChangeRecipients] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706043452_AddTemplateChangeAlertsConfig'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260706043452_AddTemplateChangeAlertsConfig', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706044522_AddTicketsModule'
)
BEGIN
    CREATE TABLE [Tickets] (
        [Id] int NOT NULL IDENTITY,
        [Titulo] nvarchar(max) NOT NULL,
        [Descripcion] nvarchar(max) NOT NULL,
        [CreadoPorNombre] nvarchar(max) NOT NULL,
        [CreadoPorEmail] nvarchar(max) NOT NULL,
        [CreadoEn] datetime2 NOT NULL,
        [Estado] nvarchar(max) NOT NULL,
        [RespuestaAdmin] nvarchar(max) NULL,
        [RespondidoPor] nvarchar(max) NULL,
        [RespondidoEn] datetime2 NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706044522_AddTicketsModule'
)
BEGIN
    CREATE TABLE [TicketViewers] (
        [Id] int NOT NULL IDENTITY,
        [UserEmail] nvarchar(max) NOT NULL,
        [UserNombre] nvarchar(max) NOT NULL,
        [AgregadoEn] datetime2 NOT NULL,
        CONSTRAINT [PK_TicketViewers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706044522_AddTicketsModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260706044522_AddTicketsModule', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706165031_AddLockThresholdHours'
)
BEGIN
    ALTER TABLE [AlertConfigurations] ADD [LockThresholdHours] int NOT NULL DEFAULT 36;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706165031_AddLockThresholdHours'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260706165031_AddLockThresholdHours', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706205848_AddPinToCatalogoFirma'
)
BEGIN
    ALTER TABLE [CatalogoFirmas] ADD [PinHash] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706205848_AddPinToCatalogoFirma'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260706205848_AddPinToCatalogoFirma', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709175130_AddIndicadores'
)
BEGIN
    CREATE TABLE [Indicadores] (
        [Id] int NOT NULL IDENTITY,
        [Titulo] nvarchar(max) NOT NULL,
        [ConfigJson] nvarchar(max) NOT NULL,
        [CreadoPor] nvarchar(max) NULL,
        [Orden] int NOT NULL DEFAULT 0,
        [CreadoEn] datetime2 NOT NULL,
        CONSTRAINT [PK_Indicadores] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709175130_AddIndicadores'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709175130_AddIndicadores', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716180237_AddTablerosIndicadores'
)
BEGIN
    ALTER TABLE [Indicadores] ADD [TableroId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716180237_AddTablerosIndicadores'
)
BEGIN
    CREATE TABLE [Tableros] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(max) NOT NULL,
        [ScopeTipo] nvarchar(20) NOT NULL,
        [ScopeValor] nvarchar(max) NULL,
        [Orden] int NOT NULL,
        [CreadoPor] nvarchar(max) NULL,
        [CreadoEn] datetime2 NOT NULL,
        CONSTRAINT [PK_Tableros] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716180237_AddTablerosIndicadores'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716180237_AddTablerosIndicadores', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722190059_AddInventarioSaldoYMovimientos'
)
BEGIN
    ALTER TABLE [LotesInventario] ADD [Saldo] decimal(18,4) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722190059_AddInventarioSaldoYMovimientos'
)
BEGIN
    UPDATE LotesInventario SET Saldo = CASE WHEN Estado = 'consumido' THEN 0 ELSE PesoNeto END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722190059_AddInventarioSaldoYMovimientos'
)
BEGIN
    CREATE TABLE [MovimientosInventario] (
        [Id] int NOT NULL IDENTITY,
        [LoteInventarioId] int NOT NULL,
        [NumeroLote] nvarchar(100) NOT NULL,
        [Tipo] nvarchar(10) NOT NULL,
        [Cantidad] decimal(18,4) NOT NULL,
        [SaldoResultante] decimal(18,4) NOT NULL,
        [Proceso] nvarchar(200) NULL,
        [FormId] int NULL,
        [Notas] nvarchar(500) NULL,
        [CreadoEn] datetime2 NOT NULL,
        CONSTRAINT [PK_MovimientosInventario] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722190059_AddInventarioSaldoYMovimientos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722190059_AddInventarioSaldoYMovimientos', N'9.0.9');
END;

COMMIT;
GO

