# 📄 API de Exportación PDF/Excel - Documentación

## 🎯 Endpoints para Exportación de Formularios

Se han agregado **4 nuevos endpoints** optimizados para la exportación de formularios a PDF/Excel. Todos los endpoints devuelven los datos completamente parseados y listos para usar.

---

## 🔌 Endpoints Disponibles

### 1. **Exportar Formulario Individual**

```http
GET /api/FilledForms/{id}/with-template
```

**Descripción:** Obtiene un formulario con su template completo y todos los datos parseados.

**Parámetros:**
- `id` (path): ID del formulario

**Respuesta Exitosa (200):**
```json
{
  "formID": 4,
  "templateID": 9,
  "templateVersion": "02-01",
  "createdAt": "2025-11-12T05:19:59.683Z",
  "updatedAt": null,
  "observaciones": "Primera liberación del día",
  "isHistorical": true,
  "data": {
    "header": {
      "Fecha": "2025-11-12",
      "Lote": "L-001"
    },
    "body": [
      {
        "rows": [
          {
            "CÓDIGO PIEZA / TINA": "123",
            "BARCO": "San Mateo",
            "CAJA N°": "001"
          }
        ]
      }
    ],
    "firmas": {
      "Calificador": "Juan Pérez",
      "Fecha": "2025-11-12"
    }
  },
  "template": {
    "templateID": 9,
    "codigo": "FOR-PD-3",
    "nombre": "LISTA DE EMPAQUE Y CALIFICACIÓN (FRESCO)",
    "version": "02-01",
    "objetivo": "Asegurar que los productos cumplen con los estándares",
    "proceso": "Calidad / Producción",
    "cuandoSeUsa": "Al finalizar empaque",
    "quienLoLlena": "Supervisor de Calidad",
    "structure": {
      "headerFields": [
        {
          "label": "Fecha",
          "type": "date",
          "required": true
        },
        {
          "label": "Lote",
          "type": "text",
          "required": true
        }
      ],
      "bodyElements": [
        {
          "id": 1688886401000,
          "type": "table",
          "title": "PRODUCTO TERMINADO",
          "columns": [
            { "id": "col1", "name": "CÓDIGO PIEZA / TINA", "type": "text" },
            { "id": "col2", "name": "BARCO", "type": "text" },
            { "id": "col3", "name": "CAJA N°", "type": "number" }
          ]
        }
      ],
      "firmas": [
        { "puesto": "SUPERVISOR GENERAL DE PRODUCCIÓN" },
        { "puesto": "CALIFICADOR" }
      ]
    }
  }
}
```

**Casos de Uso:**
- Exportar formulario individual a PDF
- Generar vista previa de impresión
- Exportar a Excel individual

---

### 2. **Exportar Múltiples Formularios**

```http
POST /api/FilledForms/export-multiple
Content-Type: application/json

[1, 2, 3, 4, 5]
```

**Descripción:** Obtiene múltiples formularios para exportación masiva.

**Body:**
```json
[4, 5, 6, 7]
```

**Respuesta Exitosa (200):**
```json
{
  "count": 4,
  "message": "4 formulario(s) listos para exportación",
  "forms": [
    {
      "formID": 4,
      "templateID": 9,
      "templateVersion": "02-01",
      "createdAt": "2025-11-12T05:19:59.683Z",
      "updatedAt": null,
      "observaciones": "Primera liberación",
      "isHistorical": true,
      "data": {
        "header": { "Fecha": "2025-11-12" },
        "body": [...],
        "firmas": {...}
      },
      "template": {
        "templateID": 9,
        "codigo": "FOR-PD-3",
        "nombre": "LISTA DE EMPAQUE Y CALIFICACIÓN (FRESCO)",
        "version": "02-01",
        "structure": {...}
      }
    },
    {
      "formID": 5,
      ...
    }
  ]
}
```

**Casos de Uso:**
- Exportar múltiples formularios seleccionados a un solo PDF
- Generar reportes consolidados
- Exportación masiva a Excel

---

### 3. **Exportar por Template**

```http
GET /api/FilledForms/export-by-template/{templateId}
```

**Descripción:** Obtiene todos los formularios de un template específico.

**Parámetros:**
- `templateId` (path): ID del template

**Ejemplo:**
```
GET /api/FilledForms/export-by-template/9
```

**Respuesta:** Misma estructura que `export-multiple`

**Casos de Uso:**
- Exportar todos los formularios de "LISTA DE EMPAQUE" a Excel
- Generar reporte mensual de un tipo de formulario
- Auditoría de un template específico

---

### 4. **Exportar por Rango de Fechas**

```http
GET /api/FilledForms/export-by-date-range?startDate=2025-01-01&endDate=2025-12-31
```

**Descripción:** Obtiene formularios por rango de fechas.

**Parámetros Query:**
- `startDate` (query): Fecha inicio (formato: YYYY-MM-DD)
- `endDate` (query): Fecha fin (formato: YYYY-MM-DD)

**Ejemplo:**
```
GET /api/FilledForms/export-by-date-range?startDate=2025-11-01&endDate=2025-11-30
```

**Respuesta:** Misma estructura que `export-multiple`

**Casos de Uso:**
- Exportar formularios del mes pasado
- Reportes trimestrales
- Auditorías por período

---

## 🎨 Uso en Frontend

### **Ejemplo 1: Exportar Formulario Individual**

```javascript
// En tu servicio de exportación (pdfExportService.js)

const exportFormToPDF = async (formId) => {
  try {
    // 🚀 Una sola llamada obtiene TODO
    const response = await fetch(
      `/api/FilledForms/${formId}/with-template`
    );
    const data = await response.json();
    
    // data ya tiene todo parseado:
    // - data.template.structure (HeaderFields, BodyElements, Firmas)
    // - data.data (Header, Body, Firmas)
    // - data.template (metadata del template)
    
    // Generar PDF con la data
    generatePDF(data);
    
  } catch (error) {
    console.error('Error exportando PDF:', error);
  }
};

const generatePDF = (formData) => {
  const pdf = new jsPDF();
  
  // Encabezado del documento
  pdf.setFontSize(16);
  pdf.text(formData.template.nombre, 10, 10);
  pdf.setFontSize(10);
  pdf.text(`Código: ${formData.template.codigo}`, 10, 20);
  pdf.text(`Versión: ${formData.template.version}`, 10, 25);
  pdf.text(`Fecha: ${new Date(formData.createdAt).toLocaleDateString()}`, 10, 30);
  
  let yPosition = 40;
  
  // Renderizar Header Fields
  formData.template.structure.headerFields.forEach(field => {
    const value = formData.data.header[field.label] || '';
    pdf.text(`${field.label}: ${value}`, 10, yPosition);
    yPosition += 5;
  });
  
  yPosition += 10;
  
  // Renderizar Body Elements (tablas)
  formData.template.structure.bodyElements.forEach((element, index) => {
    if (element.type === 'table') {
      pdf.setFontSize(12);
      pdf.text(element.title || `Tabla ${index + 1}`, 10, yPosition);
      yPosition += 7;
      
      // Obtener datos de la tabla
      const tableData = formData.data.body[index]?.rows || [];
      const columns = element.columns.map(col => col.name);
      
      // Usar autoTable para tablas bonitas
      pdf.autoTable({
        startY: yPosition,
        head: [columns],
        body: tableData.map(row => 
          element.columns.map(col => row[col.name] || '')
        ),
        theme: 'grid'
      });
      
      yPosition = pdf.lastAutoTable.finalY + 10;
    }
  });
  
  // Guardar PDF
  pdf.save(`${formData.template.codigo}_${formData.formID}.pdf`);
};
```

### **Ejemplo 2: Exportar Múltiples Formularios**

```javascript
const exportMultipleToPDF = async (formIds) => {
  try {
    const response = await fetch('/api/FilledForms/export-multiple', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(formIds) // [1, 2, 3, 4]
    });
    
    const result = await response.json();
    
    const pdf = new jsPDF();
    
    result.forms.forEach((formData, index) => {
      if (index > 0) pdf.addPage(); // Nueva página para cada formulario
      
      // Generar contenido del formulario
      generateFormContent(pdf, formData);
    });
    
    pdf.save(`Reporte_${result.count}_formularios.pdf`);
    
  } catch (error) {
    console.error('Error:', error);
  }
};
```

### **Ejemplo 3: Exportar a Excel**

```javascript
import * as XLSX from 'xlsx';

const exportFormToExcel = async (formId) => {
  const response = await fetch(`/api/FilledForms/${formId}/with-template`);
  const formData = await response.json();
  
  // Crear workbook
  const wb = XLSX.utils.book_new();
  
  // Hoja 1: Información General
  const infoSheet = XLSX.utils.json_to_sheet([
    { Campo: 'Formulario ID', Valor: formData.formID },
    { Campo: 'Template', Valor: formData.template.nombre },
    { Campo: 'Código', Valor: formData.template.codigo },
    { Campo: 'Versión', Valor: formData.templateVersion },
    { Campo: 'Fecha Creación', Valor: new Date(formData.createdAt).toLocaleDateString() },
    { Campo: 'Observaciones', Valor: formData.observaciones || '' }
  ]);
  XLSX.utils.book_append_sheet(wb, infoSheet, 'Información');
  
  // Hoja 2: Datos del Header
  const headerData = Object.entries(formData.data.header).map(([key, value]) => ({
    Campo: key,
    Valor: value
  }));
  const headerSheet = XLSX.utils.json_to_sheet(headerData);
  XLSX.utils.book_append_sheet(wb, headerSheet, 'Encabezado');
  
  // Hojas 3+: Tablas del Body
  formData.template.structure.bodyElements.forEach((element, index) => {
    if (element.type === 'table') {
      const tableRows = formData.data.body[index]?.rows || [];
      const tableSheet = XLSX.utils.json_to_sheet(tableRows);
      XLSX.utils.book_append_sheet(wb, tableSheet, element.title || `Tabla ${index + 1}`);
    }
  });
  
  // Descargar Excel
  XLSX.writeFile(wb, `${formData.template.codigo}_${formData.formID}.xlsx`);
};
```

---

## ✨ Características de los Endpoints

### ✅ **Versionamiento Incluido**
- Usa `TemplateSnapshot` si existe (versión histórica)
- Fallback al template actual para datos antiguos
- Campo `isHistorical` indica si usa snapshot

### ✅ **Datos Pre-Parseados**
- Todos los JSON ya están deserializados
- No necesitas hacer `JSON.parse()` en el frontend
- Manejo de errores de parsing incluido

### ✅ **Estructura Completa**
- Template con metadata completa
- Estructura del formulario (HeaderFields, BodyElements, Firmas)
- Datos del formulario llenado

### ✅ **Optimizado para Exportación**
- Una sola llamada HTTP
- Respuesta estructurada para fácil renderizado
- Compatible con jsPDF, pdfmake, xlsx, etc.

---

## 🔒 Validaciones y Manejo de Errores

### **Formulario No Encontrado (404)**
```json
{
  "message": "Formulario no encontrado"
}
```

### **Template No Encontrado (404)**
```json
{
  "message": "Template no encontrado"
}
```

### **Error de Parsing JSON (400)**
```json
{
  "message": "Error parseando datos del formulario",
  "error": "Invalid JSON format..."
}
```

### **IDs Vacíos en Export Multiple (400)**
```json
{
  "message": "Debe proporcionar al menos un FormID"
}
```

### **Sin Formularios en Rango de Fechas (200)**
```json
{
  "count": 0,
  "forms": [],
  "message": "No hay formularios entre 2025-01-01 y 2025-01-31"
}
```

---

## 📊 Casos de Uso Reales

### **Caso 1: Exportar Formulario a PDF**
```
Usuario hace clic en "Exportar PDF" → 
Frontend llama GET /api/FilledForms/4/with-template → 
Backend devuelve data completa → 
Frontend genera PDF con jsPDF → 
Usuario descarga archivo
```

### **Caso 2: Reporte Mensual en Excel**
```
Usuario selecciona "Noviembre 2025" → 
Frontend llama GET /api/FilledForms/export-by-date-range?startDate=2025-11-01&endDate=2025-11-30 → 
Backend devuelve todos los formularios del mes → 
Frontend genera Excel con xlsx → 
Usuario descarga reporte
```

### **Caso 3: Auditoría de Template**
```
Usuario quiere auditar template "FOR-PD-3" → 
Frontend llama GET /api/FilledForms/export-by-template/9 → 
Backend devuelve todos los formularios de ese template → 
Frontend genera PDF consolidado → 
Usuario imprime para auditoría
```

---

## 🚀 Próximos Pasos

1. **Instalar librerías en frontend:**
   ```bash
   npm install jspdf jspdf-autotable xlsx
   ```

2. **Crear servicio de exportación:**
   ```javascript
   // services/pdfExportService.js
   // services/excelExportService.js
   ```

3. **Agregar botones en UI:**
   ```jsx
   <button onClick={() => exportFormToPDF(formId)}>
     📄 Exportar PDF
   </button>
   <button onClick={() => exportFormToExcel(formId)}>
     📊 Exportar Excel
   </button>
   ```

4. **Probar endpoints:**
   - Usar Postman para verificar respuestas
   - Validar estructura de datos
   - Probar casos edge (formularios sin datos, templates antiguos, etc.)

---

## 📝 Resumen

**Endpoints Agregados:**
- ✅ `GET /api/FilledForms/{id}/with-template` - Individual
- ✅ `POST /api/FilledForms/export-multiple` - Múltiples
- ✅ `GET /api/FilledForms/export-by-template/{id}` - Por template
- ✅ `GET /api/FilledForms/export-by-date-range` - Por fechas

**Beneficios:**
- ✅ Datos pre-parseados (sin JSON.parse manual)
- ✅ Versionamiento automático (snapshots)
- ✅ Una sola llamada HTTP
- ✅ Estructura optimizada para renderizado
- ✅ Compatible con múltiples librerías de exportación

**Estado:** ✅ Backend Completo - Listo para Frontend

---

**Fecha:** 16 de Diciembre de 2025  
**Desarrollador:** GitHub Copilot  
**Versión:** 1.0
