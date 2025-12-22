# 🚀 Guía Rápida - Sistema de 15 Tinas

## 📋 Resumen
Sistema optimizado para registrar producción de **15 tinas** sin crear 15 tablas separadas.

---

## ⚡ Creación Instantánea (1 Endpoint)

### Opción 1: Template Completo (RECOMENDADO)
```http
POST http://localhost:5074/api/TemplatePresets/create-15-tinas
```

**Crea:**
- ✅ Tabla de registro detallado (HORA, TINA, CÓDIGO, ESPECIE, PESO BRUTO, PESO NETO, % RENDIMIENTO)
- ✅ Tabla de resumen automático (totales por tina)
- ✅ Cálculo automático de rendimientos
- ✅ Totales generales

### Opción 2: Template Simple (3 columnas)
```http
POST http://localhost:5074/api/TemplatePresets/create-simple-3-columns
```

**Crea:**
- ✅ Solo 3 columnas: HORA, TINA, PESO NETO
- ✅ Más rápido para registro simple

### Ver templates disponibles:
```http
GET http://localhost:5074/api/TemplatePresets/available
```

---

## 📊 Cómo Usar el Formulario

### 1. Llenar Registro Detallado

```javascript
// Frontend: Agregar una fila
const nuevaFila = {
  "HORA": "08:00",
  "TINA": "T1",
  "CÓDIGO": "COD-001",
  "ESPECIE": "Tilapia",
  "PESO BRUTO (kg)": 150.5,
  "PESO NETO (kg)": 120.3,
  "% RENDIMIENTO": 80.0  // Se calcula automáticamente
};

// Agregar a la tabla
formData.body["registro-detallado"].rows.push(nuevaFila);
```

### 2. Resumen se Calcula Automáticamente

El frontend debe agrupar los datos por TINA:

```javascript
// Función para calcular resumen automático
function calcularResumen(registros) {
  const tinas = ["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12", "T13", "T14", "T15"];
  
  return tinas.map(tina => {
    const registrosTina = registros.filter(r => r.TINA === tina);
    
    if (registrosTina.length === 0) {
      return {
        TINA: tina,
        NUM_REGISTROS: 0,
        TOTAL_BRUTO: 0,
        TOTAL_NETO: 0,
        RENDIMIENTO_PROMEDIO: 0,
        ESPECIES: "-"
      };
    }
    
    const totalBruto = registrosTina.reduce((sum, r) => sum + r["PESO BRUTO (kg)"], 0);
    const totalNeto = registrosTina.reduce((sum, r) => sum + r["PESO NETO (kg)"], 0);
    const rendimientoPromedio = (totalNeto / totalBruto) * 100;
    const especies = [...new Set(registrosTina.map(r => r.ESPECIE))].join(", ");
    
    return {
      TINA: tina,
      NUM_REGISTROS: registrosTina.length,
      TOTAL_BRUTO: totalBruto.toFixed(2),
      TOTAL_NETO: totalNeto.toFixed(2),
      RENDIMIENTO_PROMEDIO: rendimientoPromedio.toFixed(1),
      ESPECIES: especies
    };
  });
}

// Uso:
const resumen = calcularResumen(formData.body["registro-detallado"].rows);
formData.body["resumen-tinas"].rows = resumen;
```

### 3. Guardar Formulario Completo

```javascript
// POST al backend
const payload = {
  templateID: 5,  // ID del template de 15 tinas
  headerData: JSON.stringify({
    "Fecha": "2025-01-15",
    "Turno": "Mañana",
    "Responsable": "Juan Pérez",
    "Lote de Proceso": "LP-2025-001"
  }),
  bodyData: JSON.stringify({
    "registro-detallado": {
      rows: [
        { "HORA": "08:00", "TINA": "T1", "CÓDIGO": "COD-001", "ESPECIE": "Tilapia", "PESO BRUTO (kg)": 150.5, "PESO NETO (kg)": 120.3 },
        { "HORA": "08:15", "TINA": "T1", "CÓDIGO": "COD-002", "ESPECIE": "Tilapia", "PESO BRUTO (kg)": 160.0, "PESO NETO (kg)": 130.5 },
        { "HORA": "08:30", "TINA": "T2", "CÓDIGO": "COD-003", "ESPECIE": "Trucha", "PESO BRUTO (kg)": 200.0, "PESO NETO (kg)": 180.0 }
      ]
    },
    "resumen-tinas": {
      calculated: true,
      rows: resumen  // Calculado arriba
    }
  }),
  firmasData: JSON.stringify({
    "ASISTENTE DE PRODUCCIÓN": "Juan Pérez",
    "SUPERVISOR DE TURNO": "María López",
    "JEFE DE ASEGURAMIENTO DE CALIDAD": "Carlos Ruiz"
  }),
  observaciones: "Producción normal sin observaciones"
};

const response = await fetch('/api/FilledForms', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(payload)
});
```

---

## 🔍 Consultas Útiles

### Obtener datos de una tina específica
```javascript
// Filtrar por T5
const datosT5 = formData.body["registro-detallado"].rows
  .filter(row => row.TINA === "T5");

console.log(datosT5);
```

### Obtener resumen de una tina
```javascript
const resumenT5 = formData.body["resumen-tinas"].rows
  .find(row => row.TINA === "T5");

console.log(resumenT5);
// { TINA: "T5", NUM_REGISTROS: 2, TOTAL_NETO: 295.0, ... }
```

### Tinas con mayor producción
```javascript
const tinasOrdenadas = formData.body["resumen-tinas"].rows
  .filter(row => row.NUM_REGISTROS > 0)
  .sort((a, b) => b.TOTAL_NETO - a.TOTAL_NETO);

console.log(tinasOrdenadas[0]); // Tina con más producción
```

### Tinas sin producción
```javascript
const tinasSinDatos = formData.body["resumen-tinas"].rows
  .filter(row => row.NUM_REGISTROS === 0);

console.log(tinasSinDatos.map(r => r.TINA));
// ["T4", "T6", "T7", "T8", "T9", ...]
```

---

## 📈 Reportes y Visualizaciones

### Gráfico de Producción
```javascript
// Datos para Chart.js
const chartData = {
  labels: formData.body["resumen-tinas"].rows.map(r => r.TINA),
  datasets: [{
    label: 'Peso Neto (kg)',
    data: formData.body["resumen-tinas"].rows.map(r => r.TOTAL_NETO),
    backgroundColor: 'rgba(54, 162, 235, 0.5)'
  }]
};
```

### Tabla de Rendimientos Color-coded
```javascript
function getColorByRendimiento(rendimiento) {
  if (rendimiento >= 90) return '🟢'; // Verde - Excelente
  if (rendimiento >= 85) return '🟡'; // Amarillo - Bueno
  if (rendimiento >= 80) return '🟠'; // Naranja - Aceptable
  if (rendimiento > 0) return '🔴'; // Rojo - Bajo
  return '⚪'; // Blanco - Sin datos
}

formData.body["resumen-tinas"].rows.forEach(row => {
  const emoji = getColorByRendimiento(row.RENDIMIENTO_PROMEDIO);
  console.log(`${emoji} ${row.TINA}: ${row.RENDIMIENTO_PROMEDIO}%`);
});
```

---

## 💡 Tips de Uso

### 1. Validación en Tiempo Real
```javascript
// Validar que PESO NETO <= PESO BRUTO
function validarPeso(pesoBruto, pesoNeto) {
  if (pesoNeto > pesoBruto) {
    alert('El peso neto no puede ser mayor al peso bruto');
    return false;
  }
  return true;
}
```

### 2. Autocompletar TINA
```javascript
// Dropdown con búsqueda
<select name="tina" searchable>
  <option value="">Seleccionar tina...</option>
  <option value="T1">T1</option>
  <option value="T2">T2</option>
  ...
  <option value="T15">T15</option>
</select>
```

### 3. Copiar Última Fila
```javascript
function copiarUltimaFila() {
  const rows = formData.body["registro-detallado"].rows;
  const ultimaFila = rows[rows.length - 1];
  
  const nuevaFila = {
    ...ultimaFila,
    HORA: getCurrentTime(),  // Nueva hora
    "PESO BRUTO (kg)": 0,    // Resetear pesos
    "PESO NETO (kg)": 0
  };
  
  rows.push(nuevaFila);
}
```

### 4. Importar desde Excel
```javascript
// Leer archivo Excel y convertir a formato del formulario
async function importarDesdeExcel(file) {
  const workbook = XLSX.read(await file.arrayBuffer());
  const sheet = workbook.Sheets[workbook.SheetNames[0]];
  const data = XLSX.utils.sheet_to_json(sheet);
  
  const rows = data.map(row => ({
    "HORA": row.HORA || "",
    "TINA": row.TINA || "",
    "CÓDIGO": row.CÓDIGO || "",
    "ESPECIE": row.ESPECIE || "",
    "PESO BRUTO (kg)": parseFloat(row["PESO BRUTO"]) || 0,
    "PESO NETO (kg)": parseFloat(row["PESO NETO"]) || 0
  }));
  
  formData.body["registro-detallado"].rows = rows;
}
```

---

## 🎨 Ejemplo de UI Completa

```jsx
function FormularioTinas() {
  const [formData, setFormData] = useState({
    header: {
      Fecha: new Date().toISOString().split('T')[0],
      Turno: "Mañana",
      Responsable: "",
      "Lote de Proceso": ""
    },
    body: {
      "registro-detallado": { rows: [] },
      "resumen-tinas": { rows: [] }
    }
  });
  
  const agregarFila = () => {
    const nuevaFila = {
      HORA: new Date().toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit' }),
      TINA: "",
      CÓDIGO: "",
      ESPECIE: "",
      "PESO BRUTO (kg)": 0,
      "PESO NETO (kg)": 0
    };
    
    setFormData(prev => ({
      ...prev,
      body: {
        ...prev.body,
        "registro-detallado": {
          rows: [...prev.body["registro-detallado"].rows, nuevaFila]
        }
      }
    }));
  };
  
  const calcularResumen = () => {
    const resumen = calcularResumen(formData.body["registro-detallado"].rows);
    setFormData(prev => ({
      ...prev,
      body: {
        ...prev.body,
        "resumen-tinas": { rows: resumen }
      }
    }));
  };
  
  const guardar = async () => {
    calcularResumen();  // Calcular antes de guardar
    
    const payload = {
      templateID: 5,
      headerData: JSON.stringify(formData.header),
      bodyData: JSON.stringify(formData.body),
      firmasData: JSON.stringify({}),
      observaciones: ""
    };
    
    await fetch('/api/FilledForms', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
  };
  
  return (
    <div>
      <h2>Registro de Producción - 15 Tinas</h2>
      
      {/* Tabla de registro */}
      <table>
        <thead>
          <tr>
            <th>HORA</th>
            <th>TINA</th>
            <th>CÓDIGO</th>
            <th>ESPECIE</th>
            <th>PESO BRUTO</th>
            <th>PESO NETO</th>
          </tr>
        </thead>
        <tbody>
          {formData.body["registro-detallado"].rows.map((row, index) => (
            <tr key={index}>
              <td><input type="time" value={row.HORA} /></td>
              <td>
                <select value={row.TINA}>
                  <option value="">Seleccionar...</option>
                  {[...Array(15)].map((_, i) => (
                    <option key={i} value={`T${i+1}`}>T{i+1}</option>
                  ))}
                </select>
              </td>
              <td><input type="text" value={row.CÓDIGO} /></td>
              <td><input type="text" value={row.ESPECIE} /></td>
              <td><input type="number" value={row["PESO BRUTO (kg)"]} /></td>
              <td><input type="number" value={row["PESO NETO (kg)"]} /></td>
            </tr>
          ))}
        </tbody>
      </table>
      
      <button onClick={agregarFila}>+ Agregar Fila</button>
      <button onClick={calcularResumen}>🔄 Calcular Resumen</button>
      
      {/* Tabla de resumen */}
      <h3>Resumen por Tina</h3>
      <table>
        <thead>
          <tr>
            <th>TINA</th>
            <th># REG</th>
            <th>TOTAL NETO</th>
            <th>% REND</th>
          </tr>
        </thead>
        <tbody>
          {formData.body["resumen-tinas"].rows.map((row, index) => (
            <tr key={index} className={row.NUM_REGISTROS === 0 ? 'empty' : ''}>
              <td>{row.TINA}</td>
              <td>{row.NUM_REGISTROS}</td>
              <td>{row.TOTAL_NETO} kg</td>
              <td>{row.RENDIMIENTO_PROMEDIO}%</td>
            </tr>
          ))}
        </tbody>
      </table>
      
      <button onClick={guardar}>💾 Guardar Formulario</button>
    </div>
  );
}
```

---

## ✅ Checklist de Implementación

- [ ] Crear template con `POST /api/TemplatePresets/create-15-tinas`
- [ ] Implementar función `calcularResumen()` en frontend
- [ ] Crear interfaz para agregar filas
- [ ] Implementar dropdown de tinas (T1-T15)
- [ ] Validar peso neto <= peso bruto
- [ ] Mostrar tabla de resumen automático
- [ ] Botón para guardar formulario completo
- [ ] Exportar a PDF/Excel
- [ ] Visualizaciones (gráficos)

---

**¡Listo para usar! 🚀** Crea el template con un solo POST y empieza a registrar las 15 tinas.
