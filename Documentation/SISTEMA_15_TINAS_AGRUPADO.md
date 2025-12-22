# 🎯 Sistema de 15 Tinas - Diseño Agrupado por Tina

## 📐 Nuevo Diseño: Una Sección por Cada Tina

Cada tina tiene su propia tabla independiente con múltiples registros de hora y pesos.

```
┌─────────────────────────────────────────────────────────────┐
│ 🔵 TINA T1                                                   │
├──────────┬─────────┬─────────┬─────────┬─────────┬──────────┤
│   HORA   │ PESO #1 │ PESO #2 │ PESO #3 │ PESO #4 │  TOTAL   │
├──────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│  08:00   │  120.5  │  150.3  │  180.0  │   95.2  │  546.0   │
│  08:15   │  165.0  │  170.5  │  155.0  │  160.0  │  650.5   │
│  08:30   │  200.0  │  210.0  │    0.0  │    0.0  │  410.0   │
├──────────┴─────────┴─────────┴─────────┴─────────┼──────────┤
│                          SUBTOTAL T1:             │ 1,606.5  │⭐
└───────────────────────────────────────────────────┴──────────┘

┌─────────────────────────────────────────────────────────────┐
│ 🔵 TINA T2                                                   │
├──────────┬─────────┬─────────┬─────────┬─────────┬──────────┤
│   HORA   │ PESO #1 │ PESO #2 │ PESO #3 │ PESO #4 │  TOTAL   │
├──────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│  09:00   │  200.0  │  210.5  │  190.0  │  185.5  │  786.0   │
│  09:30   │  220.0  │  230.0  │  215.0  │  225.0  │  890.0   │
├──────────┴─────────┴─────────┴─────────┴─────────┼──────────┤
│                          SUBTOTAL T2:             │ 1,676.0  │⭐
└───────────────────────────────────────────────────┴──────────┘

┌─────────────────────────────────────────────────────────────┐
│ 🔵 TINA T3                                                   │
├──────────┬─────────┬─────────┬─────────┬─────────┬──────────┤
│   HORA   │ PESO #1 │ PESO #2 │ PESO #3 │ PESO #4 │  TOTAL   │
├──────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│  (sin registros)                                             │
├──────────┴─────────┴─────────┴─────────┴─────────┼──────────┤
│                          SUBTOTAL T3:             │     0.0  │
└───────────────────────────────────────────────────┴──────────┘

... (continúa con T4, T5, T6, ..., T15)

┌─────────────────────────────────────────────────────────────┐
│ 📊 RESUMEN GENERAL - TODAS LAS TINAS                         │
├──────────┬───────────────┬──────────────────────────────────┤
│   TINA   │  # REGISTROS  │     TOTAL ACUMULADO (kg)         │
├──────────┼───────────────┼──────────────────────────────────┤
│   T1     │       3       │            1,606.5               │
│   T2     │       2       │            1,676.0               │
│   T3     │       0       │                0.0               │
│   T4     │       0       │                0.0               │
│   T5     │       0       │                0.0               │
│   T6     │       0       │                0.0               │
│   T7     │       0       │                0.0               │
│   T8     │       0       │                0.0               │
│   T9     │       0       │                0.0               │
│   T10    │       0       │                0.0               │
│   T11    │       0       │                0.0               │
│   T12    │       0       │                0.0               │
│   T13    │       0       │                0.0               │
│   T14    │       0       │                0.0               │
│   T15    │       0       │                0.0               │
├──────────┴───────────────┼──────────────────────────────────┤
│      TOTAL GENERAL:      │            3,282.5        🏆     │
└──────────────────────────┴──────────────────────────────────┘
```

---

## 📊 Estructura del Template

### 🎯 bodyElements (15 secciones, una por tina):

```json
{
  "bodyElements": [
    {
      "type": "section-group",
      "id": "tina-t1",
      "title": "🔵 TINA T1",
      "collapsible": true,
      "table": {
        "columns": [
          { "name": "HORA", "type": "time", "width": 15 },
          { "name": "PESO #1 (kg)", "type": "number", "width": 15, "step": 0.1 },
          { "name": "PESO #2 (kg)", "type": "number", "width": 15, "step": 0.1 },
          { "name": "PESO #3 (kg)", "type": "number", "width": 15, "step": 0.1 },
          { "name": "PESO #4 (kg)", "type": "number", "width": 15, "step": 0.1 },
          { "name": "TOTAL (kg)", "type": "calculated", "formula": "PESO1+PESO2+PESO3+PESO4", "readonly": true, "width": 15 }
        ],
        "enableSubtotal": true,
        "subtotalLabel": "SUBTOTAL T1:",
        "subtotalColumns": ["TOTAL (kg)"]
      }
    },
    {
      "type": "section-group",
      "id": "tina-t2",
      "title": "🔵 TINA T2",
      "collapsible": true,
      "table": {
        "columns": [
          { "name": "HORA", "type": "time", "width": 15 },
          { "name": "PESO #1 (kg)", "type": "number", "width": 15, "step": 0.1 },
          { "name": "PESO #2 (kg)", "type": "number", "width": 15, "step": 0.1 },
          { "name": "PESO #3 (kg)", "type": "number", "width": 15, "step": 0.1 },
          { "name": "PESO #4 (kg)", "type": "number", "width": 15, "step": 0.1 },
          { "name": "TOTAL (kg)", "type": "calculated", "formula": "PESO1+PESO2+PESO3+PESO4", "readonly": true, "width": 15 }
        ],
        "enableSubtotal": true,
        "subtotalLabel": "SUBTOTAL T2:",
        "subtotalColumns": ["TOTAL (kg)"]
      }
    },
    // ... (T3 a T15 con la misma estructura)
    {
      "type": "summary-table",
      "id": "resumen-general",
      "title": "📊 RESUMEN GENERAL - TODAS LAS TINAS",
      "readonly": true,
      "columns": [
        { "name": "TINA", "label": "TINA", "width": 15 },
        { "name": "NUM_REGISTROS", "label": "# REGISTROS", "width": 20 },
        { "name": "TOTAL_ACUMULADO", "label": "TOTAL ACUMULADO (kg)", "width": 30, "format": "0.00" }
      ]
    }
  ]
}
```

---

## 💾 Estructura de Datos

### Datos por Tina:
```json
{
  "bodyData": {
    "tina-t1": {
      "rows": [
        { "HORA": "08:00", "PESO #1 (kg)": 120.5, "PESO #2 (kg)": 150.3, "PESO #3 (kg)": 180.0, "PESO #4 (kg)": 95.2, "TOTAL (kg)": 546.0 },
        { "HORA": "08:15", "PESO #1 (kg)": 165.0, "PESO #2 (kg)": 170.5, "PESO #3 (kg)": 155.0, "PESO #4 (kg)": 160.0, "TOTAL (kg)": 650.5 },
        { "HORA": "08:30", "PESO #1 (kg)": 200.0, "PESO #2 (kg)": 210.0, "PESO #3 (kg)": 0.0, "PESO #4 (kg)": 0.0, "TOTAL (kg)": 410.0 }
      ],
      "subtotal": 1606.5
    },
    "tina-t2": {
      "rows": [
        { "HORA": "09:00", "PESO #1 (kg)": 200.0, "PESO #2 (kg)": 210.5, "PESO #3 (kg)": 190.0, "PESO #4 (kg)": 185.5, "TOTAL (kg)": 786.0 },
        { "HORA": "09:30", "PESO #1 (kg)": 220.0, "PESO #2 (kg)": 230.0, "PESO #3 (kg)": 215.0, "PESO #4 (kg)": 225.0, "TOTAL (kg)": 890.0 }
      ],
      "subtotal": 1676.0
    },
    "tina-t3": {
      "rows": [],
      "subtotal": 0.0
    },
    // ... (T4 a T15)
    "resumen-general": {
      "rows": [
        { "TINA": "T1", "NUM_REGISTROS": 3, "TOTAL_ACUMULADO": 1606.5 },
        { "TINA": "T2", "NUM_REGISTROS": 2, "TOTAL_ACUMULADO": 1676.0 },
        { "TINA": "T3", "NUM_REGISTROS": 0, "TOTAL_ACUMULADO": 0.0 },
        // ... (T4 a T15)
      ],
      "grandTotal": 3282.5
    }
  }
}
```

---

## 🚀 Código Frontend

### Calcular Subtotal por Tina:
```javascript
function calcularSubtotalTina(tinaNombre) {
  const rows = formData.body[`tina-${tinaNombre.toLowerCase()}`].rows;
  
  const subtotal = rows.reduce((sum, row) => {
    return sum + (parseFloat(row["TOTAL (kg)"]) || 0);
  }, 0);
  
  return subtotal.toFixed(2);
}

// Uso:
formData.body["tina-t1"].subtotal = calcularSubtotalTina("T1");
formData.body["tina-t2"].subtotal = calcularSubtotalTina("T2");
// ... para cada tina
```

### Calcular TOTAL por Fila:
```javascript
function calcularTotalFila(row) {
  const peso1 = parseFloat(row["PESO #1 (kg)"]) || 0;
  const peso2 = parseFloat(row["PESO #2 (kg)"]) || 0;
  const peso3 = parseFloat(row["PESO #3 (kg)"]) || 0;
  const peso4 = parseFloat(row["PESO #4 (kg)"]) || 0;
  
  return (peso1 + peso2 + peso3 + peso4).toFixed(2);
}

// Uso automático al cambiar pesos:
row["TOTAL (kg)"] = calcularTotalFila(row);
```

### Generar Resumen General:
```javascript
function generarResumenGeneral() {
  const tinas = ["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12", "T13", "T14", "T15"];
  
  const resumen = tinas.map(tina => {
    const tinaKey = `tina-${tina.toLowerCase()}`;
    const rows = formData.body[tinaKey].rows || [];
    const subtotal = parseFloat(formData.body[tinaKey].subtotal) || 0;
    
    return {
      TINA: tina,
      NUM_REGISTROS: rows.length,
      TOTAL_ACUMULADO: subtotal.toFixed(2)
    };
  });
  
  const grandTotal = resumen.reduce((sum, r) => sum + parseFloat(r.TOTAL_ACUMULADO), 0);
  
  formData.body["resumen-general"] = {
    rows: resumen,
    grandTotal: grandTotal.toFixed(2)
  };
}
```

---

## 🎨 Componente React Ejemplo

```jsx
function FormularioTinasPorSeccion() {
  const [formData, setFormData] = useState({
    "tina-t1": { rows: [], subtotal: 0 },
    "tina-t2": { rows: [], subtotal: 0 },
    // ... T3 a T15
    "resumen-general": { rows: [], grandTotal: 0 }
  });
  
  const [tinaActiva, setTinaActiva] = useState("T1");
  
  const agregarFilaTina = (tina) => {
    const tinaKey = `tina-${tina.toLowerCase()}`;
    const nuevaFila = {
      HORA: new Date().toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit' }),
      "PESO #1 (kg)": 0,
      "PESO #2 (kg)": 0,
      "PESO #3 (kg)": 0,
      "PESO #4 (kg)": 0,
      "TOTAL (kg)": 0
    };
    
    setFormData(prev => ({
      ...prev,
      [tinaKey]: {
        rows: [...prev[tinaKey].rows, nuevaFila],
        subtotal: prev[tinaKey].subtotal
      }
    }));
  };
  
  const calcularSubtotales = () => {
    const tinas = ["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12", "T13", "T14", "T15"];
    
    const newFormData = { ...formData };
    
    tinas.forEach(tina => {
      const tinaKey = `tina-${tina.toLowerCase()}`;
      const subtotal = newFormData[tinaKey].rows.reduce((sum, row) => {
        return sum + (parseFloat(row["TOTAL (kg)"]) || 0);
      }, 0);
      newFormData[tinaKey].subtotal = subtotal.toFixed(2);
    });
    
    // Generar resumen
    const resumen = tinas.map(tina => {
      const tinaKey = `tina-${tina.toLowerCase()}`;
      return {
        TINA: tina,
        NUM_REGISTROS: newFormData[tinaKey].rows.length,
        TOTAL_ACUMULADO: newFormData[tinaKey].subtotal
      };
    });
    
    const grandTotal = resumen.reduce((sum, r) => sum + parseFloat(r.TOTAL_ACUMULADO), 0);
    
    newFormData["resumen-general"] = {
      rows: resumen,
      grandTotal: grandTotal.toFixed(2)
    };
    
    setFormData(newFormData);
  };
  
  return (
    <div className="formulario-tinas">
      <h2>Registro de Producción - 15 Tinas (Por Sección)</h2>
      
      {/* Selector de Tina */}
      <div className="tina-tabs">
        {["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12", "T13", "T14", "T15"].map(tina => (
          <button 
            key={tina}
            className={tinaActiva === tina ? 'active' : ''}
            onClick={() => setTinaActiva(tina)}
          >
            {tina}
          </button>
        ))}
      </div>
      
      {/* Tabla de la Tina Activa */}
      <div className="tina-section">
        <h3>🔵 TINA {tinaActiva}</h3>
        
        <table>
          <thead>
            <tr>
              <th>HORA</th>
              <th>PESO #1 (kg)</th>
              <th>PESO #2 (kg)</th>
              <th>PESO #3 (kg)</th>
              <th>PESO #4 (kg)</th>
              <th>TOTAL (kg)</th>
            </tr>
          </thead>
          <tbody>
            {formData[`tina-${tinaActiva.toLowerCase()}`].rows.map((row, index) => (
              <tr key={index}>
                <td><input type="time" value={row.HORA} /></td>
                <td><input type="number" value={row["PESO #1 (kg)"]} step="0.1" /></td>
                <td><input type="number" value={row["PESO #2 (kg)"]} step="0.1" /></td>
                <td><input type="number" value={row["PESO #3 (kg)"]} step="0.1" /></td>
                <td><input type="number" value={row["PESO #4 (kg)"]} step="0.1" /></td>
                <td><strong>{row["TOTAL (kg)"]}</strong></td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan="5" style={{textAlign: 'right'}}><strong>SUBTOTAL {tinaActiva}:</strong></td>
              <td><strong>{formData[`tina-${tinaActiva.toLowerCase()}`].subtotal} kg</strong></td>
            </tr>
          </tfoot>
        </table>
        
        <button onClick={() => agregarFilaTina(tinaActiva)}>+ Agregar Registro</button>
      </div>
      
      {/* Botón para calcular */}
      <button onClick={calcularSubtotales}>🔄 Calcular Subtotales y Resumen</button>
      
      {/* Resumen General */}
      <div className="resumen-general">
        <h3>📊 RESUMEN GENERAL - TODAS LAS TINAS</h3>
        <table>
          <thead>
            <tr>
              <th>TINA</th>
              <th># REGISTROS</th>
              <th>TOTAL ACUMULADO (kg)</th>
            </tr>
          </thead>
          <tbody>
            {formData["resumen-general"].rows.map((row, index) => (
              <tr key={index}>
                <td>{row.TINA}</td>
                <td>{row.NUM_REGISTROS}</td>
                <td>{row.TOTAL_ACUMULADO}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan="2" style={{textAlign: 'right'}}><strong>TOTAL GENERAL:</strong></td>
              <td><strong>{formData["resumen-general"].grandTotal} kg 🏆</strong></td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}
```

---

## ✅ Ventajas de Este Diseño

1. ✅ **Una sección por tina** - Cada tina tiene su espacio dedicado
2. ✅ **Múltiples registros por tina** - Agrega filas con hora diferente
3. ✅ **Subtotal por tina** - Ve el acumulado de cada tina
4. ✅ **Resumen general** - Vista consolidada de todas las 15 tinas
5. ✅ **Navegación por pestañas** - Cambia fácilmente entre tinas
6. ✅ **Total automático** - Por fila y por tina

---

¿Quieres que cree el endpoint en el backend con esta estructura exacta? 🚀
