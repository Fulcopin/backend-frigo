/* =============================================================================
   CORRECCIÓN: los "cambios de proceso" no debían restar saldo
   Base: FormBuilder-rg

   QUÉ PASÓ
   Hasta ahora, marcar una tabla como "Cambio de Proceso" hacía que el formulario
   llamara a consumir-cantidad: el movimiento quedaba como Tipo = 'salida' y el
   saldo del lote bajaba. Pero un cambio de proceso NO consume el producto — el
   lote sigue en la planta, solo que en otra etapa. El saldo no debía moverse.

   QUÉ HACE ESTE SCRIPT
     1. Muestra los movimientos afectados (solo lectura).
     2. Muestra cuánto saldo se le devuelve a cada lote (solo lectura).
     3. Corrige, dentro de una transacción con ensayo:
          · devuelve al lote el saldo que se le había restado
          · reclasifica el movimiento a Tipo = 'traspaso'
          · corrige el SaldoResultante que guardó ese movimiento
          · recalcula el Estado (disponible / parcial / consumido)

   CÓMO EJECUTARLO
     sqlcmd -S ".\SQLEXPRESS" -d "FormBuilder-rg" -E -C -W -s "|" -f 65001 `
            -i "backend-frigo\Scripts\FIX_CambioProcesoNoDebeRestar.sql"

     Contra el servidor de planta, cambiar -S por el suyo.

   IMPORTANTE
     · Las secciones 1 y 2 no escriben. Corré ESAS PRIMERO y revisá el resultado.
     · La sección 3 arranca con @Commit = 0: hace todo y deshace, mostrando el
       resultado. Recién cuando los números cierren, poné @Commit = 1.
     · Sacá respaldo antes de commitear:
         BACKUP DATABASE [FormBuilder-rg] TO DISK = 'C:\temp\FormBuilder-rg.bak';
   ============================================================================= */

USE [FormBuilder-rg];
GO
SET NOCOUNT ON;
GO


/* =============================================================================
   1. QUÉ MOVIMIENTOS ESTÁN MAL
   Cambios de proceso registrados como salida: restaron cuando no debían.
   ============================================================================= */
PRINT '=== 1. MOVIMIENTOS A CORREGIR ===';

SELECT
    m.Id,
    m.NumeroLote,
    m.Tipo,
    m.Cantidad                                  AS SeRestoDeMas,
    m.SaldoResultante                           AS SaldoQueGuardo,
    m.SaldoResultante + m.Cantidad              AS SaldoQueDebiaQuedar,
    LEFT(ISNULL(m.Proceso, '—'), 28)            AS ProcesoDestino,
    m.FormId,
    CONVERT(varchar(16), m.CreadoEn, 120)       AS Creado,
    LEFT(ISNULL(m.Notas, ''), 60)               AS Notas
FROM MovimientosInventario m
WHERE m.Tipo = 'salida'
  AND m.Notas LIKE '%#cambio_proceso:%'
ORDER BY m.NumeroLote, m.Id;
GO


/* =============================================================================
   2. IMPACTO POR LOTE
   Cuánto saldo recupera cada uno. "SaldoCorregido" es cómo debería quedar.
   ============================================================================= */
PRINT '=== 2. IMPACTO POR LOTE ===';

SELECT
    li.Id,
    li.NumeroLote,
    LEFT(ISNULL(li.Producto, '—'), 26)          AS Producto,
    li.PesoNeto,
    li.Saldo                                    AS SaldoActual,
    SUM(m.Cantidad)                             AS SeDevuelve,
    li.Saldo + SUM(m.Cantidad)                  AS SaldoCorregido,
    COUNT(*)                                    AS Movimientos,
    li.Estado                                   AS EstadoActual,
    CASE
        WHEN li.Saldo + SUM(m.Cantidad) <= 0            THEN 'consumido'
        WHEN li.Saldo + SUM(m.Cantidad) < li.PesoNeto   THEN 'parcial'
        ELSE 'disponible'
    END                                         AS EstadoCorregido
FROM MovimientosInventario m
JOIN LotesInventario li ON li.NumeroLote = m.NumeroLote
WHERE m.Tipo = 'salida'
  AND m.Notas LIKE '%#cambio_proceso:%'
GROUP BY li.Id, li.NumeroLote, li.Producto, li.PesoNeto, li.Saldo, li.Estado
ORDER BY SUM(m.Cantidad) DESC;
GO


/* =============================================================================
   3. LA CORRECCIÓN
   Arranca en ENSAYO (@Commit = 0): aplica, muestra y deshace.
   Poné @Commit = 1 solo después de revisar las secciones 1 y 2.
   ============================================================================= */
PRINT '=== 3. CORRECCION ===';

DECLARE @Commit bit = 0;   -- 0 = ensayo (rollback) · 1 = grabar

BEGIN TRAN;

BEGIN TRY

    /* Se congela la lista de movimientos a tocar: si algo corre en paralelo
       mientras esto se ejecuta, no queremos que el conjunto cambie a mitad. */
    DECLARE @Afectados TABLE (
        MovId       int PRIMARY KEY,
        NumeroLote  nvarchar(200),
        Cantidad    decimal(18,4)
    );

    INSERT INTO @Afectados (MovId, NumeroLote, Cantidad)
    SELECT m.Id, m.NumeroLote, m.Cantidad
    FROM MovimientosInventario m
    WHERE m.Tipo = 'salida'
      AND m.Notas LIKE '%#cambio_proceso:%';

    DECLARE @totalMovs int = (SELECT COUNT(*) FROM @Afectados);
    PRINT CONCAT('Movimientos a corregir: ', @totalMovs);

    /* ── 3a. Devolver el saldo a cada lote ─────────────────────────────────── */
    UPDATE li
    SET li.Saldo         = li.Saldo + d.Devolver,
        li.ActualizadoEn = SYSDATETIME()
    FROM LotesInventario li
    JOIN (
        SELECT NumeroLote, SUM(Cantidad) AS Devolver
        FROM @Afectados
        GROUP BY NumeroLote
    ) d ON d.NumeroLote = li.NumeroLote;

    PRINT CONCAT('Lotes con saldo devuelto: ', @@ROWCOUNT);

    /* ── 3b. Recalcular el estado con el saldo ya corregido ────────────────── */
    UPDATE li
    SET li.Estado = CASE
                        WHEN li.Saldo <= 0          THEN 'consumido'
                        WHEN li.Saldo < li.PesoNeto THEN 'parcial'
                        ELSE 'disponible'
                    END
    FROM LotesInventario li
    WHERE li.NumeroLote IN (SELECT NumeroLote FROM @Afectados);

    /* ── 3c. Reclasificar el movimiento ────────────────────────────────────────
       No se borra: el cambio de proceso ocurrió de verdad y hay que poder
       leerlo. Pasa a 'traspaso', que es el tipo que no descuenta, y se corrige
       el SaldoResultante para que refleje que el saldo no se movió. */
    UPDATE m
    SET m.Tipo            = 'traspaso',
        m.SaldoResultante = m.SaldoResultante + m.Cantidad,
        m.Notas           = LEFT(ISNULL(m.Notas, '') + ' [corregido: no descuenta]', 1000)
    FROM MovimientosInventario m
    JOIN @Afectados a ON a.MovId = m.Id;

    PRINT CONCAT('Movimientos reclasificados a traspaso: ', @@ROWCOUNT);

    /* ── 3d. Resultado, para revisar antes de decidir ──────────────────────── */
    PRINT '--- Lotes despues de la correccion ---';
    SELECT
        li.NumeroLote,
        LEFT(ISNULL(li.Producto, '—'), 26)  AS Producto,
        li.PesoNeto,
        li.Saldo                            AS SaldoNuevo,
        li.Estado                           AS EstadoNuevo
    FROM LotesInventario li
    WHERE li.NumeroLote IN (SELECT NumeroLote FROM @Afectados)
    ORDER BY li.NumeroLote;

    IF @Commit = 1
    BEGIN
        COMMIT TRAN;
        PRINT '>>> CAMBIOS GRABADOS.';
    END
    ELSE
    BEGIN
        ROLLBACK TRAN;
        PRINT '>>> ENSAYO: no se grabo nada. Pone @Commit = 1 para aplicar.';
    END

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '>>> ERROR: se deshizo todo.';
    PRINT ERROR_MESSAGE();
END CATCH
GO


/* =============================================================================
   4. VERIFICACIÓN — correr DESPUÉS de grabar
   Las dos consultas tienen que dar 0 filas.
   ============================================================================= */
PRINT '=== 4. VERIFICACION ===';

SELECT COUNT(*) AS CambiosDeProcesoQueAunRestan
FROM MovimientosInventario
WHERE Tipo = 'salida' AND Notas LIKE '%#cambio_proceso:%';

SELECT COUNT(*) AS LotesConSaldoMayorAlPesoNeto
FROM LotesInventario
WHERE Saldo > PesoNeto AND PesoNeto > 0;
GO
