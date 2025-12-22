# 🏭 Sistema de Registro de 15 Tinas - Documentación Completa

## 📋 Problema
- Tienes **15 tinas** (T1 a T15)
- Necesitas registrar múltiples entradas por tina
- Cada tina debe calcular su **total automáticamente**
- No quieres crear 15 tablas separadas

---

## ✅ SOLUCIÓN IMPLEMENTADA: Tabla Única con Resumen Automático

### 🎯 Estructura del Template

```json
{
  "templateID": 0,
  "codigo": "FRM-TINAS-15",
  "nombre": "Registro de Producción - 15 Tinas",
  "version": "01-00",
  "objetivo": "Registrar la producción de las 15 tinas y calcular totales automáticos",
  "proceso": "Producción",
  "cuandoSeUsa": "Durante el turno de producción para registrar cada tina",
  "quienLoLlena": "Asistente de Producción",
  
  "headerFields": [
    { "label": "Fecha", "type": "date", "required": true },
    { "label": "Turno", "type": "select", "options": ["Mañana", "Tarde", "Noche"] },
    { "label": "Responsable", "type": "text", "required": true },
    { "label": "Lote de Proceso", "type": "text", "required": true }
  ],
  
  "bodyElements": [
    {
      "type": "table",
      "id": "registro-detallado",
      "title": "Registro Detallado por Tina",
      "description": "Registra cada entrada de producción indicando la tina correspondiente",
      "columns": [
        {
          "name": "HORA",
          "type": "time",
          "width": 10,
          "required": true
        },
        {
          "name": "TINA",
          "type": "select",
          "width": 10,
          "required": true,
          "options": [
            "T1", "T2", "T3", "T4", "T5", 
            "T6", "T7", "T8", "T9", "T10",
            "T11", "T12", "T13", "T14", "T15"
          ],
          "placeholder": "Seleccionar tina..."
        },
        {
          "name": "CÓDIGO",
          "type": "text",
          "width": 12,
          "placeholder": "COD-XXX"
        },
        {
          "name": "ESPECIE",
          "type": "select",
          "width": 15,
          "options": ["Tilapia", "Trucha", "Salmón", "Corvina", "Lenguado", "Otro"]
        },
        {
          "name": "PESO BRUTO (kg)",
          "type": "number",
          "width": 15,
          "required": true,
          "min": 0,
          "step": 0.1
        },
        {
          "name": "PESO NETO (kg)",
          "type": "number",
          "width": 15,
          "required": true,
          "min": 0,
          "step": 0.1
        },
        {
          "name": "% RENDIMIENTO",
          "type": "calculated",
          "width": 12,
          "formula": "(PESO NETO / PESO BRUTO) * 100",
          "format": "0.0",
          "readonly": true
        }
      ],
      "enableTotals": true,
      "totalsColumns": ["PESO BRUTO (kg)", "PESO NETO (kg)"],
      "allowAdd": true,
      "allowDelete": true,
      "sortable": true
    },
    
    {
      "type": "summary-table",
      "id": "resumen-tinas",
      "title": "Resumen por Tina (Calculado Automáticamente)",
      "description": "Esta tabla se calcula automáticamente a partir de los datos ingresados arriba",
      "sourceTable": "registro-detallado",
      "groupBy": "TINA",
      "showAllGroups": true,
      "expectedGroups": [
        "T1", "T2", "T3", "T4", "T5", 
        "T6", "T7", "T8", "T9", "T10",
        "T11", "T12", "T13", "T14", "T15"
      ],
      "columns": [
        {
          "name": "TINA",
          "label": "TINA",
          "width": 10
        },
        {
          "name": "NUM_REGISTROS",
          "label": "# REGISTROS",
          "function": "count",
          "sourceColumn": "HORA",
          "width": 12
        },
        {
          "name": "TOTAL_BRUTO",
          "label": "PESO BRUTO TOTAL (kg)",
          "function": "sum",
          "sourceColumn": "PESO BRUTO (kg)",
          "width": 18,
          "format": "0.00"
        },
        {
          "name": "TOTAL_NETO",
          "label": "PESO NETO TOTAL (kg)",
          "function": "sum",
          "sourceColumn": "PESO NETO (kg)",
          "width": 18,
          "format": "0.00"
        },
        {
          "name": "RENDIMIENTO_PROMEDIO",
          "label": "% REND. PROM.",
          "function": "average",
          "sourceColumn": "% RENDIMIENTO",
          "width": 15,
          "format": "0.0"
        },
        {
          "name": "ESPECIES",
          "label": "ESPECIES",
          "function": "concat-unique",
          "sourceColumn": "ESPECIE",
          "width": 20,
          "separator": ", "
        }
      ],
      "showGrandTotal": true,
      "readonly": true,
      "highlightEmptyGroups": true
    }
  ],
  
  "firmas": [
    { "puesto": "ASISTENTE DE PRODUCCIÓN" },
    { "puesto": "SUPERVISOR DE TURNO" },
    { "puesto": "JEFE DE ASEGURAMIENTO DE CALIDAD" }
  ]
}
```

---

## 📊 Ejemplo de Datos Llenados

### Datos de Entrada (headerData):
```json
{
  "Fecha": "2025-01-15",
  "Turno": "Mañana",
  "Responsable": "Juan Pérez",
  "Lote de Proceso": "LP-2025-001"
}
```

### Datos del Cuerpo (bodyData):
```json
{
  "registro-detallado": {
    "rows": [
      { "HORA": "08:00", "TINA": "T1", "CÓDIGO": "COD-001", "ESPECIE": "Tilapia", "PESO BRUTO (kg)": 150.5, "PESO NETO (kg)": 120.3, "% RENDIMIENTO": 80.0 },
      { "HORA": "08:15", "TINA": "T1", "CÓDIGO": "COD-002", "ESPECIE": "Tilapia", "PESO BRUTO (kg)": 160.0, "PESO NETO (kg)": 130.5, "% RENDIMIENTO": 81.6 },
      { "HORA": "08:30", "TINA": "T2", "CÓDIGO": "COD-003", "ESPECIE": "Trucha", "PESO BRUTO (kg)": 200.0, "PESO NETO (kg)": 180.0, "% RENDIMIENTO": 90.0 },
      { "HORA": "08:45", "TINA": "T2", "CÓDIGO": "COD-004", "ESPECIE": "Trucha", "PESO BRUTO (kg)": 210.5, "PESO NETO (kg)": 190.2, "% RENDIMIENTO": 90.4 },
      { "HORA": "09:00", "TINA": "T3", "CÓDIGO": "COD-005", "ESPECIE": "Salmón", "PESO BRUTO (kg)": 250.0, "PESO NETO (kg)": 220.0, "% RENDIMIENTO": 88.0 },
      { "HORA": "09:30", "TINA": "T5", "CÓDIGO": "COD-006", "ESPECIE": "Tilapia", "PESO BRUTO (kg)": 175.0, "PESO NETO (kg)": 145.0, "% RENDIMIENTO": 82.9 },
      { "HORA": "10:00", "TINA": "T5", "CÓDIGO": "COD-007", "ESPECIE": "Tilapia", "PESO BRUTO (kg)": 180.0, "PESO NETO (kg)": 150.0, "% RENDIMIENTO": 83.3 }
    ]
  },
  
  "resumen-tinas": {
    "calculated": true,
    "rows": [
      { "TINA": "T1", "NUM_REGISTROS": 2, "TOTAL_BRUTO": 310.5, "TOTAL_NETO": 250.8, "RENDIMIENTO_PROMEDIO": 80.8, "ESPECIES": "Tilapia" },
      { "TINA": "T2", "NUM_REGISTROS": 2, "TOTAL_BRUTO": 410.5, "TOTAL_NETO": 370.2, "RENDIMIENTO_PROMEDIO": 90.2, "ESPECIES": "Trucha" },
      { "TINA": "T3", "NUM_REGISTROS": 1, "TOTAL_BRUTO": 250.0, "TOTAL_NETO": 220.0, "RENDIMIENTO_PROMEDIO": 88.0, "ESPECIES": "Salmón" },
      { "TINA": "T4", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T5", "NUM_REGISTROS": 2, "TOTAL_BRUTO": 355.0, "TOTAL_NETO": 295.0, "RENDIMIENTO_PROMEDIO": 83.1, "ESPECIES": "Tilapia" },
      { "TINA": "T6", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T7", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T8", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T9", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T10", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T11", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T12", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T13", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T14", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" },
      { "TINA": "T15", "NUM_REGISTROS": 0, "TOTAL_BRUTO": 0.0, "TOTAL_NETO": 0.0, "RENDIMIENTO_PROMEDIO": 0.0, "ESPECIES": "-" }
    ],
    "grandTotal": {
      "TINA": "TOTAL GENERAL",
      "NUM_REGISTROS": 7,
      "TOTAL_BRUTO": 1326.0,
      "TOTAL_NETO": 1136.0,
      "RENDIMIENTO_PROMEDIO": 85.7,
      "ESPECIES": "Tilapia, Trucha, Salmón"
    }
  }
}
```

---

## 🎨 Cómo se ve en el Frontend

### Tabla 1: Registro Detallado
```
┌────────────────────────────────────────────────────────────────────────────────────┐
│ REGISTRO DETALLADO POR TINA                                                        │
│ Registra cada entrada de producción indicando la tina correspondiente              │
├──────┬──────┬─────────┬──────────┬──────────────┬──────────────┬──────────────────┤
│ HORA │ TINA │ CÓDIGO  │ ESPECIE  │ PESO BRUTO   │ PESO NETO    │ % RENDIMIENTO    │
│      │      │         │          │    (kg)      │    (kg)      │                  │
├──────┼──────┼─────────┼──────────┼──────────────┼──────────────┼──────────────────┤
│ 8:00 │  T1  │COD-001  │ Tilapia  │    150.5     │    120.3     │      80.0        │
│ 8:15 │  T1  │COD-002  │ Tilapia  │    160.0     │    130.5     │      81.6        │
│ 8:30 │  T2  │COD-003  │ Trucha   │    200.0     │    180.0     │      90.0        │
│ 8:45 │  T2  │COD-004  │ Trucha   │    210.5     │    190.2     │      90.4        │
│ 9:00 │  T3  │COD-005  │ Salmón   │    250.0     │    220.0     │      88.0        │
│ 9:30 │  T5  │COD-006  │ Tilapia  │    175.0     │    145.0     │      82.9        │
│10:00 │  T5  │COD-007  │ Tilapia  │    180.0     │    150.0     │      83.3        │
├──────┴──────┴─────────┴──────────┼──────────────┼──────────────┼──────────────────┤
│                    TOTALES:       │   1,326.0    │   1,136.0    │      85.7        │
└───────────────────────────────────┴──────────────┴──────────────┴──────────────────┘
          [+ Agregar Fila]
```

### Tabla 2: Resumen Automático
```
┌────────────────────────────────────────────────────────────────────────────────────┐
│ RESUMEN POR TINA (Calculado Automáticamente)                                       │
│ Esta tabla se calcula automáticamente a partir de los datos ingresados arriba      │
├──────┬──────────────┬─────────────────────┬─────────────────────┬────────────┬─────┤
│ TINA │ # REGISTROS  │ PESO BRUTO TOTAL(kg)│ PESO NETO TOTAL(kg) │ % REND.    │ ESP │
│      │              │                     │                     │ PROM.      │     │
├──────┼──────────────┼─────────────────────┼─────────────────────┼────────────┼─────┤
│  T1  │      2       │       310.5         │       250.8         │   80.8     │Tilap│
│  T2  │      2       │       410.5         │       370.2         │   90.2     │Truch│
│  T3  │      1       │       250.0         │       220.0         │   88.0     │Salmó│
│  T4  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│  T5  │      2       │       355.0         │       295.0         │   83.1     │Tilap│
│  T6  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│  T7  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│  T8  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│  T9  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│ T10  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│ T11  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│ T12  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│ T13  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│ T14  │      0       │         0.0         │         0.0         │    0.0     │  -  │
│ T15  │      0       │         0.0         │         0.0         │    0.0     │  -  │
├──────┴──────────────┼─────────────────────┼─────────────────────┼────────────┼─────┤
│     TOTAL GENERAL   │      1,326.0        │      1,136.0        │   85.7     │T,T,S│
└─────────────────────┴─────────────────────┴─────────────────────┴────────────┴─────┘
```

---

## ✅ Ventajas de esta Solución

1. **Una Sola Tabla de Entrada**: No tienes que crear 15 tablas
2. **Resumen Automático**: Se calcula en tiempo real
3. **Muestra Todas las Tinas**: Incluso las que no tienen datos (con 0)
4. **Fácil de Filtrar**: Puedes buscar por tina, especie, hora, etc.
5. **Totales Generales**: Ve el panorama completo
6. **Exportable a PDF/Excel**: Ambas tablas se exportan
7. **Escalable**: Puedes agregar o quitar tinas fácilmente

---

## 🔍 Consultas Útiles

### Obtener solo datos de una tina específica:
```javascript
// En el frontend o backend
const datosTina5 = formulario.bodyData["registro-detallado"].rows
  .filter(row => row.TINA === "T5");

console.log(datosTina5);
// [
//   { "HORA": "09:30", "TINA": "T5", "PESO NETO (kg)": 145.0 },
//   { "HORA": "10:00", "TINA": "T5", "PESO NETO (kg)": 150.0 }
// ]
```

### Obtener resumen de una tina:
```javascript
const resumenT5 = formulario.bodyData["resumen-tinas"].rows
  .find(row => row.TINA === "T5");

console.log(resumenT5);
// { "TINA": "T5", "NUM_REGISTROS": 2, "TOTAL_NETO": 295.0, ... }
```

---

## 📱 Visualizaciones Adicionales

### Gráfico de Barras (Peso Neto por Tina):
```
      kg
300 │     ██
250 │ ██  ██     ██
200 │ ██  ██ ██  ██
150 │ ██  ██ ██  ██
100 │ ██  ██ ██  ██
 50 │ ██  ██ ██  ██
  0 └─┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──
     T1  T2 T3 T4 T5 T6 T7 T8 T9T10T11T12T13T14T15
```

### Tabla de Rendimiento (Color-coded):
```
🟢 T2:  90.2% (Excelente)
🟢 T3:  88.0% (Muy Bueno)
🟡 T5:  83.1% (Bueno)
🟡 T1:  80.8% (Aceptable)
⚪ T4:   0.0% (Sin datos)
```

---

¿Te gusta esta solución? ¿Quieres que cree el template completo en la base de datos o prefieres ajustar algo? 🚀
