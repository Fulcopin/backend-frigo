-- =====================================================
-- AGREGAR CAMPOS EDITABLES: CÓDIGO, VERSIÓN Y FECHA
-- Template: FRM-TINAS-15-VERTICAL (ID: 38)
-- Fecha: 26/12/2025
-- =====================================================

-- 1. Ver el HeaderFields actual del template
SELECT 
    TemplateID,
    Codigo,
    Nombre,
    Version,
    HeaderFields
FROM Templates
WHERE Codigo = 'FRM-TINAS-15-VERTICAL';

-- 2. Actualizar HeaderFields para incluir los 3 campos editables
--    CÓDIGO, VERSIÓN y FECHA

UPDATE Templates
SET HeaderFields = '[
  {
    "name": "codigo",
    "label": "Código",
    "type": "text",
    "required": true,
    "placeholder": "Ej: FRM-TINAS-15-VERTICAL"
  },
  {
    "name": "version",
    "label": "Versión",
    "type": "text",
    "required": true,
    "placeholder": "Ej: 10-00"
  },
  {
    "name": "fecha",
    "label": "Fecha",
    "type": "date",
    "required": true,
    "placeholder": ""
  }
]'
WHERE Codigo = 'FRM-TINAS-15-VERTICAL';

-- 3. Verificar el cambio
SELECT 
    TemplateID,
    Codigo,
    Nombre,
    Version,
    HeaderFields
FROM Templates
WHERE Codigo = 'FRM-TINAS-15-VERTICAL';

-- =====================================================
-- RESULTADO ESPERADO
-- =====================================================
-- HeaderFields debe contener los 3 campos:
-- [
--   { "name": "codigo", "label": "Código", "type": "text", "required": true },
--   { "name": "version", "label": "Versión", "type": "text", "required": true },
--   { "name": "fecha", "label": "Fecha", "type": "date", "required": true }
-- ]

-- =====================================================
-- VALIDACIÓN ADICIONAL
-- =====================================================

-- Contar cuántos campos tiene ahora
SELECT 
    TemplateID,
    Codigo,
    Nombre,
    LEN(HeaderFields) AS LongitudJSON,
    CASE 
        WHEN HeaderFields LIKE '%codigo%' THEN 'SI' 
        ELSE 'NO' 
    END AS TieneCodigo,
    CASE 
        WHEN HeaderFields LIKE '%version%' THEN 'SI' 
        ELSE 'NO' 
    END AS TieneVersion,
    CASE 
        WHEN HeaderFields LIKE '%fecha%' THEN 'SI' 
        ELSE 'NO' 
    END AS TieneFecha
FROM Templates
WHERE Codigo = 'FRM-TINAS-15-VERTICAL';

-- =====================================================
-- NOTAS IMPORTANTES
-- =====================================================
-- 1. Estos campos aparecerán en "Información General" del formulario
-- 2. El usuario podrá editar: Código, Versión y Fecha
-- 3. Los valores se guardan en headerData del formulario
-- 4. El PDF tomará estos valores para el encabezado
-- 5. Si no se llenan, el PDF usará valores por defecto del template
