# 🚀 Quick Start - Exportación PDF/Excel

## ⚡ Instalación Rápida

```bash
npm install jspdf jspdf-autotable xlsx
```

## 📦 Archivos Creados

```
Frontend-Services/
├── pdfExportService.js          ← Servicio PDF (completo)
├── excelExportService.js        ← Servicio Excel (completo)
├── FormExportButtons.jsx        ← Componente React listo para usar
├── EJEMPLOS_USO.jsx             ← 10 casos de uso reales
└── README_EXPORTACION.md        ← Documentación completa
```

## 🎯 Uso Más Simple (Copy-Paste)

### 1. Exportar UN formulario

```javascript
import { exportFormToPDF } from './Frontend-Services/pdfExportService';
import { exportFormToExcel } from './Frontend-Services/excelExportService';

// En tu botón/función:
await exportFormToPDF(4);        // ID del formulario
await exportFormToExcel(4);      // ID del formulario
```

### 2. Exportar MÚLTIPLES formularios

```javascript
import { exportMultipleFormsToPDF } from './Frontend-Services/pdfExportService';

const formIds = [1, 2, 3, 4, 5]; // IDs seleccionados
await exportMultipleFormsToPDF(formIds);
```

### 3. Usar componente pre-hecho

```jsx
import FormExportButtons from './Frontend-Services/FormExportButtons';

<FormExportButtons formId={4} selectedFormIds={[1,2,3,4,5]} />
```

## 🔌 Endpoints Backend (Ya están listos)

```
✅ GET  /api/FilledForms/{id}/with-template
✅ POST /api/FilledForms/export-multiple
✅ GET  /api/FilledForms/export-by-template/{templateId}
✅ GET  /api/FilledForms/export-by-date-range?startDate=...&endDate=...
```

## 📊 Qué Renderiza Automáticamente

### PDF incluye:
- ✅ Encabezado con logo y datos del formulario
- ✅ Información General (todos los header fields)
- ✅ Todas las tablas del body (dinámicas)
- ✅ Observaciones
- ✅ Firmas y aprobaciones
- ✅ Pie de página con fecha y numeración
- ✅ Marca de agua "HISTÓRICO" si aplica

### Excel incluye:
- ✅ Hoja individual por formulario
- ✅ Hoja consolidada para análisis de datos
- ✅ Formato tabular fácil de filtrar
- ✅ Compatible con PowerBI/análisis

## 🎨 Estructura de Datos que Recibe el Frontend

```json
{
  "formID": 4,
  "template": {
    "codigo": "FRM-001",
    "nombre": "Registro de Producción",
    "version": "02-01",
    "structure": {
      "headerFields": [...],
      "bodyElements": [...],
      "firmas": [...]
    }
  },
  "data": {
    "header": { "Fecha": "2025-01-15", ... },
    "body": [ { "rows": [...] } ],
    "firmas": { "ASISTENTE": "Juan Pérez" }
  },
  "observaciones": "Todo correcto",
  "isHistorical": false
}
```

## ✅ Checklist de Implementación

1. ✅ Backend ya tiene los 4 endpoints (FilledFormsController.cs)
2. ⬜ Compilar backend: `dotnet build`
3. ⬜ Instalar dependencias frontend: `npm install jspdf jspdf-autotable xlsx`
4. ⬜ Importar servicios en tu componente
5. ⬜ Agregar botones de exportación
6. ⬜ Probar con un formulario de prueba

## 🧪 Prueba Rápida en Postman

```http
GET http://localhost:5074/api/FilledForms/4/with-template
```

Deberías ver todos los datos parseados listos para renderizar.

## 💡 Tips Importantes

- **No necesitas parsear JSON** - El backend ya lo hace
- **Funciona con CUALQUIER estructura** - Es completamente dinámico
- **Versiones históricas** - Automáticamente usa snapshots
- **Sin configuración** - Todo sale de la estructura del template

## 🐛 Si algo no funciona

1. Verifica que el backend esté corriendo: `dotnet run`
2. Comprueba la consola del navegador (F12)
3. Prueba el endpoint en Postman primero
4. Verifica que el FormID exista en la base de datos

## 📞 Archivos de Ayuda

- `README_EXPORTACION.md` - Documentación completa
- `EJEMPLOS_USO.jsx` - 10 casos de uso reales
- `FormExportButtons.jsx` - Componente listo para usar

---

**¡Ya está todo listo! 🚀** Solo tienes que instalar las dependencias y empezar a usar.
