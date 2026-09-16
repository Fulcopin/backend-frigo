-- ============================================================
-- Migración: Crear tabla ConsultasPlan
-- Fecha: 2026-08-26
-- Para qué: las consultas fijas del Comparativo Plan (📌 de la matriz
--           actividad × columna) vivían en el localStorage de cada
--           navegador: lo que configuraba una persona no lo veía nadie
--           más y se perdía al cambiar de computadora. Acá quedan en la
--           base, para toda la planta.
-- Clave real: Actividad + Grupo (bloque) + Clave (columna).
--           «Empaque» de camarón no sale del mismo formulario que el de
--           pescado, por eso el bloque es parte de la clave.
--           Actividad vacía = consulta genérica de esa columna.
--           Grupo vacío     = sin bloque (lo configurado antes de separar
--                             pescado y camarón; sirve para los dos).
-- Idempotente: se puede correr varias veces sin romper nada.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ConsultasPlan' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[ConsultasPlan] (
        [Id]             INT IDENTITY(1,1)  NOT NULL,
        [Actividad]      NVARCHAR(200)      NOT NULL DEFAULT '',
        [Grupo]          NVARCHAR(20)       NOT NULL DEFAULT '',
        [Clave]          NVARCHAR(60)       NOT NULL,
        [Receta]         NVARCHAR(MAX)      NOT NULL,
        [ActualizadoPor] NVARCHAR(200)      NULL,
        [CreatedAt]      DATETIME2          NOT NULL DEFAULT GETDATE(),
        [UpdatedAt]      DATETIME2          NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_ConsultasPlan] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Una sola consulta por cruce: el upsert del controlador busca por estos tres.
    CREATE UNIQUE INDEX [UX_ConsultasPlan_Cruce]
        ON [dbo].[ConsultasPlan] ([Actividad] ASC, [Grupo] ASC, [Clave] ASC);

    PRINT 'Tabla ConsultasPlan creada.';
END
ELSE
BEGIN
    PRINT 'La tabla ConsultasPlan ya existe, no se hizo nada.';
END
GO
