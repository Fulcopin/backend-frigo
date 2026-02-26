-- ✅ Migración: Agregar columna IsDraft a tabla Templates
-- Fecha: 2026-02-17
-- Objetivo: Permitir guardar plantillas como borradores (no publicadas)

USE [frigo_db]; -- Reemplaza con el nombre de tu base de datos
GO

-- 1. Verificar si la columna ya existe
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Templates]') 
    AND name = 'IsDraft'
)
BEGIN
    -- 2. Agregar columna IsDraft (por defecto false = publicado)
    ALTER TABLE [dbo].[Templates]
    ADD IsDraft BIT NOT NULL DEFAULT 0;
    
    PRINT '✅ Columna IsDraft agregada exitosamente a tabla Templates';
    PRINT '   - Valor por defecto: 0 (false = publicado)';
    PRINT '   - Tipo: BIT (booleano)';
END
ELSE
BEGIN
    PRINT '⚠️ La columna IsDraft ya existe en la tabla Templates';
END
GO

-- 3. Verificar el resultado
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Templates'
AND COLUMN_NAME = 'IsDraft';
GO

-- 4. Actualizar plantillas existentes (todas se marcan como publicadas)
UPDATE [dbo].[Templates]
SET IsDraft = 0
WHERE IsDraft IS NULL;

PRINT '✅ Migración completada';
PRINT '   - Todas las plantillas existentes marcadas como publicadas (IsDraft = 0)';
GO
