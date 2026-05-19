-- ============================================================
-- Migración: Crear tabla LotesInventario
-- Fecha: 2026-05-18
-- Regla: PesoNeto = PesoEntrada - Desperdicio
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LotesInventario' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[LotesInventario] (
        [Id]              INT IDENTITY(1,1)     NOT NULL,
        [NumeroLote]      NVARCHAR(100)         NOT NULL,
        [Proceso]         NVARCHAR(200)         NOT NULL,
        [Producto]        NVARCHAR(200)         NULL,
        [Clasificacion]   NVARCHAR(100)         NULL,
        [PesoEntrada]     DECIMAL(18,4)         NOT NULL DEFAULT 0,
        [Desperdicio]     DECIMAL(18,4)         NOT NULL DEFAULT 0,
        [TipoDesperdicio] NVARCHAR(100)         NULL,
        [PesoNeto]        DECIMAL(18,4)         NOT NULL DEFAULT 0,
        [Estado]          NVARCHAR(20)          NOT NULL DEFAULT 'disponible',
        [LotePadre]       NVARCHAR(100)         NULL,
        [FormId]          INT                   NULL,
        [TemplateId]      NVARCHAR(50)          NULL,
        [Fecha]           DATE                  NULL,
        [Notas]           NVARCHAR(500)         NULL,
        [CreadoEn]        DATETIME2             NOT NULL DEFAULT GETUTCDATE(),
        [ActualizadoEn]   DATETIME2             NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_LotesInventario] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Índices para consultas frecuentes
    CREATE INDEX [IX_LotesInventario_NumeroLote]  ON [dbo].[LotesInventario] ([NumeroLote]);
    CREATE INDEX [IX_LotesInventario_LotePadre]   ON [dbo].[LotesInventario] ([LotePadre]);
    CREATE INDEX [IX_LotesInventario_Estado]       ON [dbo].[LotesInventario] ([Estado]);
    CREATE INDEX [IX_LotesInventario_FormId]       ON [dbo].[LotesInventario] ([FormId]);

    PRINT 'Tabla LotesInventario creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La tabla LotesInventario ya existe, saltando creación.';
END
GO
