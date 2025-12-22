# 🎯 Sistema 15 Tinas - Diseño Matricial (Vertical/Horizontal)

## 📐 Diseño EXACTO según tu imagen

**UNA SOLA TABLA** con formato matricial:
- **Filas = HORAS** (vertical)
- **Columnas = TINAS** (horizontal: T1, T2, T3, ..., T15)
- **Última columna = TOTAL** (suma automática de todas las tinas en esa hora)
- **Última fila = TOTALES POR TINA** (suma de todos los pesos de cada tina)

```
┌──────┬────────┬────────┬────────┬────────┬────────┬────────┬─────┬────────┬──────────┐
│ HORA │   T1   │   T2   │   T3   │   T4   │   T5   │   T6   │ ... │  T15   │  TOTAL   │
│      │  (kg)  │  (kg)  │  (kg)  │  (kg)  │  (kg)  │  (kg)  │     │  (kg)  │   (kg)   │
├──────┼────────┼────────┼────────┼────────┼────────┼────────┼─────┼────────┼──────────┤
│08:00 │ 120.5  │ 150.3  │  95.2  │ 180.0  │ 140.0  │ 110.5  │ ... │   0.0  │  796.5   │
│08:30 │ 165.0  │ 170.5  │ 160.0  │ 155.0  │ 145.0  │ 135.0  │ ... │   0.0  │  930.5   │
│09:00 │ 200.0  │ 210.0  │ 185.5  │ 190.0  │ 175.0  │ 165.0  │ ... │  85.0  │ 1,210.5  │
│09:30 │ 220.0  │ 230.0  │ 225.0  │ 215.0  │ 205.0  │ 195.0  │ ... │ 105.0  │ 1,395.0  │
│10:00 │   0.0  │ 150.0  │ 175.0  │   0.0  │ 160.0  │ 140.0  │ ... │   0.0  │  625.0   │
│10:30 │   0.0  │   0.0  │ 130.0  │ 145.0  │   0.0  │ 125.0  │ ... │  95.0  │  495.0   │
├──────┼────────┼────────┼────────┼────────┼────────┼────────┼─────┼────────┼──────────┤
│TOTAL │ 705.5  │ 910.8  │ 970.7  │ 885.0  │ 825.0  │ 870.5  │ ... │ 285.0  │ 5,452.5  │
│TINA  │        │        │        │        │        │        │     │        │    🏆    │
└──────┴────────┴────────┴────────┴────────┴────────┴────────┴─────┴────────┴──────────┘
```

---

## 🎨 Ventajas del Diseño Matricial

### ✅ **Compacto y Eficiente**
- ✅ **Una sola tabla** (no 15 tablas separadas)
- ✅ **Vista completa** de todas las tinas en un solo lugar
- ✅ **Poco espacio vertical** (solo creces por hora, no por tina)
- ✅ **Scrollable horizontal** para las 15 columnas de tinas

### ✅ **Fácil de Usar**
- ✅ Cada **fila = una hora**
- ✅ Cada **columna = una tina**
- ✅ Llenas los pesos **por hora** para cada tina activa
- ✅ Dejas en **0.0** las tinas sin actividad

### ✅ **Cálculos Automáticos**
- ✅ **TOTAL por hora** (última columna) = suma de todas las tinas en esa hora
- ✅ **TOTAL por tina** (última fila) = suma de todos los pesos de esa tina
- ✅ **TOTAL GENERAL** = suma de todos los pesos del día

---

## 📊 Estructura del Template

### 🔹 Columnas de la Tabla:

```json
{
  "columns": [
    { "name": "HORA", "type": "time", "width": 8, "required": true, "fixed": true },
    { "name": "T1", "type": "number", "width": 6, "min": 0, "step": 0.1, "placeholder": "0.0", "label": "T1 (kg)" },
    { "name": "T2", "type": "number", "width": 6, "min": 0, "step": 0.1, "placeholder": "0.0", "label": "T2 (kg)" },
    { "name": "T3", "type": "number", "width": 6, "min": 0, "step": 0.1, "placeholder": "0.0", "label": "T3 (kg)" },
    // ... T4 a T14
    { "name": "T15", "type": "number", "width": 6, "min": 0, "step": 0.1, "placeholder": "0.0", "label": "T15 (kg)" },
    { 
      "name": "TOTAL", 
      "type": "calculated", 
      "width": 8, 
      "formula": "T1+T2+T3+T4+T5+T6+T7+T8+T9+T10+T11+T12+T13+T14+T15",
      "format": "0.00",
      "readonly": true,
      "bold": true,
      "label": "TOTAL (kg)"
    }
  ],
  "enableColumnTotals": true,
  "columnTotalsLabel": "TOTAL POR TINA",
  "columnTotalsColumns": ["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12", "T13", "T14", "T15", "TOTAL"],
  "scrollableColumns": true
}
```

---

## 💾 Ejemplo de Datos

### Datos Completos:
```json
{
  "bodyData": {
    "registro-matricial": {
      "rows": [
        { 
          "HORA": "08:00", 
          "T1": 120.5, "T2": 150.3, "T3": 95.2, "T4": 180.0, "T5": 140.0,
          "T6": 110.5, "T7": 135.0, "T8": 145.5, "T9": 155.0, "T10": 165.0,
          "T11": 0.0, "T12": 0.0, "T13": 0.0, "T14": 0.0, "T15": 0.0,
          "TOTAL": 1397.0
        },
        { 
          "HORA": "08:30", 
          "T1": 165.0, "T2": 170.5, "T3": 160.0, "T4": 155.0, "T5": 145.0,
          "T6": 135.0, "T7": 140.0, "T8": 150.0, "T9": 160.0, "T10": 170.0,
          "T11": 0.0, "T12": 0.0, "T13": 0.0, "T14": 0.0, "T15": 0.0,
          "TOTAL": 1550.5
        },
        { 
          "HORA": "09:00", 
          "T1": 200.0, "T2": 210.0, "T3": 185.5, "T4": 190.0, "T5": 175.0,
          "T6": 165.0, "T7": 170.0, "T8": 180.0, "T9": 185.0, "T10": 190.0,
          "T11": 75.0, "T12": 80.0, "T13": 0.0, "T14": 0.0, "T15": 85.0,
          "TOTAL": 2090.5
        }
      ],
      "columnTotals": {
        "T1": 485.5,
        "T2": 530.8,
        "T3": 440.7,
        "T4": 525.0,
        "T5": 460.0,
        "T6": 410.5,
        "T7": 445.0,
        "T8": 475.5,
        "T9": 500.0,
        "T10": 525.0,
        "T11": 75.0,
        "T12": 80.0,
        "T13": 0.0,
        "T14": 0.0,
        "T15": 85.0,
        "TOTAL": 5038.0
      }
    }
  }
}
```

---

## 🚀 Código Frontend

### 1️⃣ Calcular TOTAL por Fila (por hora):
```javascript
function calcularTotalFila(row) {
  let total = 0;
  
  for (let i = 1; i <= 15; i++) {
    const tinaKey = `T${i}`;
    total += parseFloat(row[tinaKey]) || 0;
  }
  
  return total.toFixed(2);
}

// Uso automático al cambiar valores:
const handlePesoChange = (rowIndex, tinaNum, value) => {
  const newRows = [...formData.rows];
  newRows[rowIndex][`T${tinaNum}`] = parseFloat(value) || 0;
  newRows[rowIndex].TOTAL = calcularTotalFila(newRows[rowIndex]);
  
  setFormData({ ...formData, rows: newRows });
};
```

### 2️⃣ Calcular TOTAL por Columna (por tina):
```javascript
function calcularTotalesPorTina(rows) {
  const totales = {
    T1: 0, T2: 0, T3: 0, T4: 0, T5: 0,
    T6: 0, T7: 0, T8: 0, T9: 0, T10: 0,
    T11: 0, T12: 0, T13: 0, T14: 0, T15: 0,
    TOTAL: 0
  };
  
  rows.forEach(row => {
    for (let i = 1; i <= 15; i++) {
      const tinaKey = `T${i}`;
      totales[tinaKey] += parseFloat(row[tinaKey]) || 0;
    }
    totales.TOTAL += parseFloat(row.TOTAL) || 0;
  });
  
  // Redondear a 2 decimales
  Object.keys(totales).forEach(key => {
    totales[key] = parseFloat(totales[key].toFixed(2));
  });
  
  return totales;
}
```

### 3️⃣ Validar Datos:
```javascript
function validarFormulario(rows) {
  const errores = [];
  
  // Validar que cada fila tenga hora
  rows.forEach((row, index) => {
    if (!row.HORA) {
      errores.push(`Fila ${index + 1}: La hora es obligatoria`);
    }
  });
  
  // Validar que al menos una tina tenga peso > 0
  const tieneAlgunPeso = rows.some(row => {
    for (let i = 1; i <= 15; i++) {
      if ((parseFloat(row[`T${i}`]) || 0) > 0) return true;
    }
    return false;
  });
  
  if (!tieneAlgunPeso) {
    errores.push("Debe haber al menos una tina con peso > 0");
  }
  
  return errores;
}
```

---

## 🎨 Componente React Completo

```jsx
import React, { useState, useEffect } from 'react';

function FormularioTinasMatricial() {
  const [formData, setFormData] = useState({
    fecha: new Date().toISOString().split('T')[0],
    turno: 'Mañana',
    responsable: '',
    lote: '',
    rows: [
      { 
        HORA: '', 
        T1: 0, T2: 0, T3: 0, T4: 0, T5: 0, T6: 0, T7: 0, T8: 0, 
        T9: 0, T10: 0, T11: 0, T12: 0, T13: 0, T14: 0, T15: 0, 
        TOTAL: 0 
      }
    ]
  });
  
  const [columnTotals, setColumnTotals] = useState({});
  
  // Recalcular totales cuando cambien las filas
  useEffect(() => {
    calcularTotales();
  }, [formData.rows]);
  
  const calcularTotales = () => {
    const totales = {
      T1: 0, T2: 0, T3: 0, T4: 0, T5: 0, T6: 0, T7: 0, T8: 0,
      T9: 0, T10: 0, T11: 0, T12: 0, T13: 0, T14: 0, T15: 0, TOTAL: 0
    };
    
    formData.rows.forEach(row => {
      for (let i = 1; i <= 15; i++) {
        const key = `T${i}`;
        totales[key] += parseFloat(row[key]) || 0;
      }
      totales.TOTAL += parseFloat(row.TOTAL) || 0;
    });
    
    Object.keys(totales).forEach(key => {
      totales[key] = parseFloat(totales[key].toFixed(2));
    });
    
    setColumnTotals(totales);
  };
  
  const handlePesoChange = (rowIndex, tinaNum, value) => {
    const newRows = [...formData.rows];
    newRows[rowIndex][`T${tinaNum}`] = parseFloat(value) || 0;
    
    // Calcular TOTAL de la fila
    let totalFila = 0;
    for (let i = 1; i <= 15; i++) {
      totalFila += parseFloat(newRows[rowIndex][`T${i}`]) || 0;
    }
    newRows[rowIndex].TOTAL = parseFloat(totalFila.toFixed(2));
    
    setFormData({ ...formData, rows: newRows });
  };
  
  const agregarFila = () => {
    const nuevaFila = {
      HORA: new Date().toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit' }),
      T1: 0, T2: 0, T3: 0, T4: 0, T5: 0, T6: 0, T7: 0, T8: 0,
      T9: 0, T10: 0, T11: 0, T12: 0, T13: 0, T14: 0, T15: 0,
      TOTAL: 0
    };
    setFormData({ ...formData, rows: [...formData.rows, nuevaFila] });
  };
  
  const eliminarFila = (index) => {
    const newRows = formData.rows.filter((_, i) => i !== index);
    setFormData({ ...formData, rows: newRows });
  };
  
  return (
    <div className="formulario-tinas-matricial">
      <h2>📊 Registro de Producción - 15 Tinas (Matricial)</h2>
      
      {/* Header Fields */}
      <div className="header-fields">
        <input 
          type="date" 
          value={formData.fecha} 
          onChange={(e) => setFormData({ ...formData, fecha: e.target.value })} 
        />
        <select 
          value={formData.turno}
          onChange={(e) => setFormData({ ...formData, turno: e.target.value })}
        >
          <option>Mañana</option>
          <option>Tarde</option>
          <option>Noche</option>
        </select>
        <input 
          type="text" 
          placeholder="Responsable" 
          value={formData.responsable}
          onChange={(e) => setFormData({ ...formData, responsable: e.target.value })}
        />
        <input 
          type="text" 
          placeholder="Lote de Proceso" 
          value={formData.lote}
          onChange={(e) => setFormData({ ...formData, lote: e.target.value })}
        />
      </div>
      
      {/* Tabla Matricial */}
      <div className="table-container" style={{ overflowX: 'auto' }}>
        <table className="tabla-matricial">
          <thead>
            <tr>
              <th style={{ position: 'sticky', left: 0, zIndex: 10 }}>HORA</th>
              {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15].map(num => (
                <th key={num}>T{num}<br/>(kg)</th>
              ))}
              <th style={{ fontWeight: 'bold' }}>TOTAL<br/>(kg)</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {formData.rows.map((row, rowIndex) => (
              <tr key={rowIndex}>
                <td style={{ position: 'sticky', left: 0, backgroundColor: '#f8f9fa', zIndex: 5 }}>
                  <input 
                    type="time" 
                    value={row.HORA}
                    onChange={(e) => {
                      const newRows = [...formData.rows];
                      newRows[rowIndex].HORA = e.target.value;
                      setFormData({ ...formData, rows: newRows });
                    }}
                    style={{ width: '80px' }}
                  />
                </td>
                {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15].map(tinaNum => (
                  <td key={tinaNum}>
                    <input 
                      type="number" 
                      value={row[`T${tinaNum}`]} 
                      onChange={(e) => handlePesoChange(rowIndex, tinaNum, e.target.value)}
                      step="0.1"
                      min="0"
                      placeholder="0.0"
                      style={{ width: '60px' }}
                    />
                  </td>
                ))}
                <td style={{ fontWeight: 'bold', backgroundColor: '#e9ecef' }}>
                  {row.TOTAL.toFixed(2)}
                </td>
                <td>
                  <button onClick={() => eliminarFila(rowIndex)}>🗑️</button>
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr style={{ fontWeight: 'bold', backgroundColor: '#dee2e6' }}>
              <td style={{ position: 'sticky', left: 0, zIndex: 5 }}>TOTAL TINA</td>
              {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15].map(num => (
                <td key={num}>{(columnTotals[`T${num}`] || 0).toFixed(2)}</td>
              ))}
              <td style={{ backgroundColor: '#ffc107', fontSize: '1.1em' }}>
                {(columnTotals.TOTAL || 0).toFixed(2)} 🏆
              </td>
              <td></td>
            </tr>
          </tfoot>
        </table>
      </div>
      
      <button onClick={agregarFila} className="btn-primary">+ Agregar Hora</button>
      
      {/* Resumen */}
      <div className="resumen">
        <h3>📊 Resumen</h3>
        <p><strong>Total de Registros:</strong> {formData.rows.length} horas</p>
        <p><strong>Tinas Activas:</strong> {
          [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15].filter(n => 
            (columnTotals[`T${n}`] || 0) > 0
          ).length
        } / 15</p>
        <p><strong>Producción Total del Turno:</strong> {(columnTotals.TOTAL || 0).toFixed(2)} kg 🏆</p>
      </div>
    </div>
  );
}

export default FormularioTinasMatricial;
```

---

## 📝 CSS Recomendado

```css
.tabla-matricial {
  border-collapse: collapse;
  width: 100%;
  font-size: 0.9em;
}

.tabla-matricial th,
.tabla-matricial td {
  border: 1px solid #dee2e6;
  padding: 8px;
  text-align: center;
}

.tabla-matricial thead th {
  background-color: #007bff;
  color: white;
  position: sticky;
  top: 0;
  z-index: 10;
}

.tabla-matricial input[type="time"],
.tabla-matricial input[type="number"] {
  padding: 4px;
  border: 1px solid #ced4da;
  border-radius: 4px;
  text-align: center;
}

.tabla-matricial tfoot td {
  font-weight: bold;
  background-color: #dee2e6;
}

.table-container {
  max-height: 600px;
  overflow-y: auto;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
  border-radius: 8px;
}
```

---

## ✅ Ventajas de Este Diseño

| ✅ Ventaja | Descripción |
|-----------|-------------|
| **Compacto** | Solo crece verticalmente por HORA, no por tina |
| **Una tabla** | No necesitas 15 tablas separadas |
| **Vista completa** | Ves todas las tinas al mismo tiempo |
| **Scrollable** | Scroll horizontal para las 15 columnas |
| **Totales visibles** | Última fila y última columna con totales |
| **Cálculo automático** | TOTAL por hora y por tina automático |
| **Fácil de llenar** | Una fila por hora, llenas solo las tinas activas |

---

## 🎯 Endpoint para Crear

```bash
POST http://localhost:5074/api/TemplatePresets/create-15-tinas-vertical
Content-Type: application/json

{}
```

**Respuesta:**
```json
{
  "templateID": 123,
  "codigo": "FRM-TINAS-15-VERTICAL",
  "nombre": "Registro de Producción - 15 Tinas (Vertical/Matricial)",
  "version": "04-00",
  "createdAt": "2025-12-22T..."
}
```

---

¡Este es el diseño EXACTO de tu imagen! 🎯
