-- ============================================================
-- Migración: Saldo de lotes + libro de movimientos (kardex)
-- Equivale a: 20260722190059_AddInventarioSaldoYMovimientos
-- Fecha: 2026-07-22
--
-- Necesaria para el DESCUENTO DE INVENTARIO desde los formularios
-- (POST /api/LotesInventario/consumir-cantidad).
-- Sin ella ese endpoint devuelve 500 con body vacío, que en el
-- navegador aparece disfrazado de error de CORS.
--
-- Es IDEMPOTENTE: se puede ejecutar varias veces sin romper nada.
-- Ejecutar sobre la BD de planta:  192.168.0.88\SQLEXPRESS → FormBuilder-rg
-- ============================================================

-- ── 1. Columna Saldo en LotesInventario ─────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[LotesInventario]') AND name = 'Saldo'
)
BEGIN
    ALTER TABLE [dbo].[LotesInventario]
        ADD [Saldo] DECIMAL(18,4) NOT NULL CONSTRAINT [DF_LotesInventario_Saldo] DEFAULT (0);

    PRINT '✅ Columna Saldo agregada a LotesInventario';
END
ELSE
    PRINT 'ℹ️ La columna Saldo ya existía';
GO

-- ── 2. Backfill: los lotes ya existentes conservan su saldo ──
--     Consumidos → 0 · el resto arranca con su Peso Neto disponible.
--     Solo toca las filas que quedaron en 0 para no pisar saldos ya usados.
UPDATE [dbo].[LotesInventario]
SET [Saldo] = CASE WHEN [Estado] = 'consumido' THEN 0 ELSE [PesoNeto] END
WHERE [Saldo] = 0 AND [Estado] <> 'consumido';
GO

-- ── 3. Tabla MovimientosInventario (kardex) ─────────────────
IF NOT EXISTS (
    SELECT 1 FROM sysobjects WHERE name = 'MovimientosInventario' AND xtype = 'U'
)
BEGIN
    CREATE TABLE [dbo].[MovimientosInventario] (
        [Id]               INT IDENTITY(1,1) NOT NULL,
        [LoteInventarioId] INT               NOT NULL,
        [NumeroLote]       NVARCHAR(100)     NOT NULL,
        [Tipo]             NVARCHAR(10)      NOT NULL,   -- entrada | salida
        [Cantidad]         DECIMAL(18,4)     NOT NULL,
        [SaldoResultante]  DECIMAL(18,4)     NOT NULL,
        [Proceso]          NVARCHAR(200)     NULL,
        [FormId]           INT               NULL,
        [Notas]            NVARCHAR(500)     NULL,
        [CreadoEn]         DATETIME2         NOT NULL,
        CONSTRAINT [PK_MovimientosInventario] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    PRINT '✅ Tabla MovimientosInventario creada';
END
ELSE
    PRINT 'ℹ️ La tabla MovimientosInventario ya existía';
GO

-- ── 4. Registrar la migración en el historial de EF ─────────
--     Así un futuro `dotnet ef database update` no intenta reaplicarla.
IF EXISTS (SELECT 1 FROM sysobjects WHERE name = '__EFMigrationsHistory' AND xtype = 'U')
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = '20260722190059_AddInventarioSaldoYMovimientos'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260722190059_AddInventarioSaldoYMovimientos', '9.0.9');

    PRINT '✅ Migración registrada en __EFMigrationsHistory';
END
GO

-- ── 5. Verificación ─────────────────────────────────────────
SELECT
    (SELECT COUNT(*) FROM sys.columns
      WHERE object_id = OBJECT_ID(N'[dbo].[LotesInventario]') AND name = 'Saldo')      AS ColumnaSaldo,
    (SELECT COUNT(*) FROM sysobjects
      WHERE name = 'MovimientosInventario' AND xtype = 'U')                            AS TablaMovimientos,
    (SELECT COUNT(*) FROM [dbo].[LotesInventario] WHERE [Saldo] > 0)                   AS LotesConSaldo;
GO
