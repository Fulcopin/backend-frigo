-- ============================================================================
-- QUITAR "2.5mm Fe, " DEL ENCABEZADO DE COLUMNA DEL FORMULARIO FOR-CC-16
--
-- Columna actual:  Desafío c/hora con esferas: 2.5mm Fe, 3mm Fe, 3mm NoFe, 4mm SS - ¿Exitoso?
-- Columna deseada: Desafío c/hora con esferas: 3mm Fe, 3mm NoFe, 4mm SS - ¿Exitoso?
--
-- Quita el texto "2.5mm Fe, " (con su coma y espacio) en TODOS los lugares donde
-- se guarda, para que quede consistente:
--   1) Templates.BodyElements         -> la plantilla actual (encabezado de columna)
--   2) FilledForms.BodyData           -> registros ya llenados (la llave de cada celda;
--                                         el VALOR de la celda se conserva)
--   3) FilledForms.TemplateSnapshot   -> la copia congelada que usan los registros viejos
--                                         para dibujarse (SIN esto, los viejos NO cambian)
--   4) TemplateVersions.BodyElements  -> historial de versiones de la plantilla
--
-- ⚠️ ANTES DE EJECUTAR: haz un BACKUP de la base (o al menos de estas 3 tablas).
--    Se puede deshacer con ROLLBACK mientras no hagas COMMIT.
-- ============================================================================

SET NOCOUNT ON;
DECLARE @Buscar  NVARCHAR(100) = N'2.5mm Fe, ';   -- lo que se quita (tal como esta guardado)
DECLARE @Codigo  NVARCHAR(50)  = N'FOR-CC-16';    -- formulario objetivo

BEGIN TRANSACTION;

-- ---------------------------------------------------------------------------
-- PASO 1 (PREVIEW): cuantas filas contienen el texto en cada tabla.
--   Si TODO sale en 0, revisa el texto exacto (mayusculas/acentos) antes de seguir.
-- ---------------------------------------------------------------------------
SELECT 'Templates (esta plantilla)' AS Tabla,
       COUNT(*) AS FilasConTexto
FROM   Templates
WHERE  Codigo = @Codigo AND BodyElements LIKE N'%' + @Buscar + N'%';

SELECT 'FilledForms (registros llenados)' AS Tabla,
       COUNT(*) AS FilasConTexto
FROM   FilledForms
WHERE  TemplateID IN (SELECT TemplateID FROM Templates WHERE Codigo = @Codigo)
  AND (BodyData LIKE N'%' + @Buscar + N'%' OR TemplateSnapshot LIKE N'%' + @Buscar + N'%');

SELECT 'TemplateVersions (historial)' AS Tabla,
       COUNT(*) AS FilasConTexto
FROM   TemplateVersions
WHERE  TemplateID IN (SELECT TemplateID FROM Templates WHERE Codigo = @Codigo)
  AND  BodyElements LIKE N'%' + @Buscar + N'%';

-- ---------------------------------------------------------------------------
-- PASO 2 (CAMBIOS): reemplaza el texto por vacio en las 3 tablas.
-- ---------------------------------------------------------------------------
UPDATE Templates
SET    BodyElements = REPLACE(BodyElements, @Buscar, N'')
WHERE  Codigo = @Codigo AND BodyElements LIKE N'%' + @Buscar + N'%';

UPDATE FilledForms
SET    BodyData        = REPLACE(BodyData, @Buscar, N''),
       TemplateSnapshot = REPLACE(TemplateSnapshot, @Buscar, N'')
WHERE  TemplateID IN (SELECT TemplateID FROM Templates WHERE Codigo = @Codigo)
  AND (BodyData LIKE N'%' + @Buscar + N'%' OR TemplateSnapshot LIKE N'%' + @Buscar + N'%');

UPDATE TemplateVersions
SET    BodyElements = REPLACE(BodyElements, @Buscar, N'')
WHERE  TemplateID IN (SELECT TemplateID FROM Templates WHERE Codigo = @Codigo)
  AND  BodyElements LIKE N'%' + @Buscar + N'%';

-- ---------------------------------------------------------------------------
-- PASO 3 (VERIFICACION): debe quedar 0 en las tres.
-- ---------------------------------------------------------------------------
SELECT 'Restante Templates'      AS Chequeo, COUNT(*) AS FilasConTexto FROM Templates
WHERE  Codigo = @Codigo AND BodyElements LIKE N'%' + @Buscar + N'%';
SELECT 'Restante FilledForms'    AS Chequeo, COUNT(*) AS FilasConTexto FROM FilledForms
WHERE  TemplateID IN (SELECT TemplateID FROM Templates WHERE Codigo = @Codigo)
  AND (BodyData LIKE N'%' + @Buscar + N'%' OR TemplateSnapshot LIKE N'%' + @Buscar + N'%');
SELECT 'Restante TemplateVersions' AS Chequeo, COUNT(*) AS FilasConTexto FROM TemplateVersions
WHERE  TemplateID IN (SELECT TemplateID FROM Templates WHERE Codigo = @Codigo)
  AND  BodyElements LIKE N'%' + @Buscar + N'%';

-- ---------------------------------------------------------------------------
-- PASO 4: si los numeros del PASO 1 tenian sentido y el PASO 3 quedo en 0,
--         confirma los cambios ejecutando:   COMMIT;
--         si algo se ve mal, revierte todo con:   ROLLBACK;
-- ---------------------------------------------------------------------------
-- COMMIT;
-- ROLLBACK;
