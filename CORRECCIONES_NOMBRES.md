# CORRECCIONES DE NOMBRES DE PROPIEDADES

## Modelos Identificados:

### FilledForm:
- Id → FormID ✅
- TemplateId → TemplateID ✅
- FechaCreacion → CreatedAt ✅
- CreatedBy → CreadoPor ✅
- Codigo → NO EXISTE (necesito verificar si hay un campo)
- FormData → BodyData ✅

### Template:
- Id → TemplateID ✅
- Name → Nombre ✅
- Area → NO EXISTE (necesito verificar)
- Frecuencia → NO EXISTE (necesito verificar)

## Archivos a Corregir:
1. Controllers/SignaturesController.cs
2. Controllers/ConsumptionsController.cs  
3. Services/AlertBackgroundService.cs
4. Models/Signature.cs (FK debe ser FilledFormId con FormID)
5. Models/Alert.cs (FK debe ser FormId con FormID)
