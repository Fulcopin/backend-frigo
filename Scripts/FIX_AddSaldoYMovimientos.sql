/* ============================================================================
   FIX: aplica manualmente la migración 20260722190059_AddInventarioSaldoYMovimientos
   Úsalo SOLO si no puedes correr `dotnet ef database update` en el servidor.
   Es idempotente: se puede correr varias veces sin romper nada.
   Ejecutar en SSMS contra la base de datos que usa el servidor 192.168.0.88:8096.
   ============================================================================ */

-- 1) Agregar columna Saldo a LotesInventario si no existe
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'Saldo' AND Object_ID = Object_ID(N'dbo.LotesInventario')
)
BEGIN
    ALTER TABLE dbo.LotesInventario ADD Saldo decimal(18,4) NOT NULL CONSTRAINT DF_LotesInventario_Saldo DEFAULT 0;
    PRINT 'Columna Saldo agregada.';

    -- Backfill: consumidos = 0, el resto arranca con su PesoNeto disponible
    EXEC(N'UPDATE dbo.LotesInventario SET Saldo = CASE WHEN Estado = ''consumido'' THEN 0 ELSE PesoNeto END;');
    PRINT 'Saldo inicial calculado.';
END
ELSE
    PRINT 'Columna Saldo ya existe. Se omite.';

-- 2) Crear tabla MovimientosInventario si no existe
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'MovimientosInventario')
BEGIN
    CREATE TABLE dbo.MovimientosInventario (
        Id               int IDENTITY(1,1) NOT NULL,
        LoteInventarioId int            NOT NULL,
        NumeroLote       nvarchar(100)  NOT NULL,
        Tipo             nvarchar(10)   NOT NULL,
        Cantidad         decimal(18,4)  NOT NULL,
        SaldoResultante  decimal(18,4)  NOT NULL,
        Proceso          nvarchar(200)  NULL,
        FormId           int            NULL,
        Notas            nvarchar(500)  NULL,
        CreadoEn         datetime2      NOT NULL,
        CONSTRAINT PK_MovimientosInventario PRIMARY KEY (Id)
    );
    PRINT 'Tabla MovimientosInventario creada.';
END
ELSE
    PRINT 'Tabla MovimientosInventario ya existe. Se omite.';

-- 3) Registrar la migración en el historial de EF (para que `dotnet ef` no la re-aplique)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'__EFMigrationsHistory')
   AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260722190059_AddInventarioSaldoYMovimientos')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260722190059_AddInventarioSaldoYMovimientos', N'9.0.9');
    PRINT 'Migración registrada en __EFMigrationsHistory.';
END
GO
