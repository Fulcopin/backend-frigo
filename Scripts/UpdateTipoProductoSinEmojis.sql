-- Script para actualizar formularios existentes con emojis a texto plano
-- Fecha: 2026-02-19
-- Propósito: Cambiar "🦐 Camarón" y "🐟 Pescado" por "CAMARÓN" y "PESCADO"

USE FrigoLabDB;
GO

-- Ver formularios actuales con tipo de producto
SELECT 
    FormID,
    TipoProducto,
    CreatedAt,
    FilledBy
FROM FilledForms
WHERE TipoProducto IS NOT NULL
ORDER BY CreatedAt DESC;
GO

-- Actualizar formularios con emoji de camarón
UPDATE FilledForms
SET TipoProducto = 'CAMARÓN'
WHERE TipoProducto LIKE '%Camarón%' OR TipoProducto LIKE '%🦐%';
GO

-- Actualizar formularios con emoji de pescado
UPDATE FilledForms
SET TipoProducto = 'PESCADO'
WHERE TipoProducto LIKE '%Pescado%' OR TipoProducto LIKE '%🐟%';
GO

-- Verificar cambios
SELECT 
    FormID,
    TipoProducto,
    CreatedAt,
    FilledBy
FROM FilledForms
WHERE TipoProducto IS NOT NULL
ORDER BY CreatedAt DESC;
GO

-- Resumen de actualización
SELECT 
    'Total formularios actualizados' AS Descripcion,
    COUNT(*) AS Cantidad
FROM FilledForms
WHERE TipoProducto IN ('CAMARÓN', 'PESCADO');
GO
