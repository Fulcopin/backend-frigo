-- ============================================
-- Migración: AddTemplateVersioning
-- Descripción: Agregar campos para versionamiento de templates
-- Fecha: 2025-12-15
-- ============================================

-- Agregar columna TemplateSnapshot (almacena snapshot completo del template)
ALTER TABLE [FilledForms] 
ADD [TemplateSnapshot] nvarchar(max) NULL;

-- Agregar columna TemplateVersion (almacena la versión específica del template)
ALTER TABLE [FilledForms] 
ADD [TemplateVersion] nvarchar(20) NULL;

-- Registrar la migración
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) 
VALUES (N'AddTemplateVersioning', N'9.0.9');

-- ============================================
-- Notas:
-- - TemplateSnapshot: JSON completo del template al momento de crear el formulario
-- - TemplateVersion: Versión del template (ej: "1.0", "2.0")
-- - Estos campos permiten que los formularios antiguos se vean con el formato original
--   incluso si el template se modifica posteriormente
-- ============================================
