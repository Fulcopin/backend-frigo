-- ============================================================
-- Migración: Crear tabla DocumentosManuales
-- Objetivo: digitalizar la LISTA MAESTRA DOCUMENTAL (FOR-SGC-3).
--           Además de los formularios del sistema, SGI carga a mano
--           los procedimientos, programas y manuales (PR-TH-1, …),
--           agrupados por área.
-- Lo consume /api/DocumentosManuales y la pantalla Documentos Registrados.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DocumentosManuales' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[DocumentosManuales] (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [Area]            NVARCHAR(100)     NOT NULL,
        [Nombre]          NVARCHAR(400)     NOT NULL,
        [Codigo]          NVARCHAR(100)     NULL,
        [Version]         NVARCHAR(50)      NULL,
        [Fecha]           DATETIME2         NULL,
        [CopiaControlada] NVARCHAR(10)      NOT NULL DEFAULT 'No',
        [Ubicacion]       NVARCHAR(300)     NULL,
        [Obsoleto]        BIT               NOT NULL DEFAULT 0,
        [Observaciones]   NVARCHAR(500)     NULL,
        [CreadoPor]       NVARCHAR(200)     NULL,
        [CreadoEn]        DATETIME2         NOT NULL DEFAULT GETDATE(),
        [ActualizadoEn]   DATETIME2         NULL,
        CONSTRAINT [PK_DocumentosManuales] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- La lista se pide siempre agrupada por área y ordenada por código.
    CREATE NONCLUSTERED INDEX [IX_DocumentosManuales_Area]
        ON [dbo].[DocumentosManuales] ([Area] ASC, [Codigo] ASC);

    PRINT 'Tabla DocumentosManuales creada.';
END
ELSE
BEGIN
    PRINT 'La tabla DocumentosManuales ya existe; no se hizo nada.';
END
GO
