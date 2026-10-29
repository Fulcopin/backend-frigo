-- ─────────────────────────────────────────────────────────────────────────────
-- Historial de cambios de plantillas: permitir descripciones largas
--
-- Antes la columna era nvarchar(1000). Cuando alguien guardaba una descripción
-- de más de 1000 caracteres, el INSERT fallaba en SQL Server, el API devolvía
-- 500 sin cabeceras CORS y el navegador mostraba "Failed to fetch".
--
-- Aplicar una sola vez en la base del servidor (SSMS o sqlcmd).
-- ─────────────────────────────────────────────────────────────────────────────

IF COL_LENGTH('dbo.TemplateChangeLogs', 'CambioRealizado') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TemplateChangeLogs
        ALTER COLUMN CambioRealizado nvarchar(max) NOT NULL;
    PRINT 'TemplateChangeLogs.CambioRealizado ahora es nvarchar(max).';
END
ELSE
BEGIN
    PRINT 'No existe la tabla/columna TemplateChangeLogs.CambioRealizado.';
END
GO
