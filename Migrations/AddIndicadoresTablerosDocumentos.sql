-- ============================================================
-- Migración: tablas del tablero de Indicadores + documentos manuales
-- Equivale a: 20260709175130_AddIndicadores
--             20260716180237_AddTablerosIndicadores
--             20260804205308_PendingModelChanges
--
-- Síntoma que arregla: GET /api/Indicadores, /api/Tableros y
-- /api/DocumentosManuales devuelven 500 mientras /api/Templates,
-- /api/FilledForms y /api/LotesInventario responden 200 — es decir,
-- la conexión a la BD está bien y lo que falta son estas tablas.
--
-- Es IDEMPOTENTE: se puede ejecutar varias veces sin romper nada.
-- Ejecutar sobre la BD de planta:  .\SQLEXPRESS → FormBuilder-rg
-- ============================================================

-- ── 0. ¿Estamos en la base correcta? ────────────────────────
--     La base que usa la API tiene Templates y FilledForms. Si no
--     están, estás conectado a otra base y crear aquí las tablas
--     no arregla nada (ya pasó una vez).
PRINT '── Conectado a: ' + @@SERVERNAME + ' → ' + DB_NAME();

IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name = 'FilledForms' AND xtype = 'U')
   OR NOT EXISTS (SELECT 1 FROM sysobjects WHERE name = 'Templates' AND xtype = 'U')
BEGIN
    RAISERROR('❌ Esta base no tiene Templates/FilledForms: NO es la base que usa la API. Revisa la cadena de conexión en appsettings.json del servidor y vuelve a ejecutar.', 16, 1);
    SET NOEXEC ON;   -- aborta el resto del script sin ejecutar nada
END
GO

-- ── 1. Tabla Indicadores ────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sysobjects WHERE name = 'Indicadores' AND xtype = 'U'
)
BEGIN
    CREATE TABLE [dbo].[Indicadores] (
        [Id]         INT IDENTITY(1,1) NOT NULL,
        [Titulo]     NVARCHAR(MAX)     NOT NULL,
        [ConfigJson] NVARCHAR(MAX)     NOT NULL,
        [CreadoPor]  NVARCHAR(MAX)     NULL,
        [Orden]      INT               NOT NULL CONSTRAINT [DF_Indicadores_Orden] DEFAULT (0),
        [CreadoEn]   DATETIME2         NOT NULL,
        CONSTRAINT [PK_Indicadores] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    PRINT '✅ Tabla Indicadores creada';
END
ELSE
    PRINT 'ℹ️ La tabla Indicadores ya existía';
GO

-- ── 2. Tabla Tableros (pestañas del tablero) ────────────────
IF NOT EXISTS (
    SELECT 1 FROM sysobjects WHERE name = 'Tableros' AND xtype = 'U'
)
BEGIN
    CREATE TABLE [dbo].[Tableros] (
        [Id]         INT IDENTITY(1,1) NOT NULL,
        [Nombre]     NVARCHAR(MAX)     NOT NULL,
        [ScopeTipo]  NVARCHAR(20)      NOT NULL,   -- todos | registro | proceso
        [ScopeValor] NVARCHAR(MAX)     NULL,
        [Orden]      INT               NOT NULL,
        [CreadoPor]  NVARCHAR(MAX)     NULL,
        [CreadoEn]   DATETIME2         NOT NULL,
        CONSTRAINT [PK_Tableros] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    PRINT '✅ Tabla Tableros creada';
END
ELSE
    PRINT 'ℹ️ La tabla Tableros ya existía';
GO

-- ── 3. Columna TableroId en Indicadores ─────────────────────
--     NULL = indicador creado antes de existir las pestañas.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Indicadores]') AND name = 'TableroId'
)
BEGIN
    ALTER TABLE [dbo].[Indicadores] ADD [TableroId] INT NULL;

    PRINT '✅ Columna TableroId agregada a Indicadores';
END
ELSE
    PRINT 'ℹ️ La columna TableroId ya existía';
GO

-- ── 4. Tabla DocumentosManuales ─────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sysobjects WHERE name = 'DocumentosManuales' AND xtype = 'U'
)
BEGIN
    CREATE TABLE [dbo].[DocumentosManuales] (
        [Id]               INT IDENTITY(1,1) NOT NULL,
        [Area]             NVARCHAR(100)     NOT NULL,
        [Nombre]           NVARCHAR(400)     NOT NULL,
        [Codigo]           NVARCHAR(100)     NULL,
        [Version]          NVARCHAR(50)      NULL,
        [Fecha]            DATETIME2         NULL,
        [CopiaControlada]  NVARCHAR(10)      NOT NULL,
        [Ubicacion]        NVARCHAR(300)     NULL,
        [Obsoleto]         BIT               NOT NULL,
        [Observaciones]    NVARCHAR(500)     NULL,
        [CreadoPor]        NVARCHAR(200)     NULL,
        [CreadoEn]         DATETIME2         NOT NULL,
        [ActualizadoEn]    DATETIME2         NULL,
        CONSTRAINT [PK_DocumentosManuales] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    PRINT '✅ Tabla DocumentosManuales creada';
END
ELSE
    PRINT 'ℹ️ La tabla DocumentosManuales ya existía';
GO

-- ── 5. Tabla FilledFormChanges (qué cambió, quién y cuándo) ──
--     Va en la misma migración PendingModelChanges, así que si falta
--     DocumentosManuales lo más probable es que también falte esta.
IF NOT EXISTS (
    SELECT 1 FROM sysobjects WHERE name = 'FilledFormChanges' AND xtype = 'U'
)
BEGIN
    CREATE TABLE [dbo].[FilledFormChanges] (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [FormID]          INT               NOT NULL,
        [ChangedBy]       NVARCHAR(200)     NULL,
        [ChangedByEmail]  NVARCHAR(200)     NULL,
        [ChangedByRole]   NVARCHAR(100)     NULL,
        [ChangedAt]       DATETIME2         NOT NULL,
        [UpdatedAt]       DATETIME2         NOT NULL,
        [Cambios]         NVARCHAR(MAX)     NULL,
        [TotalCambios]    INT               NOT NULL,
        CONSTRAINT [PK_FilledFormChanges] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_FilledFormChanges_FilledForms_FormID] FOREIGN KEY ([FormID])
            REFERENCES [dbo].[FilledForms] ([FormID]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_FilledFormChanges_FormID] ON [dbo].[FilledFormChanges] ([FormID]);

    PRINT '✅ Tabla FilledFormChanges creada';
END
ELSE
    PRINT 'ℹ️ La tabla FilledFormChanges ya existía';
GO

-- ── 6. Registrar las migraciones en el historial de EF ──────
--     Así un futuro `dotnet ef database update` no intenta reaplicarlas.
IF EXISTS (SELECT 1 FROM sysobjects WHERE name = '__EFMigrationsHistory' AND xtype = 'U')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    SELECT m.[MigrationId], '9.0.9'
    FROM (VALUES
        ('20260709175130_AddIndicadores'),
        ('20260716180237_AddTablerosIndicadores'),
        ('20260804205308_PendingModelChanges')
    ) AS m([MigrationId])
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[__EFMigrationsHistory] h
        WHERE h.[MigrationId] = m.[MigrationId]
    );

    PRINT '✅ Migraciones registradas en __EFMigrationsHistory';
END
ELSE
    PRINT 'ℹ️ No existe __EFMigrationsHistory (BD creada a mano): nada que registrar';
GO

-- ── 7. Verificación ─────────────────────────────────────────
SELECT
    @@SERVERNAME                                                                        AS Servidor,
    DB_NAME()                                                                           AS BaseDeDatos,
    (SELECT COUNT(*) FROM sysobjects WHERE name = 'Indicadores'        AND xtype = 'U') AS TablaIndicadores,
    (SELECT COUNT(*) FROM sysobjects WHERE name = 'Tableros'           AND xtype = 'U') AS TablaTableros,
    (SELECT COUNT(*) FROM sysobjects WHERE name = 'DocumentosManuales' AND xtype = 'U') AS TablaDocumentos,
    (SELECT COUNT(*) FROM sysobjects WHERE name = 'FilledFormChanges'  AND xtype = 'U') AS TablaCambios,
    (SELECT COUNT(*) FROM sys.columns
      WHERE object_id = OBJECT_ID(N'[dbo].[Indicadores]') AND name = 'TableroId')       AS ColumnaTableroId;
GO

SET NOEXEC OFF;
GO
