-- ============================================================
-- Migración: Crear tabla CostosProducto
-- Fecha: 2026-08-10
-- Para qué: el inventario de lotes solo guarda libras. Esta tabla
--           lleva el costo unitario por producto que carga el área
--           de Costos, y es la que valoriza el kardex.
-- Clave real: el NOMBRE del producto (el mismo del inventario).
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CostosProducto' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[CostosProducto] (
        [Id]             INT IDENTITY(1,1)  NOT NULL,
        [Producto]       NVARCHAR(200)      NOT NULL,
        [CostoUnitario]  DECIMAL(18,4)      NOT NULL DEFAULT 0,
        [Moneda]         NVARCHAR(10)       NOT NULL DEFAULT 'USD',
        [Unidad]         NVARCHAR(20)       NOT NULL DEFAULT 'Lb',
        [Notas]          NVARCHAR(500)      NULL,
        [ActualizadoPor] NVARCHAR(150)      NULL,
        [CreadoEn]       DATETIME2          NOT NULL DEFAULT GETUTCDATE(),
        [ActualizadoEn]  DATETIME2          NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_CostosProducto] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Un solo costo por producto: el upsert del controlador busca por nombre.
    CREATE UNIQUE INDEX [UX_CostosProducto_Producto]
        ON [dbo].[CostosProducto] ([Producto] ASC);

    PRINT 'Tabla CostosProducto creada.';
END
ELSE
BEGIN
    PRINT 'La tabla CostosProducto ya existe, no se hizo nada.';
END
GO

-- Índice sobre los movimientos para que el kardex global por fecha no
-- recorra toda la tabla. Idempotente: solo se crea si falta.
IF EXISTS (SELECT * FROM sysobjects WHERE name='MovimientosInventario' AND xtype='U')
   AND NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name='IX_MovimientosInventario_CreadoEn'
                     AND object_id = OBJECT_ID('dbo.MovimientosInventario'))
BEGIN
    CREATE INDEX [IX_MovimientosInventario_CreadoEn]
        ON [dbo].[MovimientosInventario] ([CreadoEn] ASC);

    PRINT 'Índice IX_MovimientosInventario_CreadoEn creado.';
END
GO
