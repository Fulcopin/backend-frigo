# 📜 API de Historial de Versiones de Templates

## 🎯 Objetivo

Proporcionar endpoints para visualizar el historial completo de versiones de un template, ver formularios asociados a cada versión, y comparar cambios entre versiones.

---

## 🔌 Endpoints Disponibles

### 1. **Obtener Historial de Versiones**

```http
GET /api/Templates/{id}/versions/history
```

**Descripción:** Lista todas las versiones que han sido usadas de un template específico.

**Parámetros:**
- `id` (path): ID del template

**Respuesta Exitosa (200):**
```json
[
  {
    "version": "03-01",
    "firstUsedDate": "2025-12-15T10:00:00",
    "lastUsedDate": "2025-12-15T18:30:00",
    "formCount": 5,
    "isCurrentVersion": true
  },
  {
    "version": "02-01",
    "firstUsedDate": "2025-11-01T08:00:00",
    "lastUsedDate": "2025-12-14T23:59:00",
    "formCount": 23,
    "isCurrentVersion": false
  },
  {
    "version": "01-00",
    "firstUsedDate": "2025-09-15T09:00:00",
    "lastUsedDate": "2025-10-31T17:00:00",
    "formCount": 12,
    "isCurrentVersion": false
  }
]
```

**Campos:**
- `version`: Versión del template
- `firstUsedDate`: Primera vez que se usó esta versión
- `lastUsedDate`: Última vez que se usó esta versión
- `formCount`: Cantidad de formularios creados con esta versión
- `isCurrentVersion`: `true` si es la versión actual del template

---

### 2. **Obtener Detalles de una Versión Específica**

```http
GET /api/Templates/{id}/versions/{version}
```

**Descripción:** Obtiene la estructura completa de una versión específica y todos sus formularios asociados.

**Parámetros:**
- `id` (path): ID del template
- `version` (path): Versión a consultar (ej: "02-01")

**Respuesta Exitosa (200):**
```json
{
  "version": "02-01",
  "templateID": 9,
  "codigo": "FOR-CPCLT",
  "nombre": "Control de Productos Congelados (Liberación de Túneles)",
  "objetivo": "Asegurar que los productos congelados cumplen con los estándares de calidad",
  "proceso": "Calidad / Producción",
  "headerFields": "[{\"label\":\"Fecha\",\"type\":\"date\",\"required\":true}]",
  "bodyElements": "[{\"id\":1688886401000,\"type\":\"table\",\"title\":\"Registro de Liberación\"}]",
  "firmas": "[{\"puesto\":\"SUPERVISOR GENERAL DE PRODUCCIÓN\"}]",
  "associatedForms": [
    {
      "formID": 4,
      "createdAt": "2025-11-12T05:19:59.683",
      "headerData": "{\"Fecha\":\"2025-11-12\"}",
      "observaciones": "Primera liberación del día"
    },
    {
      "formID": 3,
      "createdAt": "2025-11-11T14:30:00",
      "headerData": "{\"Fecha\":\"2025-11-11\"}",
      "observaciones": null
    }
  ]
}
```

**Funcionamiento:**
- Si `version` es la versión **actual** → Usa el template actual de la BD
- Si `version` es **histórica** → Extrae el snapshot del primer formulario con esa versión
- Si no hay snapshot → Devuelve estructura básica con mensaje de fallback

---

### 3. **Comparar Dos Versiones**

```http
GET /api/Templates/{id}/versions/compare?oldVersion={v1}&newVersion={v2}
```

**Descripción:** Compara dos versiones de un template y lista los cambios detectados.

**Parámetros:**
- `id` (path): ID del template
- `oldVersion` (query): Versión antigua (ej: "02-01")
- `newVersion` (query): Versión nueva (ej: "03-01")

**Ejemplo de Petición:**
```
GET /api/Templates/9/versions/compare?oldVersion=02-01&newVersion=03-01
```

**Respuesta Exitosa (200):**
```json
{
  "oldVersion": "02-01",
  "newVersion": "03-01",
  "comparisonDate": "2025-12-15T15:30:00",
  "changes": [
    "Nombre: 'Control de Productos Congelados (Liberación de Túneles)' → 'Control de Productos Congelados V2'",
    "Objetivo: 'Asegurar que los productos congelados cumplen...' → 'Nuevo objetivo actualizado'",
    "HeaderFields: Estructura modificada",
    "BodyElements: Estructura de tabla modificada"
  ]
}
```

**Si no hay cambios:**
```json
{
  "oldVersion": "02-01",
  "newVersion": "02-01",
  "comparisonDate": "2025-12-15T15:30:00",
  "changes": [
    "No se detectaron cambios entre versiones"
  ]
}
```

---

### 4. **Obtener Formularios por Versión**

```http
GET /api/Templates/{id}/versions/{version}/forms
```

**Descripción:** Endpoint simplificado que devuelve SOLO los formularios de una versión específica (sin estructura del template).

**Parámetros:**
- `id` (path): ID del template
- `version` (path): Versión (ej: "02-01")

**Respuesta Exitosa (200):**
```json
[
  {
    "formID": 4,
    "templateID": 9,
    "templateVersion": "02-01",
    "headerData": "{\"Fecha\":\"2025-11-12\"}",
    "bodyData": "[{\"rows\":[{\"LOTE DE PROCESO\":\"jnd\"}]}]",
    "firmasData": "{}",
    "observaciones": "Primera liberación",
    "createdAt": "2025-11-12T05:19:59.683",
    "updatedAt": null
  }
]
```

**Si no hay formularios:**
```json
[]
```

---

## 🧪 Pruebas en Postman

### **Escenario 1: Ver Historial Completo**

1. **Crear varios formularios con versión actual**
   ```
   POST /api/FilledForms
   Body: { "templateID": 9, ... }
   ```

2. **Actualizar el template a nueva versión**
   ```
   PUT /api/Templates/9
   Body: { "version": "03-01", ... }
   ```

3. **Crear formularios con nueva versión**
   ```
   POST /api/FilledForms
   Body: { "templateID": 9, ... }
   ```

4. **Ver historial**
   ```
   GET /api/Templates/9/versions/history
   ```
   ✅ Debe mostrar dos versiones: "02-01" y "03-01"

---

### **Escenario 2: Ver Detalles de Versión Antigua**

```
GET /api/Templates/9/versions/02-01
```

✅ **Resultado esperado:**
- Muestra estructura del template versión "02-01" (extraída del snapshot)
- Lista todos los formularios creados con versión "02-01"
- `associatedForms` contiene los formularios históricos

---

### **Escenario 3: Comparar Versiones**

```
GET /api/Templates/9/versions/compare?oldVersion=02-01&newVersion=03-01
```

✅ **Resultado esperado:**
- Lista detallada de cambios entre las dos versiones
- Muestra qué campos fueron modificados
- Fecha de comparación

---

### **Escenario 4: Filtrar Formularios por Versión**

```
GET /api/Templates/9/versions/02-01/forms
```

✅ **Resultado esperado:**
- Solo formularios con `templateVersion: "02-01"`
- Ordenados por fecha (más reciente primero)

---

## 🎨 Interfaz de Usuario Sugerida

### **Ventana de Historial de Versiones**

```
┌─────────────────────────────────────────────────────────┐
│ 📋 Historial de Versiones - Template #9                 │
│ Control de Productos Congelados                         │
├─────────────────────────────────────────────────────────┤
│                                                          │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ Versión       Primera Uso    Última Uso   Formularios││
│ ├─────────────────────────────────────────────────────┤ │
│ │ 📌 03-01     15/12/2025    15/12/2025        5      ││ ← Actual
│ │    02-01     01/11/2025    14/12/2025       23      ││
│ │    01-00     15/09/2025    31/10/2025       12      ││
│ └─────────────────────────────────────────────────────┘ │
│                                                          │
│ [Ver Detalles] [Comparar] [Ver Formularios]            │
└─────────────────────────────────────────────────────────┘
```

### **Modal de Detalles de Versión**

```
┌─────────────────────────────────────────────────────────┐
│ 🔍 Detalles - Versión 02-01                             │
├─────────────────────────────────────────────────────────┤
│ Código: FOR-CPCLT                                       │
│ Nombre: Control de Productos Congelados...             │
│ Objetivo: Asegurar que los productos...                │
│                                                          │
│ 📝 Formularios Asociados (23)                           │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ #4  12/11/2025  Primera liberación del día         │ │
│ │ #3  11/11/2025  ---                                │ │
│ │ #2  10/11/2025  Túnel #3                           │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                          │
│ [Ver Template Completo] [Exportar] [Cerrar]            │
└─────────────────────────────────────────────────────────┘
```

### **Modal de Comparación**

```
┌─────────────────────────────────────────────────────────┐
│ ⚖️  Comparar Versiones                                   │
├─────────────────────────────────────────────────────────┤
│ De: 02-01  →  A: 03-01                                  │
│                                                          │
│ ✏️  Cambios Detectados:                                  │
│                                                          │
│ 1. Nombre modificado                                    │
│    'Control de Productos Congelados' →                  │
│    'Control de Productos Congelados V2'                 │
│                                                          │
│ 2. HeaderFields: Estructura modificada                  │
│                                                          │
│ 3. BodyElements: Estructura de tabla modificada         │
│                                                          │
│ [Cerrar]                                                │
└─────────────────────────────────────────────────────────┘
```

---

## 📊 Casos de Uso

### **1. Auditoría de Cambios**
- Ver cuándo se cambió el formato de un formulario
- Identificar qué versión se usaba en una fecha específica
- Rastrear evolución del template

### **2. Resolución de Problemas**
- Si un formulario antiguo no se ve bien, verificar qué versión usaba
- Comparar versiones para entender cambios

### **3. Reportes Históricos**
- Generar reportes con formularios de versiones específicas
- Filtrar datos por período/versión

### **4. Cumplimiento Normativo**
- Demostrar que registros antiguos usan el formato vigente en su momento
- Trazabilidad completa de cambios

---

## ⚙️ Implementación en Frontend

### **Ejemplo React/Vue**

```javascript
// Obtener historial
const loadVersionHistory = async (templateId) => {
  const response = await fetch(`/api/Templates/${templateId}/versions/history`);
  const history = await response.json();
  
  // Renderizar tabla
  history.forEach(version => {
    console.log(`Versión ${version.version}: ${version.formCount} formularios`);
    if (version.isCurrentVersion) {
      console.log('👈 Versión actual');
    }
  });
};

// Ver detalles de versión
const viewVersionDetails = async (templateId, version) => {
  const response = await fetch(`/api/Templates/${templateId}/versions/${version}`);
  const details = await response.json();
  
  console.log(`Template: ${details.nombre}`);
  console.log(`Formularios: ${details.associatedForms.length}`);
};

// Comparar versiones
const compareVersions = async (templateId, oldVer, newVer) => {
  const response = await fetch(
    `/api/Templates/${templateId}/versions/compare?oldVersion=${oldVer}&newVersion=${newVer}`
  );
  const comparison = await response.json();
  
  comparison.changes.forEach(change => {
    console.log(`📝 ${change}`);
  });
};
```

---

## 🔒 Consideraciones de Seguridad

- ✅ Todos los endpoints son **READ-ONLY** (GET)
- ✅ No modifican datos, solo consultan
- ✅ Validación de parámetros (ID, version)
- ✅ Manejo de errores 404 si no existen datos

---

## 📈 Performance

- ✅ Usa índices de base de datos (`TemplateID`, `TemplateVersion`)
- ✅ Consultas agrupadas eficientes con `GroupBy`
- ✅ Deserialización lazy (solo cuando se necesita)
- ✅ Paginación recomendada para `associatedForms` en templates con muchos formularios

---

## 🚀 ¡Listo para Usar!

El backend está completamente implementado. Solo necesitas:

1. ✅ Compilar el proyecto
2. ✅ Aplicar migración (si no está aplicada)
3. ✅ Crear interfaz de usuario
4. ✅ Probar endpoints en Postman

**¡Tu sistema de versionamiento ahora tiene vista de historial completa!** 📜✨
