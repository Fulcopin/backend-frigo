# 📥 Sistema de Exportación PDF y Excel - Guía Completa

## 🎯 Descripción General

Sistema completo de exportación de formularios dinámicos a PDF y Excel que renderiza **automáticamente** cualquier estructura de formulario guardada en la base de datos.

### ✨ Características Principales

- ✅ **Exportación dinámica**: Funciona con cualquier estructura de formulario
- ✅ **PDF profesional**: Con encabezados, tablas, firmas y marca de agua para históricos
- ✅ **Excel flexible**: Hojas múltiples y datos consolidados para análisis
- ✅ **Múltiples formularios**: Exportación individual o en lote
- ✅ **Versionamiento**: Respeta snapshots de versiones históricas
- ✅ **Sin configuración manual**: Todo se genera a partir de la estructura del template

---

## 📁 Estructura de Archivos

```
backend-frigo/
├── Controllers/
│   └── FilledFormsController.cs     ← Backend con 4 endpoints de exportación
│
└── Frontend-Services/
    ├── pdfExportService.js          ← Servicio de exportación PDF
    ├── excelExportService.js        ← Servicio de exportación Excel
    ├── FormExportButtons.jsx        ← Componente React de ejemplo
    └── README_EXPORTACION.md        ← Este archivo
```

---

## 🚀 Instalación y Configuración

### 1️⃣ Instalar Dependencias Frontend

```bash
npm install jspdf jspdf-autotable xlsx
```

### 2️⃣ Importar Servicios en tu Componente

```javascript
// Para exportación PDF
import { 
  exportFormToPDF, 
  exportMultipleFormsToPDF 
} from './Frontend-Services/pdfExportService';

// Para exportación Excel
import { 
  exportFormToExcel, 
  exportMultipleFormsToExcel,
  exportConsolidatedDataToExcel 
} from './Frontend-Services/excelExportService';
```

### 3️⃣ Configurar URL Base (si es necesario)

Si tu backend no está en la misma URL, modifica las rutas en los servicios:

```javascript
// En pdfExportService.js y excelExportService.js
const API_BASE_URL = 'http://localhost:5074'; // Tu URL del backend

const response = await fetch(`${API_BASE_URL}/api/FilledForms/${formId}/with-template`);
```

---

## 📖 Endpoints del Backend

### 1. Obtener Formulario con Template Parseado

```http
GET /api/FilledForms/{id}/with-template
```

**Respuesta:**
```json
{
  "formID": 4,
  "templateID": 2,
  "createdAt": "2025-01-15T10:30:00Z",
  "observaciones": "Todo correcto",
  "isHistorical": false,
  "template": {
    "templateID": 2,
    "codigo": "FRM-001",
    "nombre": "Registro de Producción",
    "version": "02-01",
    "structure": {
      "headerFields": [
        { "label": "Fecha", "type": "date" },
        { "label": "Lote de Proceso", "type": "text" }
      ],
      "bodyElements": [
        {
          "type": "table",
          "title": "Registro de Producción de Fileteo",
          "columns": [
            { "name": "HORA", "type": "time" },
            { "name": "TINA", "type": "text" },
            { "name": "CÓDIGOS", "type": "text" },
            { "name": "ESPECIE", "type": "text" },
            { "name": "PESO BRUTO", "type": "number" },
            { "name": "PESO NETO", "type": "number" }
          ]
        }
      ],
      "firmas": [
        { "puesto": "ASISTENTE DE PRODUCCIÓN" },
        { "puesto": "JEFE DE ASEG. DE CALIDAD" }
      ]
    }
  },
  "data": {
    "header": {
      "Fecha": "2025-01-15",
      "Lote de Proceso": "LP-2025-001"
    },
    "body": [
      {
        "rows": [
          {
            "HORA": "08:00",
            "TINA": "T1",
            "CÓDIGOS": "COD-001",
            "ESPECIE": "Tilapia",
            "PESO BRUTO": "150.5",
            "PESO NETO": "120.3"
          }
        ]
      }
    ],
    "firmas": {
      "ASISTENTE DE PRODUCCIÓN": "Juan Pérez",
      "JEFE DE ASEG. DE CALIDAD": "María López"
    }
  }
}
```

### 2. Exportar Múltiples Formularios

```http
POST /api/FilledForms/export-multiple
Content-Type: application/json

[1, 2, 3, 4, 5]
```

### 3. Exportar por Template

```http
GET /api/FilledForms/export-by-template/{templateId}
```

### 4. Exportar por Rango de Fechas

```http
GET /api/FilledForms/export-by-date-range?startDate=2025-01-01&endDate=2025-01-31
```

---

## 💻 Ejemplos de Uso Frontend

### Ejemplo 1: Exportar un Formulario Individual

```javascript
import { exportFormToPDF } from './Frontend-Services/pdfExportService';
import { exportFormToExcel } from './Frontend-Services/excelExportService';

// En tu componente o función
const handleExportPDF = async () => {
  try {
    const result = await exportFormToPDF(4); // ID del formulario
    console.log('PDF descargado:', result.fileName);
  } catch (error) {
    console.error('Error:', error);
  }
};

const handleExportExcel = async () => {
  try {
    const result = await exportFormToExcel(4);
    console.log('Excel descargado:', result.fileName);
  } catch (error) {
    console.error('Error:', error);
  }
};
```

### Ejemplo 2: Exportar Múltiples Formularios

```javascript
import { exportMultipleFormsToPDF } from './Frontend-Services/pdfExportService';

const formIds = [1, 2, 3, 4, 5]; // IDs de formularios seleccionados

const handleExportMultiple = async () => {
  try {
    const result = await exportMultipleFormsToPDF(formIds);
    console.log(`PDF generado con ${result.count} formularios`);
  } catch (error) {
    console.error('Error:', error);
  }
};
```

### Ejemplo 3: Exportar Datos Consolidados (Análisis)

```javascript
import { exportConsolidatedDataToExcel } from './Frontend-Services/excelExportService';

const handleExportConsolidated = async () => {
  try {
    const result = await exportConsolidatedDataToExcel([1, 2, 3, 4, 5]);
    console.log('Datos consolidados descargados');
    // Genera un Excel con todas las tablas agrupadas por tipo
  } catch (error) {
    console.error('Error:', error);
  }
};
```

### Ejemplo 4: Usar Componente de Botones Pre-construido

```jsx
import FormExportButtons from './Frontend-Services/FormExportButtons';

function FormDetailPage() {
  const formId = 4;
  const selectedForms = [1, 2, 3, 4, 5];

  return (
    <div>
      <h1>Detalle del Formulario</h1>
      
      {/* Muestra botones de exportación */}
      <FormExportButtons 
        formId={formId} 
        selectedFormIds={selectedForms} 
      />
    </div>
  );
}
```

---

## 🎨 Estructura del PDF Generado

El PDF se genera dinámicamente con las siguientes secciones:

### 1. Encabezado
- Nombre de la empresa
- Título del formulario
- Código y versión
- Fecha e ID del formulario

### 2. Información General (Header Fields)
- Campos del header en formato de 2 columnas
- Ejemplo: Fecha, Lote de Proceso, etc.

### 3. Cuerpo del Formulario (Body Elements)

#### Para Tablas:
```
┌────────────────────────────────────────────────┐
│ REGISTRO DE PRODUCCIÓN DE FILETEO              │
├──────┬──────┬─────────┬─────────┬────────┬─────┤
│ HORA │ TINA │ CÓDIGOS │ ESPECIE │ PESO B │ P N │
├──────┼──────┼─────────┼─────────┼────────┼─────┤
│ 8:00 │  T1  │ COD-001 │ Tilapia │  150.5 │ 120 │
│ 9:00 │  T2  │ COD-002 │ Trucha  │  200.0 │ 180 │
└──────┴──────┴─────────┴─────────┴────────┴─────┘
```

#### Para Campos de Texto:
```
Material de Empaque:
  Bolsas plásticas de 500g, cajas de cartón
```

### 4. Observaciones
```
┌────────────────────────────────────────────────┐
│ OBSERVACIONES                                  │
├────────────────────────────────────────────────┤
│ abc.,!12                                       │
│ Todo correcto en el proceso                    │
└────────────────────────────────────────────────┘
```

### 5. Firmas y Aprobaciones
```
┌──────────────────────┬──────────────────────┐
│                      │                      │
│  Juan Pérez          │  María López         │
│ ───────────────────  │ ───────────────────  │
│ ASISTENTE DE PROD.   │ JEFE DE ASEG. CAL.   │
└──────────────────────┴──────────────────────┘
```

### 6. Pie de Página
- Fecha de generación
- Número de página
- Marca de agua "HISTÓRICO" si aplica

---

## 📊 Estructura del Excel Generado

### Modo Individual (1 formulario = 1 hoja)

```
A                    B                C
─────────────────────────────────────────
Registro de Producción
Código: FRM-001               Versión: 02-01
Fecha: 15/01/2025             ID: #4

INFORMACIÓN GENERAL
Fecha                15/01/2025
Lote de Proceso      LP-2025-001

REGISTRO DE PRODUCCIÓN DE FILETEO
HORA    TINA    CÓDIGOS    ESPECIE    PESO BRUTO    PESO NETO
08:00   T1      COD-001    Tilapia    150.5         120.3
09:00   T2      COD-002    Trucha     200.0         180.0

OBSERVACIONES
abc.,!12

FIRMAS Y APROBACIONES
ASISTENTE DE PRODUCCIÓN    Juan Pérez
JEFE DE ASEG. DE CALIDAD   María López
```

### Modo Consolidado (análisis de datos)

Crea **una hoja por cada tipo de tabla**, con datos de todos los formularios:

```
Hoja: "Registro de Producción de Fileteo"
────────────────────────────────────────────────────────────
FormID  Fecha       HORA   TINA   CÓDIGOS   ESPECIE   PESO B   PESO N
1       15/01/2025  08:00  T1     COD-001   Tilapia   150.5    120.3
1       15/01/2025  09:00  T2     COD-002   Trucha    200.0    180.0
2       16/01/2025  08:30  T1     COD-003   Tilapia   175.0    140.5
2       16/01/2025  10:00  T3     COD-004   Salmón    250.0    220.0
```

---

## 🔧 Personalización

### Cambiar Estilos del PDF

Edita `pdfExportService.js`:

```javascript
// En la función addHeader
headStyles: {
  fillColor: [66, 139, 202],  // Color azul del header (RGB)
  textColor: 255,              // Color del texto (blanco)
  fontStyle: 'bold'
}

// Cambiar colores de fondo
pdf.setFillColor(240, 240, 240); // Gris claro para títulos de sección
```

### Agregar Logo de la Empresa

```javascript
// En la función addHeader, después de yPosition = 10
const imgData = 'data:image/png;base64,...'; // Tu logo en base64
pdf.addImage(imgData, 'PNG', margin, yPosition, 30, 15);
yPosition += 20;
```

### Cambiar Formato de Fecha

```javascript
// En formatDate()
const formatDate = (date) => {
  if (!date) return '';
  const d = new Date(date);
  return d.toLocaleDateString('es-PE', { 
    year: 'numeric', 
    month: 'long', 
    day: 'numeric' 
  });
  // Resultado: "15 de enero de 2025"
};
```

---

## 🐛 Troubleshooting

### Error: "fetch is not defined"

Si usas Node.js antiguo, instala:
```bash
npm install node-fetch
```

### Error: "Cannot find module 'jspdf'"

Instala las dependencias:
```bash
npm install jspdf jspdf-autotable xlsx
```

### El PDF se ve cortado

Aumenta el tamaño de página o reduce el contenido:
```javascript
const pdf = new jsPDF('p', 'mm', 'a4'); // Portrait A4
// O usa
const pdf = new jsPDF('l', 'mm', 'a4'); // Landscape A4
```

### Las tablas no se alinean bien

Ajusta el ancho de las columnas en `addTableElement`:
```javascript
columnStyles: columns.reduce((acc, col, idx) => {
  acc[idx] = {
    cellWidth: 30, // Ancho fijo en mm
    halign: 'center'
  };
  return acc;
}, {})
```

---

## 📝 Notas Importantes

1. **JSON válido**: El backend ya parsea todo el JSON, el frontend solo renderiza
2. **Versionamiento**: Los formularios históricos usan `TemplateSnapshot` automáticamente
3. **Performance**: Para más de 50 formularios, considera paginación o exportación en background
4. **Tamaño de archivo**: PDFs grandes (>100 páginas) pueden tardar en generarse
5. **Compatibilidad**: Funciona en Chrome, Firefox, Safari, Edge (últimas 2 versiones)

---

## 🎯 Siguientes Pasos

1. ✅ **Compilar backend**:
   ```bash
   dotnet build FormBuilder.API.csproj
   ```

2. ✅ **Probar endpoints en Postman**:
   ```
   GET http://localhost:5074/api/FilledForms/4/with-template
   ```

3. ✅ **Instalar dependencias frontend**:
   ```bash
   npm install jspdf jspdf-autotable xlsx
   ```

4. ✅ **Integrar componente**:
   ```jsx
   import FormExportButtons from './Frontend-Services/FormExportButtons';
   <FormExportButtons formId={4} />
   ```

5. ✅ **Personalizar estilos** según tu branding

---

## 📞 Soporte

Si tienes preguntas o encuentras problemas:

1. Revisa la consola del navegador (F12) para errores
2. Verifica que el backend esté corriendo (`dotnet run`)
3. Comprueba que los endpoints respondan correctamente
4. Revisa que los datos del formulario estén en el formato esperado

---

**¡Listo! 🎉** Ya tienes un sistema completo de exportación PDF/Excel que funciona con **cualquier** estructura de formulario dinámico.
