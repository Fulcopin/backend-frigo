# DOCUMENTACIÓN - CORRECCIONES DE NOMBRES DE PROPIEDADES

## Mapeo Completo de Propiedades:

### FilledForm:
- `.Id` → `.FormID`
- `.TemplateId` → `.TemplateID`
- `.FechaCreacion` → `.CreatedAt`
- `.CreatedBy` → `.CreadoPor`
- `.FormData` → `.BodyData`
- `.Codigo` → NO EXISTE en FilledForm (solo en Template)

### Template:
- `.Id` → `.TemplateID`
- `.Name` → `.Nombre`
- `.Area` → NO EXISTE (este campo no existe en el modelo)
- `.Frecuencia` → NO EXISTE (este campo no existe en el modelo)

## Decisión para Campos Faltantes:

1. **Template.Area** - Agregar al modelo como campo opcional
2. **Template.Frecuencia** - Agregar al modelo como campo opcional  
3. **FilledForm.Codigo** - Usar Template.Codigo en su lugar

## Plan de Corrección:

### Paso 1: Agregar campos faltantes al modelo Template
### Paso 2: Corregir SignaturesController
### Paso 3: Corregir AlertsController  
### Paso 4: Corregir ConsumptionsController
### Paso 5: Corregir AlertBackgroundService
### Paso 6: Correg EPPlus License warning
