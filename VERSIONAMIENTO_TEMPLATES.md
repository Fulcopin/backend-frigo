# 📋 Sistema de Versionamiento de Templates - Documentación Completa

## 🎯 Objetivo

Implementar un sistema de versionamiento que garantice que:
- Los formularios creados HOY con el template actual se vean/impriman con ese formato
- Si MAÑANA modificas el template, los formularios de ayer siguen viéndose con el formato ANTIGUO
- Los formularios nuevos de mañana en adelante usan el formato NUEVO

## 🔧 Cambios Implementados

### 1. **Modelo FilledForm - Nuevos Campos**

```csharp
public class FilledForm
{
    // ... campos existentes ...
    
    // ✨ NUEVO: Snapshot completo del template
    [Column(TypeName = "nvarchar(max)")]
    public string? TemplateSnapshot { get; set; }
    
    // ✨ NUEVO: Versión específica del template
    [StringLength(20)]
    public string? TemplateVersion { get; set; }
}
```

**¿Qué guardan estos campos?**
- `TemplateSnapshot`: JSON completo con TODA la estructura del template (campos, secciones, tablas, firmas, etc.)
- `TemplateVersion`: La versión del template (ej: "1.0", "2.0")

### 2. **Controlador FilledFormsController - Cambios**

#### **A. Método POST - Guardar Snapshot al Crear**

Cuando se crea un formulario nuevo:
1. Se obtiene el template completo actual
2. Se crea un snapshot (foto) del template
3. Se guarda el snapshot EN el formulario
4. Se guarda la versión específica del template

```csharp
[HttpPost]
public async Task<ActionResult<FilledForm>> PostFilledForm([FromBody] FilledFormInputDto dto)
{
    var template = await _context.Templates.FindAsync(dto.TemplateID);
    
    // Crear snapshot del template ACTUAL
    var templateSnapshot = new { /* todos los campos del template */ };
    
    var filledForm = new FilledForm
    {
        TemplateID = dto.TemplateID,
        TemplateVersion = template.Version, // ✨ Guardar versión
        TemplateSnapshot = JsonSerializer.Serialize(templateSnapshot), // ✨ Guardar snapshot
        // ... resto de campos ...
    };
}
```

#### **B. Método GET - Usar Snapshot al Leer**

Cuando se recupera un formulario:
1. Si tiene snapshot guardado → usar el snapshot (formato histórico)
2. Si NO tiene snapshot → usar el template actual (fallback para datos viejos)

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<object>> GetFilledForm(int id)
{
    if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
    {
        // Usar snapshot histórico ✅
        templateData = JsonSerializer.Deserialize<object>(filledForm.TemplateSnapshot);
    }
    else
    {
        // Fallback al template actual
        templateData = GetCurrentTemplateData(filledForm.Template);
    }
}
```

#### **C. Método GET /edit - Siempre Usar Snapshot**

Para edición, SIEMPRE se usa el snapshot guardado para mantener consistencia.

### 3. **Base de Datos - Migración**

Se agregaron dos nuevas columnas a la tabla `FilledForms`:

```sql
ALTER TABLE [FilledForms] 
ADD [TemplateSnapshot] nvarchar(max) NULL;

ALTER TABLE [FilledForms] 
ADD [TemplateVersion] nvarchar(20) NULL;
```

## 📊 Flujo Completo del Sistema

### **Escenario 1: Crear Formulario HOY**

```
1. Usuario selecciona Template "FOR-PD-1" versión "1.0"
2. Backend crea snapshot del template actual:
   {
     "TemplateID": 1,
     "Version": "1.0",
     "HeaderFields": "[...]",
     "BodyElements": "[...]",
     ...
   }
3. Se guarda el formulario con:
   - TemplateID: 1
   - TemplateVersion: "1.0"
   - TemplateSnapshot: "{...snapshot completo...}"
```

### **Escenario 2: Modificar Template MAÑANA**

```
1. Usuario actualiza Template "FOR-PD-1" a versión "2.0"
   - Cambia campos del header
   - Agrega nuevas secciones
   - Modifica tabla
2. Template "FOR-PD-1" ahora tiene:
   - Version: "2.0"
   - HeaderFields: "[...nuevos campos...]"
   - BodyElements: "[...nueva estructura...]"
```

### **Escenario 3: Ver Formulario Creado AYER**

```
1. Usuario abre formulario ID=5 (creado ayer)
2. Backend detecta que tiene TemplateSnapshot
3. Backend devuelve el snapshot guardado (versión "1.0")
4. Frontend renderiza con el formato ANTIGUO ✅
5. Usuario ve/imprime el formulario con el formato original
```

### **Escenario 4: Crear Formulario MAÑANA**

```
1. Usuario crea nuevo formulario con Template "FOR-PD-1"
2. Backend toma snapshot del template ACTUAL (versión "2.0")
3. Se guarda con la nueva estructura
4. Frontend renderiza con el formato NUEVO ✅
```

## 🎨 Respuestas de API

### **GET /api/FilledForms/{id}**

```json
{
  "formID": 5,
  "templateID": 1,
  "templateVersion": "1.0",
  "headerData": "{...}",
  "bodyData": "{...}",
  "template": {
    "templateID": 1,
    "version": "1.0",
    "headerFields": "[...]",
    "bodyElements": "[...]"
  },
  "isHistorical": true  // ✨ Indica que usa versión histórica
}
```

### **GET /api/FilledForms/{id}/edit**

```json
{
  "formID": 5,
  "templateID": 1,
  "templateVersion": "1.0",
  "template": {
    // Snapshot histórico del template
  },
  "isHistorical": true
}
```

## 🔒 Reglas de Negocio

### ✅ **Regla 1: Inmutabilidad**
- Una vez creado el formulario, su estructura NO CAMBIA
- El snapshot se guarda al crear y NUNCA se modifica

### ✅ **Regla 2: Trazabilidad**
- Siempre sabemos con qué versión del template se creó cada formulario
- Campo `TemplateVersion` muestra la versión exacta

### ✅ **Regla 3: Compatibilidad hacia Atrás**
- Formularios antiguos sin snapshot usan el template actual (fallback)
- No se rompen datos existentes

### ✅ **Regla 4: Edición Consistente**
- Al editar un formulario, se usa su snapshot original
- No se mezclan formatos antiguo y nuevo

## 📱 Cambios Requeridos en Frontend (Mínimos)

### **1. Mostrar Indicador de Versión**

```javascript
if (formData.isHistorical) {
  // Mostrar badge: "Versión Histórica: 1.0"
  console.log(`📋 Formato histórico: v${formData.templateVersion}`);
}
```

### **2. Respetar Template del Response**

```javascript
// Ya no necesitas llamar separadamente al template
// Usa el template que viene en el response del formulario
const templateStructure = formData.template;
```

### **3. Opcional: Advertencia al Editar**

```javascript
if (formData.isHistorical) {
  showWarning(
    `Este formulario fue creado con la versión ${formData.templateVersion}. ` +
    `Mantiene su formato original aunque el template haya sido actualizado.`
  );
}
```

## 🚀 Aplicar la Migración

### **Opción 1: Entity Framework (Cuando Azure SQL esté disponible)**

```bash
dotnet ef database update
```

### **Opción 2: Script SQL Manual**

Ejecutar el archivo `Migrations/AddTemplateVersioning.sql` en Azure SQL.

## ✅ Verificación del Sistema

### **Prueba 1: Crear Formulario**
```bash
POST /api/FilledForms
{
  "templateID": 1,
  "headerData": "{...}",
  ...
}

# Verificar respuesta incluya:
# - templateVersion: "1.0"
# - templateSnapshot guardado
```

### **Prueba 2: Modificar Template**
```bash
PUT /api/Templates/1
{
  "version": "2.0",
  "headerFields": "[...nuevos...]",
  ...
}
```

### **Prueba 3: Ver Formulario Antiguo**
```bash
GET /api/FilledForms/5

# Debe mostrar:
# - templateVersion: "1.0"
# - isHistorical: true
# - template: { estructura antigua }
```

### **Prueba 4: Crear Formulario Nuevo**
```bash
POST /api/FilledForms
{
  "templateID": 1,
  ...
}

# Debe guardar:
# - templateVersion: "2.0"
# - templateSnapshot con estructura nueva
```

## 📈 Beneficios del Sistema

1. ✅ **Auditoría Completa**: Sabes exactamente qué formato tenía cada formulario
2. ✅ **Cumplimiento Normativo**: Documentos históricos mantienen su formato original
3. ✅ **Flexibilidad**: Puedes evolucionar templates sin romper datos
4. ✅ **Recuperación**: Puedes recrear templates antiguos desde snapshots
5. ✅ **Transparencia**: El usuario sabe si está viendo formato antiguo o nuevo

## 🔮 Funcionalidades Futuras (Opcionales)

- **Comparador de Versiones**: Mostrar diferencias entre versiones
- **Migrador de Datos**: Convertir formularios antiguos al nuevo formato
- **Historial de Cambios**: Ver todas las versiones de un template
- **Rollback**: Revertir template a versión anterior

---

## 📞 Soporte

Para más información sobre el versionamiento, contacta al equipo de desarrollo.

**Fecha de implementación:** 15 de Diciembre de 2025
**Versión del sistema:** 1.0
