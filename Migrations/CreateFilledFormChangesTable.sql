-- ============================================================
-- Migración: Crear tabla FilledFormChanges
-- Objetivo: saber QUÉ valor de un formulario ya guardado fue
--           modificado después, por quién y cuándo.
-- Se consulta desde GET /api/FilledForms/{id}/cambios y se muestra
-- en la pantalla VER a los roles Admin y Costos.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FilledFormChanges' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[FilledFormChanges] (
        [Id]             INT IDENTITY(1,1)  NOT NULL,
        [FormID]         INT                NOT NULL,
        [ChangedBy]      NVARCHAR(200)      NULL,
        [ChangedByEmail] NVARCHAR(200)      NULL,
        [ChangedByRole]  NVARCHAR(100)      NULL,
        [ChangedAt]      DATETIME2          NOT NULL DEFAULT GETDATE(),
        [UpdatedAt]      DATETIME2          NOT NULL DEFAULT GETDATE(),
        [Cambios]        NVARCHAR(MAX)      NULL,
        [TotalCambios]   INT                NOT NULL DEFAULT 0,
        CONSTRAINT [PK_FilledFormChanges] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_FilledFormChanges_FilledForms] FOREIGN KEY ([FormID])
            REFERENCES [dbo].[FilledForms] ([FormID]) ON DELETE CASCADE
    );

    -- La consulta habitual es "todos los cambios de este formulario, del más
    -- nuevo al más viejo".
    CREATE NONCLUSTERED INDEX [IX_FilledFormChanges_FormID]
        ON [dbo].[FilledFormChanges] ([FormID] ASC, [ChangedAt] DESC);

    PRINT 'Tabla FilledFormChanges creada.';
END
ELSE
BEGIN
    PRINT 'La tabla FilledFormChanges ya existe; no se hizo nada.';
END
GO
