# 🎯 Sistema de 15 Tinas - Layout Horizontal con Múltiples Pesadas

## 📐 Diseño Actualizado

### ✅ NUEVA ESTRUCTURA (Horizontal - Pesos a la Derecha)

```
┌──────┬──────┬──────────┬──────────┬──────────┬──────────┬──────────┬────────────┐
│ HORA │ TINA │ PESO 1   │ PESO 2   │ PESO 3   │ PESO 4   │ PESO 5   │ TOTAL      │
│      │      │  (kg)    │  (kg)    │  (kg)    │  (kg)    │  (kg)    │  (kg)      │
├──────┼──────┼──────────┼──────────┼──────────┼──────────┼──────────┼────────────┤
│ 8:00 │  T1  │  120.5   │  150.3   │  180.0   │   95.2   │  110.0   │  656.0  ✓  │← Total fila
│ 8:30 │  T2  │  200.0   │  210.5   │  190.0   │    0.0   │    0.0   │  600.5  ✓  │
│ 9:00 │  T3  │  250.0   │  220.0   │  230.5   │  240.0   │  260.0   │ 1200.5  ✓  │
│ 9:30 │  T5  │  175.0   │  180.0   │    0.0   │    0.0   │    0.0   │  355.0  ✓  │
│10:00 │  T1  │  165.0   │  170.5   │  155.0   │  160.0   │    0.0   │  650.5  ✓  │← T1 otra vez
│10:30 │  T7  │  300.0   │  285.0   │  290.0   │  310.0   │  295.0   │ 1480.0  ✓  │
├──────┴──────┼──────────┼──────────┼──────────┼──────────┼──────────┼────────────┤
│   TOTALES:  │ 1210.5   │ 1216.3   │ 1045.5   │  805.2   │  665.0   │ 4942.5     │
└─────────────┴──────────┴──────────┴──────────┴──────────┴──────────┴────────────┘
                                                                         ↑
                                                              TOTAL se calcula automático
```

### 📊 RESUMEN POR TINA (Segunda Tabla)

```
┌──────┬─────────────┬─────────────┬─────────────┬─────────────┬─────────────┬─────────────┬─────────────────────┐
│ TINA │ # ENTRADAS  │ TOTAL       │ TOTAL       │ TOTAL       │ TOTAL       │ TOTAL       │ TOTAL ACUMULADO     │
│      │             │ PESO 1      │ PESO 2      │ PESO 3      │ PESO 4      │ PESO 5      │ (kg)                │
├──────┼─────────────┼─────────────┼─────────────┼─────────────┼─────────────┼─────────────┼─────────────────────┤
│  T1  │      2      │    285.5    │    320.8    │    335.0    │    255.2    │    110.0    │    1,306.5  ⭐     │
│  T2  │      1      │    200.0    │    210.5    │    190.0    │      0.0    │      0.0    │      600.5         │
│  T3  │      1      │    250.0    │    220.0    │    230.5    │    240.0    │    260.0    │    1,200.5         │
│  T4  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│  T5  │      1      │    175.0    │    180.0    │      0.0    │      0.0    │      0.0    │      355.0         │
│  T6  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│  T7  │      1      │    300.0    │    285.0    │    290.0    │    310.0    │    295.0    │    1,480.0         │
│  T8  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│  T9  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│ T10  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│ T11  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│ T12  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│ T13  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│ T14  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
│ T15  │      0      │      0.0    │      0.0    │      0.0    │      0.0    │      0.0    │        0.0         │
├──────┴─────────────┼─────────────┼─────────────┼─────────────┼─────────────┼─────────────┼─────────────────────┤
│      TOTAL GENERAL │   1,210.5   │   1,216.3   │   1,045.5   │     805.2   │     665.0   │    4,942.5  🏆     │
└────────────────────┴─────────────┴─────────────┴─────────────┴─────────────┴─────────────┴─────────────────────┘
```

---

## 🎯 Explicación del Flujo

### Paso 1: Registrar cada entrada de tina
```javascript
// Ejemplo: A las 8:00 procesas T1
{
  "HORA": "08:00",
  "TINA": "T1",
  "PESO 1 (kg)": 120.5,
  "PESO 2 (kg)": 150.3,
  "PESO 3 (kg)": 180.0,
  "PESO 4 (kg)": 95.2,
  "PESO 5 (kg)": 110.0,
  "TOTAL (kg)": 656.0  // ← Se calcula automáticamente: 120.5+150.3+180+95.2+110
}
```

### Paso 2: Si la misma tina tiene otra entrada, agregar otra fila
```javascript
// A las 10:00 vuelves a procesar T1
{
  "HORA": "10:00",
  "TINA": "T1",
  "PESO 1 (kg)": 165.0,
  "PESO 2 (kg)": 170.5,
  "PESO 3 (kg)": 155.0,
  "PESO 4 (kg)": 160.0,
  "PESO 5 (kg)": 0.0,    // ← Si no hay peso 5, dejar en 0
  "TOTAL (kg)": 650.5    // ← 165+170.5+155+160+0
}
```

### Paso 3: Resumen calcula el total acumulado por tina
```javascript
// Para T1:
{
  "TINA": "T1",
  "NUM_ENTRADAS": 2,                           // ← 2 filas con T1
  "TOTAL_PESO1": 285.5,                        // ← 120.5 + 165.0
  "TOTAL_PESO2": 320.8,                        // ← 150.3 + 170.5
  "TOTAL_PESO3": 335.0,                        // ← 180.0 + 155.0
  "TOTAL_PESO4": 255.2,                        // ← 95.2 + 160.0
  "TOTAL_PESO5": 110.0,                        // ← 110.0 + 0.0
  "TOTAL_ACUMULADO (kg)": 1306.5  // ← 656.0 + 650.5 (suma de TOTALes)
}
```

---

## 💡 Ventajas de este Diseño

### ✅ Ventajas:
1. **Pesos visibles a la derecha**: Fácil de leer horizontalmente
2. **Múltiples pesadas por entrada**: Hasta 5 pesos por fila
3. **Total por fila automático**: No necesitas calcularlo manualmente
4. **Misma tina múltiples veces**: Puedes registrar T1 varias veces
5. **Resumen consolidado**: Ve el total acumulado por cada tina
6. **Flexible**: Si solo usas 2-3 pesos, deja los demás en 0

### 📊 Casos de Uso:
- **Pesada simple**: Usa solo PESO 1, deja el resto en 0
- **Pesada múltiple**: Usa PESO 1, 2, 3, 4, 5 según necesites
- **Múltiples lotes**: Agrega otra fila con la misma tina

---

## 🚀 Código Frontend para Cálculo Automático

### Calcular TOTAL por fila:
```javascript
function calcularTotalFila(row) {
  const peso1 = parseFloat(row["PESO 1 (kg)"]) || 0;
  const peso2 = parseFloat(row["PESO 2 (kg)"]) || 0;
  const peso3 = parseFloat(row["PESO 3 (kg)"]) || 0;
  const peso4 = parseFloat(row["PESO 4 (kg)"]) || 0;
  const peso5 = parseFloat(row["PESO 5 (kg)"]) || 0;
  
  return (peso1 + peso2 + peso3 + peso4 + peso5).toFixed(2);
}

// Uso:
row["TOTAL (kg)"] = calcularTotalFila(row);
```

### Calcular RESUMEN por TINA:
```javascript
function calcularResumenPorTina(registros) {
  const tinas = ["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12", "T13", "T14", "T15"];
  
  return tinas.map(tina => {
    const registrosTina = registros.filter(r => r.TINA === tina);
    
    if (registrosTina.length === 0) {
      return {
        TINA: tina,
        NUM_ENTRADAS: 0,
        TOTAL_PESO1: 0,
        TOTAL_PESO2: 0,
        TOTAL_PESO3: 0,
        TOTAL_PESO4: 0,
        TOTAL_PESO5: 0,
        TOTAL_ACUMULADO: 0
      };
    }
    
    const totalPeso1 = registrosTina.reduce((sum, r) => sum + (parseFloat(r["PESO 1 (kg)"]) || 0), 0);
    const totalPeso2 = registrosTina.reduce((sum, r) => sum + (parseFloat(r["PESO 2 (kg)"]) || 0), 0);
    const totalPeso3 = registrosTina.reduce((sum, r) => sum + (parseFloat(r["PESO 3 (kg)"]) || 0), 0);
    const totalPeso4 = registrosTina.reduce((sum, r) => sum + (parseFloat(r["PESO 4 (kg)"]) || 0), 0);
    const totalPeso5 = registrosTina.reduce((sum, r) => sum + (parseFloat(r["PESO 5 (kg)"]) || 0), 0);
    const totalAcumulado = registrosTina.reduce((sum, r) => sum + (parseFloat(r["TOTAL (kg)"]) || 0), 0);
    
    return {
      TINA: tina,
      NUM_ENTRADAS: registrosTina.length,
      TOTAL_PESO1: totalPeso1.toFixed(2),
      TOTAL_PESO2: totalPeso2.toFixed(2),
      TOTAL_PESO3: totalPeso3.toFixed(2),
      TOTAL_PESO4: totalPeso4.toFixed(2),
      TOTAL_PESO5: totalPeso5.toFixed(2),
      TOTAL_ACUMULADO: totalAcumulado.toFixed(2)
    };
  });
}

// Uso:
const resumen = calcularResumenPorTina(formData.body["registro-horizontal"].rows);
formData.body["resumen-tinas"].rows = resumen;
```

---

## 📝 Ejemplo de Datos Completo

### Datos de Entrada:
```json
{
  "headerData": {
    "Fecha": "2025-01-15",
    "Turno": "Mañana",
    "Responsable": "Juan Pérez",
    "Lote de Proceso": "LP-2025-001"
  },
  "bodyData": {
    "registro-horizontal": {
      "rows": [
        { "HORA": "08:00", "TINA": "T1", "PESO 1 (kg)": 120.5, "PESO 2 (kg)": 150.3, "PESO 3 (kg)": 180.0, "PESO 4 (kg)": 95.2, "PESO 5 (kg)": 110.0, "TOTAL (kg)": 656.0 },
        { "HORA": "08:30", "TINA": "T2", "PESO 1 (kg)": 200.0, "PESO 2 (kg)": 210.5, "PESO 3 (kg)": 190.0, "PESO 4 (kg)": 0.0, "PESO 5 (kg)": 0.0, "TOTAL (kg)": 600.5 },
        { "HORA": "09:00", "TINA": "T3", "PESO 1 (kg)": 250.0, "PESO 2 (kg)": 220.0, "PESO 3 (kg)": 230.5, "PESO 4 (kg)": 240.0, "PESO 5 (kg)": 260.0, "TOTAL (kg)": 1200.5 },
        { "HORA": "09:30", "TINA": "T5", "PESO 1 (kg)": 175.0, "PESO 2 (kg)": 180.0, "PESO 3 (kg)": 0.0, "PESO 4 (kg)": 0.0, "PESO 5 (kg)": 0.0, "TOTAL (kg)": 355.0 },
        { "HORA": "10:00", "TINA": "T1", "PESO 1 (kg)": 165.0, "PESO 2 (kg)": 170.5, "PESO 3 (kg)": 155.0, "PESO 4 (kg)": 160.0, "PESO 5 (kg)": 0.0, "TOTAL (kg)": 650.5 },
        { "HORA": "10:30", "TINA": "T7", "PESO 1 (kg)": 300.0, "PESO 2 (kg)": 285.0, "PESO 3 (kg)": 290.0, "PESO 4 (kg)": 310.0, "PESO 5 (kg)": 295.0, "TOTAL (kg)": 1480.0 }
      ]
    },
    "resumen-tinas": {
      "calculated": true,
      "rows": [
        { "TINA": "T1", "NUM_ENTRADAS": 2, "TOTAL_PESO1": 285.5, "TOTAL_PESO2": 320.8, "TOTAL_PESO3": 335.0, "TOTAL_PESO4": 255.2, "TOTAL_PESO5": 110.0, "TOTAL_ACUMULADO": 1306.5 },
        { "TINA": "T2", "NUM_ENTRADAS": 1, "TOTAL_PESO1": 200.0, "TOTAL_PESO2": 210.5, "TOTAL_PESO3": 190.0, "TOTAL_PESO4": 0.0, "TOTAL_PESO5": 0.0, "TOTAL_ACUMULADO": 600.5 },
        { "TINA": "T3", "NUM_ENTRADAS": 1, "TOTAL_PESO1": 250.0, "TOTAL_PESO2": 220.0, "TOTAL_PESO3": 230.5, "TOTAL_PESO4": 240.0, "TOTAL_PESO5": 260.0, "TOTAL_ACUMULADO": 1200.5 },
        { "TINA": "T4", "NUM_ENTRADAS": 0, "TOTAL_PESO1": 0.0, "TOTAL_PESO2": 0.0, "TOTAL_PESO3": 0.0, "TOTAL_PESO4": 0.0, "TOTAL_PESO5": 0.0, "TOTAL_ACUMULADO": 0.0 },
        { "TINA": "T5", "NUM_ENTRADAS": 1, "TOTAL_PESO1": 175.0, "TOTAL_PESO2": 180.0, "TOTAL_PESO3": 0.0, "TOTAL_PESO4": 0.0, "TOTAL_PESO5": 0.0, "TOTAL_ACUMULADO": 355.0 },
        // ... resto de tinas con 0
        { "TINA": "T7", "NUM_ENTRADAS": 1, "TOTAL_PESO1": 300.0, "TOTAL_PESO2": 285.0, "TOTAL_PESO3": 290.0, "TOTAL_PESO4": 310.0, "TOTAL_PESO5": 295.0, "TOTAL_ACUMULADO": 1480.0 }
      ],
      "grandTotal": {
        "TINA": "TOTAL GENERAL",
        "NUM_ENTRADAS": 6,
        "TOTAL_PESO1": 1210.5,
        "TOTAL_PESO2": 1216.3,
        "TOTAL_PESO3": 1045.5,
        "TOTAL_PESO4": 805.2,
        "TOTAL_PESO5": 665.0,
        "TOTAL_ACUMULADO": 4942.5
      }
    }
  }
}
```

---

## 🎯 Cómo Usar

### 1. Crear Template:
```http
POST http://localhost:5074/api/TemplatePresets/create-15-tinas
```

### 2. Llenar Formulario:
- Registra HORA y selecciona TINA
- Ingresa pesos en las columnas 1, 2, 3, 4, 5 (las que necesites)
- TOTAL se calcula automáticamente
- Puedes agregar múltiples filas para la misma tina

### 3. Ver Resumen:
- El resumen muestra el TOTAL ACUMULADO por cada tina
- Suma TODOS los TOTALes de cada fila de esa tina
- Muestra las 15 tinas (incluso si no tienen datos)

---

## ✅ Validaciones Recomendadas

```javascript
// 1. Al menos un peso debe ser mayor a 0
function validarPesos(row) {
  const pesos = [
    parseFloat(row["PESO 1 (kg)"]) || 0,
    parseFloat(row["PESO 2 (kg)"]) || 0,
    parseFloat(row["PESO 3 (kg)"]) || 0,
    parseFloat(row["PESO 4 (kg)"]) || 0,
    parseFloat(row["PESO 5 (kg)"]) || 0
  ];
  
  const total = pesos.reduce((sum, p) => sum + p, 0);
  
  if (total === 0) {
    alert('Debes ingresar al menos un peso mayor a 0');
    return false;
  }
  
  return true;
}

// 2. Validar que TINA esté seleccionada
function validarTina(row) {
  if (!row.TINA || row.TINA === "") {
    alert('Debes seleccionar una tina');
    return false;
  }
  return true;
}
```

---

**¡Listo! 🎉** Ahora tienes un sistema horizontal donde los pesos están a la derecha y se calcula el total automáticamente por fila y por tina.
