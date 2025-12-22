# 🎯 Sistema 15 Tinas - Hora y Peso Individual por Tina

## 📐 Diseño Final: Una Hora + Un Peso por Cada Tina

**Estructura:**
- ✅ **15 tinas** (T1 a T15)
- ✅ **Cada tina tiene:**
  - ⏰ **Su propia HORA**
  - ⚖️ **Su propio PESO**
- ✅ **TOTAL GENERAL** (suma de todos los pesos)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                📊 REGISTRO DE PRODUCCIÓN - 15 TINAS                      │
└─────────────────────────────────────────────────────────────────────────┘

┌──────────┬──────────┬──────────┬──────────┬──────────┐
│   T1     │   T2     │   T3     │   T4     │   T5     │
├──────────┼──────────┼──────────┼──────────┼──────────┤
│⏰ 08:00  │⏰ 08:15  │⏰ 08:30  │⏰ 08:45  │⏰ 09:00  │
│⚖️ 120.5  │⚖️ 150.3  │⚖️  95.2  │⚖️ 180.0  │⚖️ 140.0  │
└──────────┴──────────┴──────────┴──────────┴──────────┘

┌──────────┬──────────┬──────────┬──────────┬──────────┐
│   T6     │   T7     │   T8     │   T9     │   T10    │
├──────────┼──────────┼──────────┼──────────┼──────────┤
│⏰ 09:15  │⏰ 09:30  │⏰ 09:45  │⏰ 10:00  │⏰ 10:15  │
│⚖️ 110.5  │⚖️ 135.0  │⚖️ 145.5  │⚖️ 155.0  │⚖️ 165.0  │
└──────────┴──────────┴──────────┴──────────┴──────────┘

┌──────────┬──────────┬──────────┬──────────┬──────────┐
│   T11    │   T12    │   T13    │   T14    │   T15    │
├──────────┼──────────┼──────────┼──────────┼──────────┤
│⏰ 10:30  │⏰ 10:45  │⏰ 11:00  │⏰ 11:15  │⏰ 11:30  │
│⚖️  75.0  │⚖️  80.0  │⚖️   0.0  │⚖️   0.0  │⚖️  85.0  │
└──────────┴──────────┴──────────┴──────────┴──────────┘

┌─────────────────────────────────────────────────────────────┐
│              🏆 TOTAL GENERAL: 1,541.5 kg                    │
└─────────────────────────────────────────────────────────────┘
```

---

## 📊 Estructura de Datos

```json
{
  "headerFields": {
    "fecha": "2025-12-22",
    "turno": "Mañana",
    "responsable": "Juan Pérez",
    "lote": "LOTE-2025-001"
  },
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

## 🚀 Código React Completo

```jsx
import React, { useState, useEffect } from 'react';
import './Formulario15Tinas.css';

function Formulario15Tinas() {
  const [formData, setFormData] = useState({
    // Header
    fecha: new Date().toISOString().split('T')[0],
    turno: 'Mañana',
    responsable: '',
    lote: '',
    // Body - 15 tinas
    tinas: Array.from({ length: 15 }, (_, i) => ({
      numero: i + 1,
      hora: '',
      peso: 0
    })),
    total: 0
  });
  
  // Calcular total automáticamente
  useEffect(() => {
    const total = formData.tinas.reduce((sum, tina) => {
      return sum + (parseFloat(tina.peso) || 0);
    }, 0);
    setFormData(prev => ({ ...prev, total: parseFloat(total.toFixed(2)) }));
  }, [formData.tinas]);
  
  const handleTinaChange = (index, field, value) => {
    const newTinas = [...formData.tinas];
    newTinas[index][field] = field === 'peso' ? (parseFloat(value) || 0) : value;
    setFormData({ ...formData, tinas: newTinas });
  };
  
  const handleSubmit = async (e) => {
    e.preventDefault();
    
    // Preparar datos para enviar
    const bodyData = {};
    formData.tinas.forEach(tina => {
      bodyData[`HORA_T${tina.numero}`] = tina.hora;
      bodyData[`PESO_T${tina.numero}`] = tina.peso;
    });
    bodyData.TOTAL = formData.total;
    
    const dataToSend = {
      headerFields: {
        fecha: formData.fecha,
        turno: formData.turno,
        responsable: formData.responsable,
        lote: formData.lote
      },
      bodyData: bodyData
    };
    
    console.log('Datos a enviar:', dataToSend);
    
    try {
      const response = await fetch('/api/FilledForms', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(dataToSend)
      });
      
      if (response.ok) {
        alert('✅ Formulario guardado exitosamente');
        // Limpiar formulario
      } else {
        alert('❌ Error al guardar');
      }
    } catch (error) {
      console.error('Error:', error);
      alert('❌ Error de conexión');
    }
  };
  
  return (
    <div className="formulario-15-tinas">
      <h1>📊 Registro de Producción - 15 Tinas</h1>
      
      <form onSubmit={handleSubmit}>
        {/* Header Fields */}
        <div className="header-section">
          <div className="field">
            <label>📅 Fecha:</label>
            <input 
              type="date" 
              value={formData.fecha} 
              onChange={(e) => setFormData({ ...formData, fecha: e.target.value })} 
              required
            />
          </div>
          <div className="field">
            <label>🌙 Turno:</label>
            <select 
              value={formData.turno}
              onChange={(e) => setFormData({ ...formData, turno: e.target.value })}
              required
            >
              <option>Mañana</option>
              <option>Tarde</option>
              <option>Noche</option>
            </select>
          </div>
          <div className="field">
            <label>👤 Responsable:</label>
            <input 
              type="text" 
              placeholder="Nombre del responsable" 
              value={formData.responsable}
              onChange={(e) => setFormData({ ...formData, responsable: e.target.value })}
              required
            />
          </div>
          <div className="field">
            <label>📦 Lote de Proceso:</label>
            <input 
              type="text" 
              placeholder="Código del lote" 
              value={formData.lote}
              onChange={(e) => setFormData({ ...formData, lote: e.target.value })}
              required
            />
          </div>
        </div>
        
        {/* Grid de 15 Tinas */}
        <div className="tinas-grid">
          {formData.tinas.map((tina, index) => (
            <div key={tina.numero} className="tina-card">
              <h3>T{tina.numero}</h3>
              <div className="tina-inputs">
                <div className="input-group">
                  <label>⏰</label>
                  <input 
                    type="time" 
                    value={tina.hora}
                    onChange={(e) => handleTinaChange(index, 'hora', e.target.value)}
                    placeholder="--:--"
                  />
                </div>
                <div className="input-group">
                  <label>⚖️ kg</label>
                  <input 
                    type="number" 
                    value={tina.peso} 
                    onChange={(e) => handleTinaChange(index, 'peso', e.target.value)}
                    step="0.1"
                    min="0"
                    placeholder="0.0"
                  />
                </div>
              </div>
            </div>
          ))}
        </div>
        
        {/* Total General */}
        <div className="total-section">
          <h2>🏆 TOTAL GENERAL</h2>
          <div className="total-value">{formData.total.toFixed(2)} kg</div>
        </div>
        
        {/* Resumen */}
        <div className="resumen">
          <h4>📊 Resumen</h4>
          <p><strong>Tinas Activas:</strong> {formData.tinas.filter(t => t.peso > 0).length} / 15</p>
          <p><strong>Primera Hora:</strong> {formData.tinas.find(t => t.hora)?.hora || '--:--'}</p>
          <p><strong>Última Hora:</strong> {[...formData.tinas].reverse().find(t => t.hora)?.hora || '--:--'}</p>
          <p><strong>Peso Promedio:</strong> {(formData.total / formData.tinas.filter(t => t.peso > 0).length || 0).toFixed(2)} kg/tina</p>
        </div>
        
        <button type="submit" className="btn-submit">💾 Guardar Registro</button>
      </form>
    </div>
  );
}

export default Formulario15Tinas;
```

---

## 📝 CSS Completo

```css
.formulario-15-tinas {
  max-width: 1400px;
  margin: 0 auto;
  padding: 20px;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  background: #f5f7fa;
}

.formulario-15-tinas h1 {
  text-align: center;
  color: #2c3e50;
  margin-bottom: 30px;
  font-size: 2em;
}

/* Header Section */
.header-section {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 15px;
  margin-bottom: 30px;
  padding: 25px;
  background: white;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

.field {
  display: flex;
  flex-direction: column;
}

.field label {
  font-weight: 600;
  margin-bottom: 8px;
  color: #34495e;
  font-size: 0.95em;
}

.field input,
.field select {
  padding: 10px;
  border: 2px solid #e0e6ed;
  border-radius: 8px;
  font-size: 14px;
  transition: border-color 0.3s;
}

.field input:focus,
.field select:focus {
  outline: none;
  border-color: #3498db;
}

/* Grid de Tinas */
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
  border: 3px solid #3498db;
  border-radius: 12px;
  padding: 15px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
  transition: transform 0.2s, box-shadow 0.2s, border-color 0.2s;
}

.tina-card:hover {
  transform: translateY(-5px);
  box-shadow: 0 6px 16px rgba(52, 152, 219, 0.3);
  border-color: #2980b9;
}

.tina-card h3 {
  text-align: center;
  margin: 0 0 15px 0;
  color: #3498db;
  font-size: 1.5em;
  font-weight: bold;
}

.tina-inputs {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.input-group {
  display: flex;
  flex-direction: column;
}

.input-group label {
  font-size: 1.2em;
  margin-bottom: 5px;
  color: #7f8c8d;
}

.input-group input {
  padding: 8px;
  border: 2px solid #e0e6ed;
  border-radius: 6px;
  font-size: 14px;
  text-align: center;
  transition: border-color 0.3s;
}

.input-group input:focus {
  outline: none;
  border-color: #3498db;
}

/* Total Section */
.total-section {
  background: linear-gradient(135deg, #f39c12, #e67e22);
  color: white;
  padding: 30px;
  border-radius: 12px;
  text-align: center;
  margin-bottom: 20px;
  box-shadow: 0 4px 12px rgba(243, 156, 18, 0.4);
}

.total-section h2 {
  margin: 0 0 15px 0;
  font-size: 1.8em;
}

.total-value {
  font-size: 3em;
  font-weight: bold;
  text-shadow: 2px 2px 4px rgba(0,0,0,0.2);
}

/* Resumen */
.resumen {
  background: white;
  padding: 20px;
  border-radius: 12px;
  margin-bottom: 20px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

.resumen h4 {
  margin-top: 0;
  color: #2c3e50;
  font-size: 1.3em;
}

.resumen p {
  margin: 10px 0;
  color: #34495e;
  font-size: 1.05em;
}

/* Submit Button */
.btn-submit {
  width: 100%;
  padding: 18px;
  background: linear-gradient(135deg, #27ae60, #229954);
  color: white;
  border: none;
  border-radius: 12px;
  font-size: 1.2em;
  font-weight: bold;
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
  box-shadow: 0 4px 12px rgba(39, 174, 96, 0.3);
}

.btn-submit:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(39, 174, 96, 0.4);
}

.btn-submit:active {
  transform: translateY(0);
}
```

---

## 🎯 API - Crear Template

**Endpoint:**
```bash
POST http://localhost:5074/api/TemplatePresets/create-15-tinas
Content-Type: application/json

{}
```

**Respuesta:**
```json
{
  "templateID": 123,
  "codigo": "FRM-TINAS-15",
  "nombre": "Registro de Producción - 15 Tinas",
  "version": "01-00",
  "createdAt": "2025-12-22T..."
}
```

---

## ✅ Características Principales

| Característica | Descripción |
|---------------|-------------|
| **15 Tinas Independientes** | Cada tina tiene HORA + PESO propios |
| **Total Automático** | Se calcula sumando los 15 pesos |
| **Layout en Grid** | 5 columnas × 3 filas |
| **Responsive** | Se adapta a móviles, tablets y desktop |
| **Visual Claro** | Tarjetas con colores y bordes destacados |
| **Validación** | Campos requeridos y tipos de datos validados |

---

## 📊 Ejemplo de Uso

1. **Llenar Header:** Fecha, turno, responsable, lote
2. **Llenar cada tina:**
   - T1: Hora 08:00, Peso 120.5 kg
   - T2: Hora 08:15, Peso 150.3 kg
   - ... (continuar con todas las tinas)
3. **Ver Total:** Se calcula automáticamente
4. **Guardar:** Click en el botón de guardar

---

¡Este es el diseño EXACTO que necesitas! 🎯
