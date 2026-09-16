-- ============================================================================
-- DESTRABAR UN FORMULARIO QUE DICE "El formulario ya esta firmado"
-- PERO QUE EN REALIDAD NO ESTA FIRMADO (aparece como pendiente).
--
-- Causa: quedo una fila HUERFANA en la tabla Signatures. El sistema viejo
-- bloquea la firma si existe CUALQUIER fila ahi, aunque FirmasData muestre
-- el formulario sin firmar. (El backend corregido ya no hace esto.)
--
-- USO: cambia @FormId por el numero del formulario trabado (ej. 1288),
--      ejecuta PASO 1 y PASO 2 para revisar, y si confirmas que NO esta
--      firmado, ejecuta PASO 3.
-- ============================================================================

DECLARE @FormId INT = 1288;   -- <<< CAMBIA ESTE NUMERO

-- PASO 1: Ver el formulario y su estado de firmas (revisa FirmasData: si los
--         puestos NO tienen "url"/"base64" con imagen, es que NO esta firmado).
SELECT FormID, TemplateID, FilledBy, CreatedAt, FirmasData
FROM   FilledForms
WHERE  FormID = @FormId;

-- PASO 2: Ver la(s) fila(s) en Signatures que estan bloqueando la firma.
--         Si FirmasData del PASO 1 esta sin firmar, estas filas son huerfanas.
SELECT Id, FilledFormId, SignedBy, SignedDate, Comments
FROM   Signatures
WHERE  FilledFormId = @FormId;

-- ----------------------------------------------------------------------------
-- PASO 3: SOLO si confirmaste que el formulario NO esta firmado, borra la fila
--         huerfana para poder volver a firmar. (Quita el comentario de abajo.)
-- ----------------------------------------------------------------------------
-- DELETE FROM Signatures WHERE FilledFormId = @FormId;

-- Verificacion final (debe quedar en 0 filas):
-- SELECT COUNT(*) AS FilasRestantes FROM Signatures WHERE FilledFormId = @FormId;
