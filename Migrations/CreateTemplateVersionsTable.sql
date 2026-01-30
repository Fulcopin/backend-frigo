-- Migración: Crear tabla TemplateVersions para historial de cambios
-- Fecha: 2025-12-26
-- Descripción: Guarda un snapshot completo cada vez que se edita una plantilla

-- Crear tabla TemplateVersions
CREATE TABLE TemplateVersions (
    VersionID INT IDENTITY(1,1) PRIMARY KEY,
    TemplateID INT NOT NULL,
    Version NVARCHAR(50) NOT NULL,
    Codigo NVARCHAR(100) NOT NULL,
    Nombre NVARCHAR(200) NOT NULL,
    Objetivo NVARCHAR(MAX) NULL,
    Proceso NVARCHAR(MAX) NULL,
    CuandoSeUsa NVARCHAR(MAX) NULL,
    QuienLoLlena NVARCHAR(MAX) NULL,
    HeaderFields NVARCHAR(MAX) NULL,
    BodyElements NVARCHAR(MAX) NULL,
    Firmas NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ChangeDescription NVARCHAR(500) NULL,
    ModifiedBy NVARCHAR(100) NULL,
    
    -- Foreign Key
    CONSTRAINT FK_TemplateVersions_Templates FOREIGN KEY (TemplateID) 
        REFERENCES Templates(TemplateID) ON DELETE CASCADE
);

-- Índices para mejorar rendimiento
CREATE INDEX IX_TemplateVersions_TemplateID ON TemplateVersions(TemplateID);
CREATE INDEX IX_TemplateVersions_Version ON TemplateVersions(Version);
CREATE INDEX IX_TemplateVersions_CreatedAt ON TemplateVersions(CreatedAt DESC);

-- Insertar snapshots de versiones actuales (para plantillas existentes)
-- Esto crea un registro histórico basado en las versiones que ya se usaron
INSERT INTO TemplateVersions (
    TemplateID, Version, Codigo, Nombre, Objetivo, Proceso, 
    CuandoSeUsa, QuienLoLlena, HeaderFields, BodyElements, Firmas, 
    CreatedAt, ChangeDescription
)
SELECT DISTINCT
    t.TemplateID,
    ISNULL(ff.TemplateVersion, t.Version) as Version,
    t.Codigo,
    t.Nombre,
    t.Objetivo,
    t.Proceso,
    t.CuandoSeUsa,
    t.QuienLoLlena,
    t.HeaderFields,
    t.BodyElements,
    t.Firmas,
    MIN(ff.CreatedAt) as CreatedAt,
    'Versión histórica importada del sistema anterior' as ChangeDescription
FROM Templates t
LEFT JOIN FilledForms ff ON t.TemplateID = ff.TemplateID
WHERE ff.TemplateVersion IS NOT NULL
GROUP BY 
    t.TemplateID, ff.TemplateVersion, t.Version, t.Codigo, t.Nombre, 
    t.Objetivo, t.Proceso, t.CuandoSeUsa, t.QuienLoLlena, 
    t.HeaderFields, t.BodyElements, t.Firmas;

-- Insertar versión actual de todas las plantillas
INSERT INTO TemplateVersions (
    TemplateID, Version, Codigo, Nombre, Objetivo, Proceso, 
    CuandoSeUsa, QuienLoLlena, HeaderFields, BodyElements, Firmas, 
    CreatedAt, ChangeDescription
)
SELECT 
    TemplateID,
    Version,
    Codigo,
    Nombre,
    Objetivo,
    Proceso,
    CuandoSeUsa,
    QuienLoLlena,
    HeaderFields,
    BodyElements,
    Firmas,
    GETUTCDATE() as CreatedAt,
    'Versión actual al momento de crear el historial' as ChangeDescription
FROM Templates
WHERE NOT EXISTS (
    SELECT 1 FROM TemplateVersions tv 
    WHERE tv.TemplateID = Templates.TemplateID 
    AND tv.Version = Templates.Version
);

PRINT 'Tabla TemplateVersions creada exitosamente';
PRINT 'Snapshots históricos insertados';
