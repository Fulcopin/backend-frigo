/* =============================================================================
   CONSULTA DE FORMULARIOS DE PRODUCCION (PD) Y SU PASO A INVENTARIO
   Base: FormBuilder-rg      Motor: SQL Server (compat 130+, usa OPENJSON)

   CADENA DE PROCESO (segun APIS.pdf):
     PD-04 FILETEO      -> genera producto fileteado   (YA IMPLEMENTADO)
     PD-05 LIBERACION   -> consume fileteado
     PD-06 CORTE/VACIO  -> consume fileteado  -> genera producto CORTADO
     PD-07 EMPAQUE FIN. -> consume cortado    -> genera producto EMPAQUETADO
     PD-10 CONGELACION  -> pendiente definir con produccion
     PD-11 REEMPAQUE    -> igual que PD-07 (cambia funda, mismo lote)

   COMO EJECUTARLO (PowerShell, desde la raiz del repo):
     sqlcmd -S ".\SQLEXPRESS" -d "FormBuilder-rg" -E -C -W -s "|" -f 65001 `
            -i "backend-frigo\Scripts\CONSULTA_FORMULARIOS_PD_INVENTARIO.sql"

   Las secciones 1 a 6 son SOLO LECTURA. La seccion 7 (backfill) esta
   comentada a proposito: leela y descomentala solo cuando decidas escribir.
   ============================================================================= */

USE [FormBuilder-rg];
GO
SET NOCOUNT ON;
GO


/* =============================================================================
   1. PLANTILLAS DE PRODUCCION DISPONIBLES
   Para saber que TemplateID corresponde a cada codigo PD.
   ============================================================================= */
PRINT '=== 1. PLANTILLAS PD ===';

SELECT
    t.TemplateID,
    t.Codigo,
    LEFT(t.Nombre, 50)                     AS Nombre,
    t.Version,
    t.IsDraft,
    t.IsObsolete,
    COUNT(f.FormID)                        AS FormulariosLlenados,
    CONVERT(varchar(16), MAX(f.CreatedAt), 120) AS UltimoRegistro
FROM Templates t
LEFT JOIN FilledForms f ON f.TemplateID = t.TemplateID
WHERE t.Codigo LIKE 'FOR-PD-%'
GROUP BY t.TemplateID, t.Codigo, t.Nombre, t.Version, t.IsDraft, t.IsObsolete
ORDER BY t.Codigo, t.TemplateID;
GO


/* =============================================================================
   2. FORMULARIOS PD LLENADOS  (los registros reales)
   Ajusta el filtro de fecha si quieres acotar el periodo.
   ============================================================================= */
PRINT '=== 2. FORMULARIOS PD LLENADOS ===';

SELECT
    f.FormID,
    t.Codigo,
    LEFT(t.Nombre, 35)                          AS Formulario,
    CONVERT(varchar(16), f.CreatedAt, 120)      AS Creado,
    LEFT(ISNULL(f.FilledBy, '-'), 22)           AS LlenadoPor,
    ISNULL(f.TemplateVersion, '-')              AS Ver,
    ISNULL(f.TipoProducto, '-')                 AS TipoProducto,
    /* lote de proceso: el nombre del campo cambia entre plantillas,
       por eso se buscan las tres variantes conocidas del encabezado */
    COALESCE(
        JSON_VALUE(f.HeaderData, '$.Lote'),
        JSON_VALUE(f.HeaderData, '$."LOTE DE PROCESO"'),
        JSON_VALUE(f.HeaderData, '$."Lote de proceso"')
    )                                           AS LoteEncabezado,
    LEN(f.BodyData)                             AS BytesBody
FROM FilledForms f
JOIN Templates t ON t.TemplateID = f.TemplateID
WHERE t.Codigo LIKE 'FOR-PD-%'
  -- AND f.CreatedAt >= '2026-01-01'
ORDER BY f.FormID DESC;
GO


/* =============================================================================
   3. ESTADO ACTUAL DEL INVENTARIO
   3a) Resumen por plantilla / proceso
   3b) Lotes con saldo real
   ============================================================================= */
PRINT '=== 3a. INVENTARIO POR PROCESO ===';

SELECT
    ISNULL(li.TemplateId, '(null)')  AS TemplateId,
    LEFT(ISNULL(li.Proceso, '(null)'), 40) AS Proceso,
    li.Estado,
    COUNT(*)                         AS Lotes,
    SUM(ISNULL(li.PesoEntrada, 0))   AS PesoEntrada,
    SUM(ISNULL(li.Desperdicio, 0))   AS Desperdicio,
    SUM(ISNULL(li.PesoNeto, 0))      AS PesoNeto,
    SUM(ISNULL(li.Saldo, 0))         AS Saldo
FROM LotesInventario li
GROUP BY li.TemplateId, li.Proceso, li.Estado
ORDER BY SUM(ISNULL(li.PesoNeto, 0)) DESC, COUNT(*) DESC;
GO

PRINT '=== 3b. LOTES CON SALDO ===';

SELECT
    li.Id,
    li.NumeroLote,
    ISNULL(li.LotePadre, '-')        AS LotePadre,
    LEFT(li.Producto, 25)            AS Producto,
    ISNULL(li.Clasificacion, '-')    AS Clasificacion,
    li.PesoEntrada,
    li.Desperdicio,
    li.PesoNeto,
    li.Saldo,
    li.Estado,
    li.FormId,
    li.TemplateId,
    CONVERT(varchar(10), li.Fecha, 120) AS Fecha
FROM LotesInventario li
WHERE ISNULL(li.Saldo, 0) > 0
ORDER BY li.Id DESC;
GO


/* =============================================================================
   4. *** DIAGNOSTICO PRINCIPAL ***
   Formularios PD llenados que NO generaron ninguna fila en LotesInventario.
   Esto es exactamente "lo que falta pasar a inventario".
   ============================================================================= */
PRINT '=== 4. FORMULARIOS PD SIN INVENTARIO ===';

SELECT
    t.Codigo,
    LEFT(t.Nombre, 40)                      AS Formulario,
    COUNT(*)                                AS FormsSinInventario,
    CONVERT(varchar(10), MIN(f.CreatedAt), 120) AS Desde,
    CONVERT(varchar(10), MAX(f.CreatedAt), 120) AS Hasta
FROM FilledForms f
JOIN Templates t ON t.TemplateID = f.TemplateID
WHERE t.Codigo LIKE 'FOR-PD-%'
  AND NOT EXISTS (SELECT 1 FROM LotesInventario li WHERE li.FormId = f.FormID)
GROUP BY t.Codigo, t.Nombre
ORDER BY COUNT(*) DESC;
GO

PRINT '=== 4b. DETALLE FORM x FORM ===';

SELECT
    f.FormID,
    t.Codigo,
    CONVERT(varchar(16), f.CreatedAt, 120)  AS Creado,
    LEFT(ISNULL(f.FilledBy, '-'), 20)       AS LlenadoPor,
    (SELECT COUNT(*) FROM LotesInventario li WHERE li.FormId = f.FormID)   AS FilasInventario,
    (SELECT COUNT(*) FROM LotesTrazabilidad lt WHERE lt.FormID = f.FormID) AS FilasTrazabilidad,
    (SELECT COUNT(*) FROM MovimientosInventario m WHERE m.FormId = f.FormID) AS Movimientos,
    CASE
        WHEN EXISTS (SELECT 1 FROM LotesInventario li WHERE li.FormId = f.FormID)
            THEN 'OK'
        ELSE 'FALTA PASAR A INVENTARIO'
    END                                     AS Estado
FROM FilledForms f
JOIN Templates t ON t.TemplateID = f.TemplateID
WHERE t.Codigo LIKE 'FOR-PD-%'
ORDER BY f.FormID DESC;
GO


PRINT '=== 4c. INVENTARIO FANTASMA (filas creadas pero sin peso) ===';
/* PD-06 y PD-07 si crean filas en LotesInventario, pero con PesoNeto = 0:
   el lote queda registrado y no descuenta ni aporta nada. Es el caso que
   hay que corregir segun APIS.pdf. */

SELECT
    li.TemplateId,
    t.Codigo,
    LEFT(li.Proceso, 30)             AS Proceso,
    COUNT(*)                         AS Filas,
    SUM(CASE WHEN ISNULL(li.PesoNeto, 0) = 0 THEN 1 ELSE 0 END) AS FilasEnCero,
    SUM(ISNULL(li.PesoNeto, 0))      AS PesoNetoTotal,
    SUM(CASE WHEN li.LotePadre IS NULL OR li.LotePadre = '' THEN 1 ELSE 0 END) AS SinLotePadre
FROM LotesInventario li
LEFT JOIN Templates t ON CAST(t.TemplateID AS varchar(100)) = li.TemplateId
GROUP BY li.TemplateId, t.Codigo, li.Proceso
HAVING SUM(CASE WHEN ISNULL(li.PesoNeto, 0) = 0 THEN 1 ELSE 0 END) > 0
ORDER BY FilasEnCero DESC;
GO


/* =============================================================================
   5. CONTENIDO DE LAS TABLAS DEL FORMULARIO (JSON -> filas)
   BodyData guarda un array de elementos; cada elemento de tipo "table"
   trae un array "data" con una fila por objeto.

   5a) Aplanado GENERICO celda por celda: no depende de tildes ni de nombres
       exactos de columna. Sirve para explorar cualquier PD.
   ============================================================================= */
PRINT '=== 5a. CELDAS DE FORMULARIOS PD ===';

SELECT
    f.FormID,
    t.Codigo,
    JSON_VALUE(el.value, '$.id')    AS ElementoId,
    CAST(fila.[key] AS int) + 1     AS NumFila,
    campo.[key]                     AS Columna,
    campo.value                     AS Valor
FROM FilledForms f
JOIN Templates t ON t.TemplateID = f.TemplateID
CROSS APPLY OPENJSON(f.BodyData) el
CROSS APPLY OPENJSON(JSON_QUERY(el.value, '$.data')) fila
CROSS APPLY OPENJSON(fila.value) campo
WHERE t.Codigo LIKE 'FOR-PD-%'
  AND JSON_VALUE(el.value, '$.type') = 'table'
  AND campo.[key] NOT LIKE '\_%' ESCAPE '\'   -- descarta metadatos: _apiCodigoId, etc.
  AND NULLIF(LTRIM(RTRIM(campo.value)), '') IS NOT NULL
  -- AND f.FormID = 1464
ORDER BY f.FormID DESC, ElementoId, NumFila, Columna;
GO


/* -----------------------------------------------------------------------------
   5b) Mismo aplanado pero PIVOTEADO a las columnas que importan para inventario.
       Se usa LIKE sobre el nombre de columna para tolerar tildes/mayusculas.
       Esta es la consulta que alimenta el backfill de la seccion 7.
   ----------------------------------------------------------------------------- */
PRINT '=== 5b. FILAS LISTAS PARA INVENTARIO ===';

WITH Celdas AS (
    SELECT
        f.FormID,
        f.TemplateID,
        t.Codigo,
        f.CreatedAt,
        JSON_VALUE(el.value, '$.id')  AS ElementoId,
        CAST(fila.[key] AS int)       AS NumFila,
        campo.[key]                   AS Columna,
        LTRIM(RTRIM(campo.value))     AS Valor
    FROM FilledForms f
    JOIN Templates t ON t.TemplateID = f.TemplateID
    CROSS APPLY OPENJSON(f.BodyData) el
    CROSS APPLY OPENJSON(JSON_QUERY(el.value, '$.data')) fila
    CROSS APPLY OPENJSON(fila.value) campo
    WHERE t.Codigo LIKE 'FOR-PD-%'
      AND JSON_VALUE(el.value, '$.type') = 'table'
)
SELECT
    c.FormID,
    c.Codigo,
    c.ElementoId,
    c.NumFila + 1 AS NumFila,
    MAX(CASE WHEN c.Columna LIKE '%LOTE%'          THEN c.Valor END) AS Lote,
    MAX(CASE WHEN c.Columna LIKE '%PRODUCTO%'
              OR c.Columna LIKE '%ESPECIE%'        THEN c.Valor END) AS Producto,
    MAX(CASE WHEN c.Columna LIKE '%CLASIFICA%'     THEN c.Valor END) AS Clasificacion,
    MAX(CASE WHEN c.Columna LIKE '%CLIENTE%'       THEN c.Valor END) AS Cliente,
    MAX(CASE WHEN c.Columna LIKE '%NETAS%'
              OR c.Columna LIKE '%Peso Neto%'
              OR c.Columna LIKE '%CANTIDAD%LBS%'
              OR c.Columna LIKE '%CANTIDAD Lbs%'   THEN c.Valor END) AS Lbs,
    MAX(CASE WHEN c.Columna LIKE '%BRUTO%'         THEN c.Valor END) AS PesoBruto,
    MAX(CASE WHEN c.Columna LIKE '%TEMPERATURA%'   THEN c.Valor END) AS Temperatura,
    MAX(CASE WHEN c.Columna LIKE '%GLASEO%'        THEN c.Valor END) AS Glaseo,
    CONVERT(varchar(10), c.CreatedAt, 120)                           AS Fecha
FROM Celdas c
GROUP BY c.FormID, c.Codigo, c.ElementoId, c.NumFila, c.CreatedAt
HAVING MAX(CASE WHEN c.Columna LIKE '%LOTE%'      THEN c.Valor END) IS NOT NULL
    OR MAX(CASE WHEN c.Columna LIKE '%PRODUCTO%'  THEN c.Valor END) IS NOT NULL
ORDER BY c.FormID DESC, c.ElementoId, c.NumFila;
GO


/* -----------------------------------------------------------------------------
   5c) MAPEO ENTRADA / SALIDA por formulario.
       Cada tabla del formulario cumple un rol distinto frente al inventario.
       Este mapa es la traduccion de APIS.pdf a los ElementoId reales.

       Codigo        ElementoId          Tabla                    Rol
       ------------  ------------------  -----------------------  ---------------
       FOR-PD-04 -1  1778691341839       CONTROL (mat. prima)     ENTRADA
       FOR-PD-04 -1  1778694196635       RESUMEN PRODUCCION       SALIDA (fileteado)
       FOR-PD-05     1688886401000       Registro Liberacion      ENTRADA (consume fileteado)
       FOR-PD-06     1678886403000       RESUMEN                  ENTRADA (consume fileteado)
       FOR-PD-06     1678886401000       Registro de Proceso      SALIDA (cortado)
       FOR-PD-07     tabla_salida_camara_1  Clasificacion         ENTRADA (consume cortado)
       FOR-PD-07     tabla_empaque_final    Registro Empaque Fin. SALIDA (empaquetado)
       FOR-PD-11     tabla_reempaque        Reempaque             SALIDA (mismo lote, nueva funda)
   ----------------------------------------------------------------------------- */
PRINT '=== 5c. ENTRADAS vs SALIDAS POR FORMULARIO ===';

WITH Mapa AS (
    SELECT * FROM (VALUES
        ('FOR-PD-04 -1', '1778691341839',       'ENTRADA', 'Materia prima'),
        ('FOR-PD-04 -1', '1778694196635',       'SALIDA',  'Fileteado'),
        ('FOR-PD-05',    '1688886401000',       'ENTRADA', 'Fileteado'),
        ('FOR-PD-06',    '1678886403000',       'ENTRADA', 'Fileteado'),
        ('FOR-PD-06',    '1678886401000',       'SALIDA',  'Cortado'),
        ('FOR-PD-07',    'tabla_salida_camara_1','ENTRADA','Cortado'),
        ('FOR-PD-07',    'tabla_empaque_final',  'SALIDA', 'Empaquetado'),
        ('FOR-PD-11',    'tabla_reempaque',      'SALIDA', 'Reempacado')
    ) v(Codigo, ElementoId, Rol, Estadio)
),
Celdas AS (
    SELECT f.FormID, t.Codigo, f.CreatedAt,
           JSON_VALUE(el.value, '$.id') AS ElementoId,
           CAST(fila.[key] AS int)      AS NumFila,
           campo.[key]                  AS Columna,
           LTRIM(RTRIM(campo.value))    AS Valor
    FROM FilledForms f
    JOIN Templates t ON t.TemplateID = f.TemplateID
    CROSS APPLY OPENJSON(f.BodyData) el
    CROSS APPLY OPENJSON(JSON_QUERY(el.value, '$.data')) fila
    CROSS APPLY OPENJSON(fila.value) campo
    WHERE JSON_VALUE(el.value, '$.type') = 'table'
),
Filas AS (
    SELECT c.FormID, c.Codigo, c.ElementoId, c.NumFila, c.CreatedAt,
           MAX(CASE WHEN c.Columna LIKE '%LOTE%'      THEN c.Valor END) AS Lote,
           MAX(CASE WHEN c.Columna LIKE '%PRODUCTO%'  THEN c.Valor END) AS Producto,
           MAX(CASE WHEN c.Columna LIKE '%CLASIFICA%' THEN c.Valor END) AS Clasificacion,
           MAX(CASE WHEN c.Columna LIKE '%NETAS%'
                     OR c.Columna LIKE '%Peso Neto%'
                     OR c.Columna LIKE '%CANTIDAD%'   THEN c.Valor END) AS Lbs
    FROM Celdas c
    GROUP BY c.FormID, c.Codigo, c.ElementoId, c.NumFila, c.CreatedAt
)
SELECT
    m.Rol,
    m.Estadio,
    fi.Codigo,
    fi.FormID,
    fi.NumFila + 1                          AS NumFila,
    fi.Lote,
    fi.Producto,
    fi.Clasificacion,
    TRY_CONVERT(decimal(18,4), fi.Lbs)      AS Lbs,
    CONVERT(varchar(10), fi.CreatedAt, 120) AS Fecha,
    CASE WHEN EXISTS (SELECT 1 FROM LotesInventario li
                      WHERE li.FormId = fi.FormID
                        AND li.NumeroLote = fi.Lote
                        AND ISNULL(li.PesoNeto, 0) > 0)
         THEN 'YA EN INVENTARIO' ELSE 'PENDIENTE' END AS EstadoInventario
FROM Filas fi
JOIN Mapa m ON m.Codigo = fi.Codigo AND m.ElementoId = fi.ElementoId
WHERE fi.Lote IS NOT NULL AND fi.Lote <> ''
ORDER BY fi.FormID DESC, m.Rol, fi.NumFila;
GO


/* =============================================================================
   6. TRAZABILIDAD: cadena lote padre -> lotes hijos
   Convencion actual (la genera PD-04 V2): el lote del encabezado es el padre
   y cada fila del RESUMEN DE PRODUCCION crea un hijo con sufijo -P01, -P02...
   ============================================================================= */
PRINT '=== 6. CADENA DE LOTES ===';

SELECT
    p.NumeroLote                     AS LotePadre,
    p.Producto                       AS ProductoPadre,
    p.PesoEntrada                    AS EntradaPadre,
    p.Desperdicio,
    p.PesoNeto                       AS NetoPadre,
    h.NumeroLote                     AS LoteHijo,
    h.Producto                       AS ProductoHijo,
    h.PesoNeto                       AS NetoHijo,
    h.Saldo                          AS SaldoHijo,
    h.FormId,
    h.TemplateId
FROM LotesInventario p
LEFT JOIN LotesInventario h ON h.LotePadre = p.NumeroLote
WHERE p.LotePadre IS NULL OR p.LotePadre = ''
ORDER BY p.Id DESC, h.Id;
GO

PRINT '=== 6b. MOVIMIENTOS ===';

SELECT TOP 100
    m.Id, m.NumeroLote, m.Tipo, m.Cantidad, m.SaldoResultante,
    LEFT(m.Proceso, 30) AS Proceso, m.FormId,
    CONVERT(varchar(16), m.CreadoEn, 120) AS CreadoEn
FROM MovimientosInventario m
ORDER BY m.Id DESC;
GO


/* =============================================================================
   7. BACKFILL: pasar a inventario los formularios PD que faltan
      ---------------------------------------------------------------
      *** ESTA SECCION ESTA COMENTADA A PROPOSITO ***
      Antes de descomentarla:
        1. Corre la seccion 5b y revisa que Lote / Producto / Lbs salgan bien.
        2. Saca respaldo:  BACKUP DATABASE [FormBuilder-rg] TO DISK='...'
        3. Ejecuta primero con @Commit = 0 (hace ROLLBACK y solo muestra).
      Ajusta @Codigo para procesar un formulario a la vez: PD-06, luego PD-07.
   ============================================================================= */
/*
DECLARE @Codigo  varchar(20) = 'FOR-PD-06';
DECLARE @Proceso varchar(50) = 'Corte';        -- 'Corte' | 'Empaque final' | 'Liberacion'
DECLARE @Commit  bit         = 0;              -- 0 = ensayo (rollback), 1 = grabar

BEGIN TRAN;

WITH Celdas AS (
    SELECT f.FormID, f.TemplateID, t.Codigo, f.CreatedAt,
           JSON_VALUE(el.value, '$.id') AS ElementoId,
           CAST(fila.[key] AS int)      AS NumFila,
           campo.[key]                  AS Columna,
           LTRIM(RTRIM(campo.value))    AS Valor
    FROM FilledForms f
    JOIN Templates t ON t.TemplateID = f.TemplateID
    CROSS APPLY OPENJSON(f.BodyData) el
    CROSS APPLY OPENJSON(JSON_QUERY(el.value, '$.data')) fila
    CROSS APPLY OPENJSON(fila.value) campo
    WHERE t.Codigo = @Codigo
      AND JSON_VALUE(el.value, '$.type') = 'table'
      AND NOT EXISTS (SELECT 1 FROM LotesInventario li WHERE li.FormId = f.FormID)
),
Filas AS (
    SELECT c.FormID, c.TemplateID, c.CreatedAt, c.ElementoId, c.NumFila,
           MAX(CASE WHEN c.Columna LIKE '%LOTE%'      THEN c.Valor END) AS Lote,
           MAX(CASE WHEN c.Columna LIKE '%PRODUCTO%'  THEN c.Valor END) AS Producto,
           MAX(CASE WHEN c.Columna LIKE '%CLASIFICA%' THEN c.Valor END) AS Clasificacion,
           MAX(CASE WHEN c.Columna LIKE '%NETAS%'
                     OR c.Columna LIKE '%CANTIDAD%'   THEN c.Valor END) AS Lbs
    FROM Celdas c
    GROUP BY c.FormID, c.TemplateID, c.CreatedAt, c.ElementoId, c.NumFila
)
INSERT INTO LotesInventario
    (NumeroLote, Proceso, Producto, Clasificacion,
     PesoEntrada, Desperdicio, PesoNeto, Saldo,
     Estado, LotePadre, FormId, TemplateId, Fecha, CreadoEn)
SELECT
    fi.Lote,
    @Proceso,
    fi.Producto,
    fi.Clasificacion,
    TRY_CONVERT(decimal(18,4), fi.Lbs),
    0,
    TRY_CONVERT(decimal(18,4), fi.Lbs),
    TRY_CONVERT(decimal(18,4), fi.Lbs),
    'disponible',
    NULL,                                  -- << completar con el lote de origen
    fi.FormID,
    CAST(fi.TemplateID AS varchar(100)),
    CAST(fi.CreatedAt AS date),
    SYSDATETIME()
FROM Filas fi
WHERE fi.Lote IS NOT NULL
  AND TRY_CONVERT(decimal(18,4), fi.Lbs) > 0
  AND NOT EXISTS (SELECT 1 FROM LotesInventario li
                  WHERE li.NumeroLote = fi.Lote AND li.FormId = fi.FormID);

SELECT @@ROWCOUNT AS FilasInsertadas;

IF @Commit = 1 COMMIT TRAN; ELSE ROLLBACK TRAN;
*/
