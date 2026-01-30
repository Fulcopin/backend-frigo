-- =====================================================
-- Script de Migracion: Agregar Campo FechaVersion
-- Backend: backend-frigo
-- Fecha: 2025-12-26
-- Descripcion: Agrega el campo FechaVersion a FilledForms
-- =====================================================

-- NOTA: Los campos TemplateVersion y TemplateSnapshot ya existen en este backend
-- Solo necesitamos agregar FechaVersion

-- NOTA: Quitamos USE porque Azure SQL no lo permite
-- La base de datos ya esta seleccionada en la cadena de conexion

-- Verificar si la columna ya existe antes de agregarla
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FilledForms]') AND name = 'FechaVersion')
BEGIN
    PRINT 'Agregando columna FechaVersion...';
    ALTER TABLE [dbo].[FilledForms]
    ADD [FechaVersion] DATETIME2 NULL;
    PRINT 'FechaVersion agregada correctamente';
END
ELSE
BEGIN
    PRINT 'La columna FechaVersion ya existe, omitiendo...';
END

-- =====================================================
-- Actualizar registros existentes
-- =====================================================

PRINT 'Actualizando registros existentes...';

-- Para formularios existentes sin FechaVersion, usar CreatedAt como fecha de version
UPDATE [dbo].[FilledForms]
SET FechaVersion = CreatedAt
WHERE FechaVersion IS NULL AND TemplateVersion IS NOT NULL;

PRINT 'Registros actualizados con fechas de version';

-- =====================================================
-- Verificacion Final
-- =====================================================

PRINT 'Verificando instalacion...';

-- Mostrar estructura de los campos de versionamiento
SELECT 
    COLUMN_NAME as Columna,
    DATA_TYPE as Tipo,
    IS_NULLABLE as Nullable,
    CHARACTER_MAXIMUM_LENGTH as Longitud
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FilledForms'
    AND COLUMN_NAME IN ('TemplateVersion', 'TemplateSnapshot', 'FechaVersion')
ORDER BY ORDINAL_POSITION;

PRINT 'Estadisticas de versiones:';

-- Contar formularios por version
SELECT 
    TemplateVersion as Version,
    COUNT(*) as CantidadFormularios,
    MIN(CreatedAt) as PrimerUso,
    MAX(CreatedAt) as UltimoUso,
    MIN(FechaVersion) as FechaVersionMin,
    MAX(FechaVersion) as FechaVersionMax
FROM [dbo].[FilledForms]
WHERE TemplateVersion IS NOT NULL
GROUP BY TemplateVersion
ORDER BY TemplateVersion DESC;

PRINT 'Migracion completada exitosamente!';
PRINT 'Notas:';
PRINT '   - FechaVersion agregada correctamente';
PRINT '   - Los formularios existentes se actualizaron con CreatedAt como FechaVersion';
PRINT '   - Los nuevos formularios guardaran automaticamente FechaVersion';
