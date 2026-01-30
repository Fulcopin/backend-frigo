-- =============================================
-- Migración: Agregar columna FechaVersion
-- Descripción: Agrega campo para la fecha de vigencia de una versión
-- Fecha: 28/12/2025
-- =============================================

USE [FormBuilder-rg];
GO

-- 1. Agregar columna FechaVersion a Templates
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'FechaVersion' 
    AND Object_ID = Object_ID(N'Templates')
)
BEGIN
    PRINT 'Agregando columna FechaVersion a Templates...'
    ALTER TABLE Templates
    ADD FechaVersion DATETIME2 NULL;
    PRINT 'Columna FechaVersion agregada a Templates ✓'
END
ELSE
BEGIN
    PRINT 'La columna FechaVersion ya existe en Templates'
END
GO

-- 2. Agregar columna FechaVersion a TemplateVersions
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'FechaVersion' 
    AND Object_ID = Object_ID(N'TemplateVersions')
)
BEGIN
    PRINT 'Agregando columna FechaVersion a TemplateVersions...'
    ALTER TABLE TemplateVersions
    ADD FechaVersion DATETIME2 NULL;
    PRINT 'Columna FechaVersion agregada a TemplateVersions ✓'
END
ELSE
BEGIN
    PRINT 'La columna FechaVersion ya existe en TemplateVersions'
END
GO

PRINT '✓ Migración completada exitosamente'
PRINT 'Las tablas Templates y TemplateVersions ahora tienen el campo FechaVersion'
GO
