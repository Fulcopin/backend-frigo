# 🎯 Sistema 15 Tinas - Hora Individual por Tina

## 📐 Diseño: Una Hora y Un Peso por Cada Tina

Cada una de las **15 tinas** tiene:
- ⏰ **Su propia HORA** (campo independiente)
- ⚖️ **Su propio PESO** (campo independiente)
- 🏆 **TOTAL GENERAL** al final (suma automática de los 15 pesos)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                  📊 REGISTRO DE PRODUCCIÓN - 15 TINAS                    │
│                     (Hora Individual por Tina)                           │
└─────────────────────────────────────────────────────────────────────────┘

┌───────────┬───────────┬───────────┬───────────┬───────────┐
│    T1     │    T2     │    T3     │    T4     │    T5     │
├───────────┼───────────┼───────────┼───────────┼───────────┤
│ ⏰ 08:00  │ ⏰ 08:15  │ ⏰ 08:30  │ ⏰ 08:45  │ ⏰ 09:00  │
│ ⚖️ 120.5  │ ⚖️ 150.3  │ ⚖️  95.2  │ ⚖️ 180.0  │ ⚖️ 140.0  │
└───────────┴───────────┴───────────┴───────────┴───────────┘

┌───────────┬───────────┬───────────┬───────────┬───────────┐
│    T6     │    T7     │    T8     │    T9     │   T10     │
├───────────┼───────────┼───────────┼───────────┼───────────┤
│ ⏰ 09:15  │ ⏰ 09:30  │ ⏰ 09:45  │ ⏰ 10:00  │ ⏰ 10:15  │
│ ⚖️ 110.5  │ ⚖️ 135.0  │ ⚖️ 145.5  │ ⚖️ 155.0  │ ⚖️ 165.0  │
└───────────┴───────────┴───────────┴───────────┴───────────┘

┌───────────┬───────────┬───────────┬───────────┬───────────┐
│   T11     │   T12     │   T13     │   T14     │   T15     │
├───────────┼───────────┼───────────┼───────────┼───────────┤
│ ⏰ 10:30  │ ⏰ 10:45  │ ⏰ 11:00  │ ⏰ 11:15  │ ⏰ 11:30  │
│ ⚖️  75.0  │ ⚖️  80.0  │ ⚖️   0.0  │ ⚖️   0.0  │ ⚖️  85.0  │
└───────────┴───────────┴───────────┴───────────┴───────────┘

┌─────────────────────────────────────────────────────────────┐
│              🏆 TOTAL GENERAL: 1,541.5 kg                    │
└─────────────────────────────────────────────────────────────┘
```

---

## 🎨 Ventajas de Este Diseño

### ✅ **Independencia por Tina**
- ✅ Cada tina tiene **su propia hora de registro**
- ✅ Útil cuando las tinas se procesan en **diferentes momentos**
- ✅ No necesitas registrar todas las tinas al mismo tiempo
- ✅ Puedes ir llenando tina por tina según vayas trabajando

### ✅ **Diseño Visual Claro**
- ✅ **15 tarjetas** (una por tina)
- ✅ Cada tarjeta muestra: Número de tina, hora y peso
- ✅ Layout en **grid** (5 columnas × 3 filas)
- ✅ TOTAL visible al final

### ✅ **Flexible**
- ✅ Puedes dejar tinas sin llenar (hora vacía, peso en 0)
- ✅ Solo llenas las tinas activas
- ✅ El TOTAL se calcula automáticamente

---

## 📊 Estructura del Template

### 🔹 Body Elements (15 field-groups + 1 calculated field):

```json
{
  "bodyElements": [
    {
      "type": "field-group",
      "id": "tina-t1-group",
      "title": "T1",
      "inline": true,
      "width": 12,
      "fields": [
        {
          "type": "time",
          "id": "hora-t1",
          "label": "⏰ Hora",
          "name": "HORA_T1",
          "placeholder": "--:--",
          "width": 50
        },
        {
          "type": "number",
          "id": "peso-t1",
          "label": "⚖️ Peso (kg)",
          "name": "PESO_T1",
          "min": 0,
          "step": 0.1,
          "placeholder": "0.0",
          "width": 50
        }
      ]
    },
    {
      "type": "field-group",
      "id": "tina-t2-group",
      "title": "T2",
      "inline": true,
      "width": 12,
      "fields": [
        {
          "type": "time",
          "id": "hora-t2",
          "label": "⏰ Hora",
          "name": "HORA_T2",
          "placeholder": "--:--",
          "width": 50
        },
        {
          "type": "number",
          "id": "peso-t2",
          "label": "⚖️ Peso (kg)",
          "name": "PESO_T2",
          "min": 0,
          "step": 0.1,
          "placeholder": "0.0",
          "width": 50
        }
      ]
    },
    // ... (T3 a T15 con la misma estructura)
    {
      "type": "calculated-field",
      "id": "total-general",
      "label": "🏆 TOTAL GENERAL",
      "name": "TOTAL",
      "formula": "PESO_T1+PESO_T2+PESO_T3+PESO_T4+PESO_T5+PESO_T6+PESO_T7+PESO_T8+PESO_T9+PESO_T10+PESO_T11+PESO_T12+PESO_T13+PESO_T14+PESO_T15",
      "format": "0.00",
      "unit": "kg",
      "readonly": true,
      "bold": true,
      "width": 100,
      "highlight": true,
      "fontSize": "1.2em"
    }
  ]
}
```

---

## 💾 Estructura de Datos

### Datos Completos:
```json
{
  "bodyData": {
    "HORA_T1": "08:00",
    "PESO_T1": 120.5,
    "HORA_T2": "08:15",
    "PESO_T2": 150.3,
    "HORA_T3": "08:30",
    "PESO_T3": 95.2,
    "HORA_T4": "08:45",
    "PESO_T4": 180.0,
    "HORA_T5": "09:00",
    "PESO_T5": 140.0,
    "HORA_T6": "09:15",
    "PESO_T6": 110.5,
    "HORA_T7": "09:30",
    "PESO_T7": 135.0,
    "HORA_T8": "09:45",
    "PESO_T8": 145.5,
    "HORA_T9": "10:00",
    "PESO_T9": 155.0,
    "HORA_T10": "10:15",
    "PESO_T10": 165.0,
    "HORA_T11": "10:30",
    "PESO_T11": 75.0,
    "HORA_T12": "10:45",
    "PESO_T12": 80.0,
    "HORA_T13": "11:00",
    "PESO_T13": 0.0,
    "HORA_T14": "11:15",
    "PESO_T14": 0.0,
    "HORA_T15": "11:30",
    "PESO_T15": 85.0,
    "TOTAL": 1541.5
  }
}
```

---

## 🚀 Código Frontend

### 1️⃣ State del Formulario:
```javascript
const [formData, setFormData] = useState({
  fecha: new Date().toISOString().split('T')[0],
  turno: 'Mañana',
  responsable: '',
  lote: '',
  tinas: [
    { numero: 1, hora: '', peso: 0 },
    { numero: 2, hora: '', peso: 0 },
    { numero: 3, hora: '', peso: 0 },
    { numero: 4, hora: '', peso: 0 },
    { numero: 5, hora: '', peso: 0 },
    { numero: 6, hora: '', peso: 0 },
    { numero: 7, hora: '', peso: 0 },
    { numero: 8, hora: '', peso: 0 },
    { numero: 9, hora: '', peso: 0 },
    { numero: 10, hora: '', peso: 0 },
    { numero: 11, hora: '', peso: 0 },
    { numero: 12, hora: '', peso: 0 },
    { numero: 13, hora: '', peso: 0 },
    { numero: 14, hora: '', peso: 0 },
    { numero: 15, hora: '', peso: 0 }
  ],
  total: 0
});
```

### 2️⃣ Calcular Total Automático:
```javascript
const calcularTotal = () => {
  const total = formData.tinas.reduce((sum, tina) => {
    return sum + (parseFloat(tina.peso) || 0);
  }, 0);
  
  setFormData(prev => ({ ...prev, total: parseFloat(total.toFixed(2)) }));
};

// Ejecutar cuando cambie algún peso
useEffect(() => {
  calcularTotal();
}, [formData.tinas]);
```

### 3️⃣ Handle Change para Peso:
```javascript
const handlePesoChange = (tinaIndex, value) => {
  const newTinas = [...formData.tinas];
  newTinas[tinaIndex].peso = parseFloat(value) || 0;
  setFormData({ ...formData, tinas: newTinas });
};
```

### 4️⃣ Handle Change para Hora:
```javascript
const handleHoraChange = (tinaIndex, value) => {
  const newTinas = [...formData.tinas];
  newTinas[tinaIndex].hora = value;
  setFormData({ ...formData, tinas: newTinas });
};
```

### 5️⃣ Validar Datos:
```javascript
const validarFormulario = () => {
  const errores = [];
  
  // Validar que al menos una tina tenga datos
  const tieneAlgunDato = formData.tinas.some(tina => 
    tina.hora || (parseFloat(tina.peso) || 0) > 0
  );
  
  if (!tieneAlgunDato) {
    errores.push("Debe registrar al menos una tina");
  }
  
  // Validar que si hay peso, haya hora (y viceversa)
  formData.tinas.forEach((tina, index) => {
    const tienePeso = (parseFloat(tina.peso) || 0) > 0;
    const tieneHora = tina.hora && tina.hora.trim() !== '';
    
    if (tienePeso && !tieneHora) {
      errores.push(`T${tina.numero}: Si hay peso, debe registrar la hora`);
    }
  });
  
  return errores;
};
```

---

## 🎨 Componente React Completo

```jsx
import React, { useState, useEffect } from 'react';
import './FormularioTinasIndividual.css';

function FormularioTinasIndividual() {
  const [formData, setFormData] = useState({
    fecha: new Date().toISOString().split('T')[0],
    turno: 'Mañana',
    responsable: '',
    lote: '',
    tinas: Array.from({ length: 15 }, (_, i) => ({
      numero: i + 1,
      hora: '',
      peso: 0
    })),
    total: 0
  });
  
  // Calcular total automáticamente
  useEffect(() => {
    const total = formData.tinas.reduce((sum, tina) => sum + (parseFloat(tina.peso) || 0), 0);
    setFormData(prev => ({ ...prev, total: parseFloat(total.toFixed(2)) }));
  }, [formData.tinas]);
  
  const handlePesoChange = (index, value) => {
    const newTinas = [...formData.tinas];
    newTinas[index].peso = parseFloat(value) || 0;
    setFormData({ ...formData, tinas: newTinas });
  };
  
  const handleHoraChange = (index, value) => {
    const newTinas = [...formData.tinas];
    newTinas[index].hora = value;
    setFormData({ ...formData, tinas: newTinas });
  };
  
  const handleSubmit = (e) => {
    e.preventDefault();
    
    // Validar
    const errores = validarFormulario();
    if (errores.length > 0) {
      alert('Errores:\n' + errores.join('\n'));
      return;
    }
    
    // Preparar datos para enviar
    const dataToSend = {
      headerFields: {
        fecha: formData.fecha,
        turno: formData.turno,
        responsable: formData.responsable,
        lote: formData.lote
      },
      bodyData: {}
    };
    
    // Agregar datos de cada tina
    formData.tinas.forEach(tina => {
      dataToSend.bodyData[`HORA_T${tina.numero}`] = tina.hora;
      dataToSend.bodyData[`PESO_T${tina.numero}`] = tina.peso;
    });
    dataToSend.bodyData.TOTAL = formData.total;
    
    console.log('Datos a enviar:', dataToSend);
    // Aquí harías el POST al backend
  };
  
  const validarFormulario = () => {
    const errores = [];
    
    const tieneAlgunDato = formData.tinas.some(tina => 
      tina.hora || (parseFloat(tina.peso) || 0) > 0
    );
    
    if (!tieneAlgunDato) {
      errores.push("Debe registrar al menos una tina");
    }
    
    formData.tinas.forEach(tina => {
      const tienePeso = (parseFloat(tina.peso) || 0) > 0;
      const tieneHora = tina.hora && tina.hora.trim() !== '';
      
      if (tienePeso && !tieneHora) {
        errores.push(`T${tina.numero}: Si hay peso, debe registrar la hora`);
      }
    });
    
    return errores;
  };
  
  return (
    <div className="formulario-tinas-individual">
      <h2>📊 Registro de Producción - 15 Tinas</h2>
      <p className="subtitle">(Hora Individual por Tina)</p>
      
      <form onSubmit={handleSubmit}>
        {/* Header Fields */}
        <div className="header-fields">
          <div className="field">
            <label>Fecha:</label>
            <input 
              type="date" 
              value={formData.fecha} 
              onChange={(e) => setFormData({ ...formData, fecha: e.target.value })} 
              required
            />
          </div>
          <div className="field">
            <label>Turno:</label>
            <select 
              value={formData.turno}
              onChange={(e) => setFormData({ ...formData, turno: e.target.value })}
            >
              <option>Mañana</option>
              <option>Tarde</option>
              <option>Noche</option>
            </select>
          </div>
          <div className="field">
            <label>Responsable:</label>
            <input 
              type="text" 
              placeholder="Nombre del responsable" 
              value={formData.responsable}
              onChange={(e) => setFormData({ ...formData, responsable: e.target.value })}
              required
            />
          </div>
          <div className="field">
            <label>Lote de Proceso:</label>
            <input 
              type="text" 
              placeholder="Código del lote" 
              value={formData.lote}
              onChange={(e) => setFormData({ ...formData, lote: e.target.value })}
              required
            />
          </div>
        </div>
        
        {/* Grid de Tinas */}
        <div className="tinas-grid">
          {formData.tinas.map((tina, index) => (
            <div key={tina.numero} className="tina-card">
              <h3>T{tina.numero}</h3>
              <div className="tina-fields">
                <div className="field">
                  <label>⏰ Hora:</label>
                  <input 
                    type="time" 
                    value={tina.hora}
                    onChange={(e) => handleHoraChange(index, e.target.value)}
                    placeholder="--:--"
                  />
                </div>
                <div className="field">
                  <label>⚖️ Peso (kg):</label>
                  <input 
                    type="number" 
                    value={tina.peso} 
                    onChange={(e) => handlePesoChange(index, e.target.value)}
                    step="0.1"
                    min="0"
                    placeholder="0.0"
                  />
                </div>
              </div>
            </div>
          ))}
        </div>
        
        {/* Total */}
        <div className="total-section">
          <h3>🏆 TOTAL GENERAL</h3>
          <div className="total-value">{formData.total.toFixed(2)} kg</div>
        </div>
        
        {/* Resumen */}
        <div className="resumen">
          <h4>📊 Resumen</h4>
          <p><strong>Tinas Activas:</strong> {formData.tinas.filter(t => (parseFloat(t.peso) || 0) > 0).length} / 15</p>
          <p><strong>Primera Hora:</strong> {formData.tinas.find(t => t.hora)?.hora || '--:--'}</p>
          <p><strong>Última Hora:</strong> {[...formData.tinas].reverse().find(t => t.hora)?.hora || '--:--'}</p>
        </div>
        
        <button type="submit" className="btn-submit">💾 Guardar Registro</button>
      </form>
    </div>
  );
}

export default FormularioTinasIndividual;
```

---

## 📝 CSS Recomendado

```css
.formulario-tinas-individual {
  max-width: 1400px;
  margin: 0 auto;
  padding: 20px;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}

.formulario-tinas-individual h2 {
  text-align: center;
  color: #2c3e50;
  margin-bottom: 5px;
}

.subtitle {
  text-align: center;
  color: #7f8c8d;
  font-style: italic;
  margin-bottom: 30px;
}

.header-fields {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 15px;
  margin-bottom: 30px;
  padding: 20px;
  background: #ecf0f1;
  border-radius: 8px;
}

.field {
  display: flex;
  flex-direction: column;
}

.field label {
  font-weight: 600;
  margin-bottom: 5px;
  color: #34495e;
}

.field input,
.field select {
  padding: 8px;
  border: 1px solid #bdc3c7;
  border-radius: 4px;
  font-size: 14px;
}

.tinas-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 15px;
  margin-bottom: 30px;
}

@media (max-width: 1200px) {
  .tinas-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

@media (max-width: 900px) {
  .tinas-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (max-width: 600px) {
  .tinas-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

.tina-card {
  background: white;
  border: 2px solid #3498db;
  border-radius: 8px;
  padding: 15px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
  transition: transform 0.2s, box-shadow 0.2s;
}

.tina-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.15);
}

.tina-card h3 {
  text-align: center;
  margin: 0 0 15px 0;
  color: #3498db;
  font-size: 1.3em;
  font-weight: bold;
}

.tina-fields {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.tina-card .field input {
  width: 100%;
}

.total-section {
  background: linear-gradient(135deg, #f39c12, #e67e22);
  color: white;
  padding: 25px;
  border-radius: 8px;
  text-align: center;
  margin-bottom: 20px;
  box-shadow: 0 4px 12px rgba(243, 156, 18, 0.3);
}

.total-section h3 {
  margin: 0 0 10px 0;
  font-size: 1.5em;
}

.total-value {
  font-size: 2.5em;
  font-weight: bold;
}

.resumen {
  background: #ecf0f1;
  padding: 20px;
  border-radius: 8px;
  margin-bottom: 20px;
}

.resumen h4 {
  margin-top: 0;
  color: #2c3e50;
}

.resumen p {
  margin: 8px 0;
  color: #34495e;
}

.btn-submit {
  width: 100%;
  padding: 15px;
  background: #27ae60;
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 1.1em;
  font-weight: bold;
  cursor: pointer;
  transition: background 0.3s;
}

.btn-submit:hover {
  background: #229954;
}
```

---

## ✅ Ventajas de Este Diseño

| ✅ Ventaja | Descripción |
|-----------|-------------|
| **Independiente** | Cada tina tiene su propia hora de registro |
| **Flexible** | Llena solo las tinas que estés procesando |
| **Visual** | Layout en grid con tarjetas por tina |
| **Claro** | Fácil identificar qué tina es cuál |
| **Automático** | TOTAL se calcula automáticamente |
| **Responsive** | Se adapta a diferentes tamaños de pantalla |

---

## 🎯 Endpoint para Crear

```bash
POST http://localhost:5074/api/TemplatePresets/create-15-tinas-single-row
Content-Type: application/json

{}
```

**Respuesta:**
```json
{
  "templateID": 123,
  "codigo": "FRM-TINAS-15-SINGLE",
  "nombre": "Registro de Producción - 15 Tinas (Hora Individual por Tina)",
  "version": "05-00",
  "createdAt": "2025-12-22T..."
}
```

---

## 📊 Cuándo Usar Este Template

✅ **SÍ usar** cuando:
- Cada tina se procesa en diferente momento
- Necesitas registrar hora específica por tina
- Las tinas tienen tiempos de proceso independientes
- Quieres flexibilidad para llenar tina por tina

❌ **NO usar** cuando:
- Todas las tinas se procesan al mismo tiempo
- Solo necesitas una hora general
- Prefieres formato de tabla matricial

---

¡Este es el diseño perfecto para registrar cada tina con su propia hora! 🎯
