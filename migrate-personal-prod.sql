-- ============================================================================
-- Migración dirigida del módulo de Personal para la base de datos de PRODUCCIÓN
-- ============================================================================
-- A diferencia de un script generado con `dotnet ef migrations script`, este
-- script NO asume el historial completo de migraciones: verifica el estado
-- real del esquema (sys.tables / sys.columns) antes de crear o alterar nada.
-- Es seguro ejecutarlo aunque la base de datos ya tenga las tablas base
-- (Templates, FilledForms, etc.) creadas por fuera de __EFMigrationsHistory.
--
-- Qué hace:
--   1. Crea [PersonalRegistros] si no existe (migración AddPersonalModule).
--      La FK hacia [FilledForms] solo se agrega si esa tabla existe con ese
--      nombre exacto; si no, se crea la tabla igual (sin la FK) y se avisa
--      con PRINT, para no bloquear el resto del script.
--   2. Crea [ProcesoEstandares] si no existe (migración AddPersonalModule).
--   3. Agrega [FormularioNombre] y [TemplateID] a [ProcesoEstandares] si faltan
--      (migración AddTemplateToProcesoEstandar), con su índice y FK hacia
--      [Templates] (misma protección: solo si esa tabla existe).
--   4. Si existe [__EFMigrationsHistory], registra ambas migraciones para que
--      futuros `dotnet ef database update` no intenten reaplicarlas.
--
-- Diagnóstico: si algo falla, primero corre por separado:
--   SELECT s.name AS esquema, t.name AS tabla FROM sys.tables t
--   JOIN sys.schemas s ON s.schema_id = t.schema_id
--   WHERE t.name LIKE '%Form%' OR t.name LIKE '%Template%';
-- para confirmar el esquema/nombre real de FilledForms y Templates en esta BD.
-- ============================================================================

SET NOCOUNT ON;

-- Diagnóstico rápido: nombre y esquema reales de las tablas relacionadas.
SELECT s.name AS esquema, t.name AS tabla
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.name IN (N'FilledForms', N'Templates', N'PersonalRegistros', N'ProcesoEstandares');

BEGIN TRANSACTION;

-- ── 1) Tabla PersonalRegistros ─────────────────────────────────────────────
IF OBJECT_ID(N'[dbo].[PersonalRegistros]', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[FilledForms]', N'U') IS NOT NULL
    BEGIN
        CREATE TABLE [PersonalRegistros] (
            [Id] int NOT NULL IDENTITY,
            [FormID] int NOT NULL,
            [TemplateId] nvarchar(50) NULL,
            [Proceso] nvarchar(200) NOT NULL,
            [Fecha] date NOT NULL,
            [PersonalPlanta] int NOT NULL,
            [PersonalExterno] int NOT NULL,
            [HoraDesde] time NOT NULL,
            [HoraHasta] time NOT NULL,
            [HorasTrabajadas] decimal(6,2) NOT NULL,
            [Observaciones] nvarchar(max) NULL,
            [CumpleEstandar] bit NOT NULL,
            [MotivoIncumplimiento] nvarchar(500) NULL,
            [JustificacionVariacion] nvarchar(1000) NULL,
            [CreadoPor] nvarchar(200) NULL,
            [CreadoEn] datetime2 NOT NULL,
            [ActualizadoEn] datetime2 NULL,
            CONSTRAINT [PK_PersonalRegistros] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_PersonalRegistros_FilledForms_FormID] FOREIGN KEY ([FormID]) REFERENCES [FilledForms] ([FormID]) ON DELETE CASCADE
        );
        PRINT 'Creada tabla PersonalRegistros (con FK a FilledForms).';
    END
    ELSE
    BEGIN
        CREATE TABLE [PersonalRegistros] (
            [Id] int NOT NULL IDENTITY,
            [FormID] int NOT NULL,
            [TemplateId] nvarchar(50) NULL,
            [Proceso] nvarchar(200) NOT NULL,
            [Fecha] date NOT NULL,
            [PersonalPlanta] int NOT NULL,
            [PersonalExterno] int NOT NULL,
            [HoraDesde] time NOT NULL,
            [HoraHasta] time NOT NULL,
            [HorasTrabajadas] decimal(6,2) NOT NULL,
            [Observaciones] nvarchar(max) NULL,
            [CumpleEstandar] bit NOT NULL,
            [MotivoIncumplimiento] nvarchar(500) NULL,
            [JustificacionVariacion] nvarchar(1000) NULL,
            [CreadoPor] nvarchar(200) NULL,
            [CreadoEn] datetime2 NOT NULL,
            [ActualizadoEn] datetime2 NULL,
            CONSTRAINT [PK_PersonalRegistros] PRIMARY KEY ([Id])
        );
        PRINT 'ADVERTENCIA: no se encontró la tabla [dbo].[FilledForms]; PersonalRegistros se creó SIN la FK. Revisa el nombre/esquema real con la consulta de diagnóstico y agrega la FK manualmente si corresponde.';
    END;

    CREATE INDEX [IX_PersonalRegistros_FormID] ON [PersonalRegistros] ([FormID]);
END
ELSE
BEGIN
    PRINT 'PersonalRegistros ya existe, se omite.';
END;

-- ── 2) Tabla ProcesoEstandares ──────────────────────────────────────────────
IF OBJECT_ID(N'[dbo].[ProcesoEstandares]', N'U') IS NULL
BEGIN
    CREATE TABLE [ProcesoEstandares] (
        [Id] int NOT NULL IDENTITY,
        [Proceso] nvarchar(200) NOT NULL,
        [HoraInicioEsperada] time NULL,
        [HoraFinEsperada] time NULL,
        [DuracionMinimaHoras] decimal(6,2) NULL,
        [DuracionMaximaHoras] decimal(6,2) NULL,
        [ToleranciaMinutos] int NOT NULL,
        [Activo] bit NOT NULL,
        [CreadoEn] datetime2 NOT NULL,
        [ActualizadoEn] datetime2 NULL,
        CONSTRAINT [PK_ProcesoEstandares] PRIMARY KEY ([Id])
    );

    PRINT 'Creada tabla ProcesoEstandares.';
END
ELSE
BEGIN
    PRINT 'ProcesoEstandares ya existe, se omite.';
END;

-- ── 3) Columnas FormularioNombre / TemplateID en ProcesoEstandares ────────
IF COL_LENGTH(N'[dbo].[ProcesoEstandares]', N'FormularioNombre') IS NULL
BEGIN
    ALTER TABLE [ProcesoEstandares] ADD [FormularioNombre] nvarchar(255) NULL;
    PRINT 'Agregada columna FormularioNombre.';
END;

IF COL_LENGTH(N'[dbo].[ProcesoEstandares]', N'TemplateID') IS NULL
BEGIN
    ALTER TABLE [ProcesoEstandares] ADD [TemplateID] int NULL;
    PRINT 'Agregada columna TemplateID.';
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ProcesoEstandares_TemplateID' AND object_id = OBJECT_ID(N'[dbo].[ProcesoEstandares]')
)
BEGIN
    CREATE INDEX [IX_ProcesoEstandares_TemplateID] ON [ProcesoEstandares] ([TemplateID]);
    PRINT 'Creado índice IX_ProcesoEstandares_TemplateID.';
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProcesoEstandares_Templates_TemplateID'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[Templates]', N'U') IS NOT NULL
    BEGIN
        ALTER TABLE [ProcesoEstandares]
            ADD CONSTRAINT [FK_ProcesoEstandares_Templates_TemplateID]
            FOREIGN KEY ([TemplateID]) REFERENCES [Templates] ([TemplateID]);
        PRINT 'Creada FK FK_ProcesoEstandares_Templates_TemplateID.';
    END
    ELSE
    BEGIN
        PRINT 'ADVERTENCIA: no se encontró la tabla [dbo].[Templates]; no se creó la FK de ProcesoEstandares hacia Templates.';
    END;
END;

-- ── 4) Registrar migraciones en __EFMigrationsHistory (si esa tabla existe) ─
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260904233047_AddPersonalModule')
        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260904233047_AddPersonalModule', N'9.0.9');

    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260905005335_AddTemplateToProcesoEstandar')
        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260905005335_AddTemplateToProcesoEstandar', N'9.0.9');

    PRINT 'Historial de migraciones actualizado.';
END
ELSE
BEGIN
    PRINT 'No existe __EFMigrationsHistory en esta base de datos; se omite el registro de historial.';
END;

COMMIT;
PRINT 'Migración de Personal completada correctamente.';

