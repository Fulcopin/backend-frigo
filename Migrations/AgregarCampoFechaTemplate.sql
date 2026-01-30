-- =====================================================
-- AGREGAR CAMPO "FECHA" AL HEADER DEL TEMPLATE
-- Template: FRM-TINAS-15-VERTICAL (ID: 38)
-- Fecha: 26/12/2025
-- =====================================================

-- 1. Ver el HeaderFields actual
SELECT 
    TemplateID,
    Codigo,
    Nombre,
    HeaderFields
FROM Templates
WHERE Codigo = 'FRM-TINAS-15-VERTICAL';

-- 2. Actualizar HeaderFields para agregar campo "fecha"
--    (Ejecuta solo si NO existe el campo fecha)

-- OPCIÓN A: Si el HeaderFields es NULL o no tiene campos
UPDATE Templates
SET HeaderFields = '[
  {
    "name": "fecha",
    "label": "Fecha",
    "type": "date",
    "required": true,
    "placeholder": ""
  }
]'
WHERE Codigo = 'FRM-TINAS-15-VERTICAL'
  AND (HeaderFields IS NULL OR HeaderFields = '[]' OR HeaderFields = '');

-- OPCIÓN B: Si ya tiene otros campos, necesitas agregar "fecha" al JSON existente
-- Ejemplo manual:
/*
UPDATE Templates
SET HeaderFields = '[
  {
    "name": "fecha",
    "label": "Fecha",
    "type": "date",
    "required": true,
    "placeholder": ""
  },
  {
    "name": "lote",
    "label": "Lote",
    "type": "text",
    "required": false
  }
]'
WHERE Codigo = 'FRM-TINAS-15-VERTICAL';
*/

-- 3. Verificar el cambio
SELECT 
    TemplateID,
    Codigo,
    Nombre,
    HeaderFields
FROM Templates
WHERE Codigo = 'FRM-TINAS-15-VERTICAL';

-- =====================================================
-- RESULTADO ESPERADO
-- =====================================================
-- HeaderFields debe contener:
-- [
--   {
--     "name": "fecha",
--     "label": "Fecha",
--     "type": "date",
--     "required": true
--   }
-- ]

-- =====================================================
-- VALIDACIÓN ADICIONAL
-- =====================================================

-- Ver todos los templates con campo "fecha" en HeaderFields
SELECT 
    TemplateID,
    Codigo,
    Nombre,
    CASE 
        WHEN HeaderFields LIKE '%fecha%' THEN 'Sí tiene fecha'
        ELSE 'NO tiene fecha'
    END AS TieneCampoFecha
FROM Templates
ORDER BY TemplateID;

-- =====================================================
-- NOTAS IMPORTANTES
-- =====================================================
-- 1. Después de ejecutar esto, REINICIA el frontend
-- 2. El campo "fecha" aparecerá en "Información General"
-- 3. La fecha se guardará automáticamente en headerData
-- 4. El PDF usará esta fecha en el encabezado
