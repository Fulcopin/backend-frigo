# 📋 Estructura Vertical - 15 Tinas

## ✅ Diseño Implementado: FILAS VERTICALES

Cada **FILA** es una **TINA** completa con todos sus datos.

### Visualización de la Tabla:

```
┌──────────┬──────┬─────────┬─────────┬─────────┬─────────┬─────────┬──────────┐
│ ⏰ HORA  │ 🔵   │ ⚖️     │ ⚖️     │ ⚖️     │ ⚖️     │ ⚖️     │ 📊      │
│          │ TINA │ PESO 1  │ PESO 2  │ PESO 3  │ PESO 4  │ PESO 5  │ TOTAL    │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 08:00    │ T1   │ 25.5 kg │ 30.2 kg │ 22.8 kg │ 28.0 kg │ 24.5 kg │ 131.0 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 08:15    │ T2   │ 27.3 kg │ 29.1 kg │ 26.4 kg │ 25.7 kg │ 31.2 kg │ 139.7 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 08:30    │ T3   │ 23.8 kg │ 28.5 kg │ 30.1 kg │ 27.6 kg │ 29.3 kg │ 139.3 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 08:45    │ T4   │ 26.2 kg │ 24.9 kg │ 28.7 kg │ 29.4 kg │ 26.8 kg │ 136.0 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 09:00    │ T5   │ 29.5 kg │ 27.8 kg │ 25.3 kg │ 30.6 kg │ 28.1 kg │ 141.3 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 09:15    │ T6   │ 24.7 kg │ 31.2 kg │ 27.9 kg │ 26.3 kg │ 30.5 kg │ 140.6 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 09:30    │ T7   │ 28.4 kg │ 26.7 kg │ 29.8 kg │ 28.2 kg │ 27.5 kg │ 140.6 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 09:45    │ T8   │ 25.9 kg │ 29.3 kg │ 26.5 kg │ 31.1 kg │ 28.9 kg │ 141.7 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 10:00    │ T9   │ 30.2 kg │ 28.6 kg │ 27.4 kg │ 29.7 kg │ 25.8 kg │ 141.7 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 10:15    │ T10  │ 27.1 kg │ 30.4 kg │ 28.9 kg │ 26.8 kg │ 29.6 kg │ 142.8 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 10:30    │ T11  │ 26.5 kg │ 28.1 kg │ 30.7 kg │ 27.3 kg │ 28.4 kg │ 141.0 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 10:45    │ T12  │ 29.8 kg │ 27.2 kg │ 26.9 kg │ 30.5 kg │ 27.9 kg │ 142.3 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 11:00    │ T13  │ 28.3 kg │ 29.7 kg │ 28.4 kg │ 27.8 kg │ 31.0 kg │ 145.2 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 11:15    │ T14  │ 27.6 kg │ 30.9 kg │ 29.2 kg │ 28.5 kg │ 26.7 kg │ 142.9 kg │
├──────────┼──────┼─────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│ 11:30    │ T15  │ 30.1 kg │ 28.8 kg │ 27.5 kg │ 29.3 kg │ 30.2 kg │ 145.9 kg │
└──────────┴──────┴─────────┴─────────┴─────────┴─────────┴─────────┴──────────┘

🏆 TOTAL GENERAL: 2,112.0 kg
```

## 🎯 Características:

### ✅ Ventajas de las Filas Verticales:
1. **Fácil de leer**: Cada fila es una tina completa
2. **Scroll vertical natural**: Se navega hacia abajo por las tinas
3. **Todos los datos visibles**: Una hora + 5 pesos + total en una sola línea
4. **Comparación fácil**: Se pueden comparar tinas mirando verticalmente
5. **Compacto**: Tabla de 15 filas × 8 columnas

### 📊 Estructura de Datos:

```json
{
  "type": "table",
  "columns": [
    { "header": "⏰ HORA", "type": "time" },
    { "header": "🔵 TINA", "type": "text" },
    { "header": "⚖️ PESO 1", "type": "number" },
    { "header": "⚖️ PESO 2", "type": "number" },
    { "header": "⚖️ PESO 3", "type": "number" },
    { "header": "⚖️ PESO 4", "type": "number" },
    { "header": "⚖️ PESO 5", "type": "number" },
    { "header": "📊 TOTAL", "type": "calculated" }
  ],
  "rows": [
    {
      "cells": [
        { "value": "08:00" },
        { "value": "T1" },
        { "value": 25.5 },
        { "value": 30.2 },
        { "value": 22.8 },
        { "value": 28.0 },
        { "value": 24.5 },
        { "formula": "sum(...)", "result": 131.0 }
      ]
    },
    // ... 14 filas más
  ]
}
```

## 🔄 Flujo de Llenado:

1. Usuario selecciona **FILA 1 (T1)**
2. Ingresa **HORA**: `08:00`
3. Ingresa **PESO 1**: `25.5`
4. Ingresa **PESO 2**: `30.2`
5. Ingresa **PESO 3**: `22.8`
6. Ingresa **PESO 4**: `28.0`
7. Ingresa **PESO 5**: `24.5`
8. ✅ **TOTAL se calcula automáticamente**: `131.0 kg`
9. Pasa a la **FILA 2 (T2)** y repite

## 🆚 Comparación con Diseño Anterior:

| Característica | Diseño Anterior (Secciones) | ✅ Diseño Actual (Filas) |
|----------------|------------------------------|---------------------------|
| Layout | 15 secciones colapsables | 1 tabla de 15 filas |
| Navegación | Scroll + abrir/cerrar secciones | Scroll vertical simple |
| Comparación | Difícil (datos separados) | Fácil (todo en pantalla) |
| Espacio | Mucho (15 secciones grandes) | Compacto (1 tabla) |
| Llenado | Click para expandir cada tina | Directo en cada fila |
| Vista general | Solo 3 tinas visibles | Todas las tinas visibles |

## 🎨 Responsividad:

### Desktop (1920px):
- Tabla completa visible
- Todas las columnas horizontales
- Scroll vertical para 15 filas

### Tablet (768px):
- Scroll horizontal para ver todas las columnas
- Prioridad: HORA | TINA | TOTAL visible
- PESO 1-5 con scroll lateral

### Mobile (375px):
- Vista de tarjetas (cards)
- Cada tina en un card expandible
- Campos apilados verticalmente

## 📝 Endpoint:

```http
POST /api/TemplatePresets/create-15-tinas
```

**Response:**
```json
{
  "templateID": 1,
  "codigo": "FRM-TINAS-15-VERTICAL",
  "nombre": "Registro 15 Tinas (Filas Verticales)",
  "version": "10-00"
}
```

## 🚀 Para Probar:

```powershell
# 1. Ejecutar el servidor
dotnet run

# 2. Crear el template
Invoke-RestMethod -Uri "http://localhost:5000/api/TemplatePresets/create-15-tinas" -Method POST

# 3. Ver el template creado
Invoke-RestMethod -Uri "http://localhost:5000/api/Templates/1" -Method GET
```

---

**Código:** `FRM-TINAS-15-VERTICAL`  
**Versión:** 10-00  
**Fecha:** 22/12/2025
