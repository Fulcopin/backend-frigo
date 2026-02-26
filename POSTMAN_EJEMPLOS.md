# 📮 COLECCIÓN DE POSTMAN - MÓDULOS NUEVOS

## Base URL
```
http://localhost:5074/api
```

---

## 🖊️ 1. FIRMAS (Signatures)

### 1.1 Obtener Formularios Pendientes
```http
GET {{baseUrl}}/Signatures/pending
```

### 1.2 Firmar Formulario Individual
```http
POST {{baseUrl}}/Signatures/sign/1
Content-Type: application/json

{
  "formId": 1,
  "signatureImage": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUA",
  "signedBy": "supervisor@frigolab.com",
  "signedDate": "2026-02-11T10:30:00Z",
  "comments": "Revisado y aprobado"
}
```

### 1.3 Firmar Múltiples Formularios
```http
POST {{baseUrl}}/Signatures/sign-multiple
Content-Type: application/json

{
  "formIds": [1, 2, 3],
  "signatureImage": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUA",
  "signedBy": "supervisor@frigolab.com",
  "signedDate": "2026-02-11T10:30:00Z",
  "comments": "Revisión masiva semanal"
}
```

### 1.4 Ver Historial de Firmas
```http
GET {{baseUrl}}/Signatures/history/1
```

### 1.5 Ver Estadísticas
```http
GET {{baseUrl}}/Signatures/stats
```

### 1.6 Rechazar Formulario
```http
POST {{baseUrl}}/Signatures/reject/1
Content-Type: application/json

{
  "formId": 1,
  "rejectedBy": "sgi@frigolab.com",
  "reason": "Datos incompletos en sección 3",
  "rejectedDate": "2026-02-11T11:00:00Z"
}
```

### 1.7 Modificar Fecha de Firma (Solo SGI)
```http
PUT {{baseUrl}}/Signatures/update-date/1
Content-Type: application/json

{
  "signatureId": 1,
  "newDate": "2026-02-10T15:00:00Z"
}
```

---

## 🔔 2. ALERTAS (Alerts)

### 2.1 Ver Alertas Activas
```http
GET {{baseUrl}}/Alerts/active
```

### 2.2 Ver Configuración
```http
GET {{baseUrl}}/Alerts/config
```

### 2.3 Actualizar Configuración
```http
PUT {{baseUrl}}/Alerts/config
Content-Type: application/json

{
  "id": 1,
  "enableMissingFormAlerts": true,
  "dailyCheckTime": "18:00",
  "missingFormRecipients": "[\"supervisor@frigolab.com\", \"admin@frigolab.com\"]",
  "enableSignatureAlerts": true,
  "signatureAlertDelay": 24,
  "signatureRecipients": "[\"supervisor@frigolab.com\"]",
  "senderEmail": "alertas@frigolab.com",
  "senderName": "Frigolab Alertas"
}
```

### 2.4 Marcar Alerta como Leída
```http
PUT {{baseUrl}}/Alerts/mark-read/1
```

### 2.5 Enviar Email de Prueba ⭐
```http
POST {{baseUrl}}/Alerts/test
Content-Type: application/json

{
  "email": "tu-email@gmail.com"
}
```

**Respuesta esperada:**
```json
{
  "success": true,
  "message": "Email de prueba enviado exitosamente"
}
```

### 2.6 Ver Historial de Alertas
```http
GET {{baseUrl}}/Alerts/history
```

**Con filtros:**
```http
GET {{baseUrl}}/Alerts/history?startDate=2026-01-01&endDate=2026-02-11&type=missing_form&status=sent
```

### 2.7 Ver Estadísticas
```http
GET {{baseUrl}}/Alerts/stats
```

### 2.8 Crear Alerta Manual
```http
POST {{baseUrl}}/Alerts/manual
Content-Type: application/json

{
  "type": "system",
  "priority": "high",
  "title": "Mantenimiento Programado",
  "message": "Se realizará mantenimiento el viernes 15/02",
  "targetEmail": "todos@frigolab.com"
}
```

---

## 📊 3. CONSUMOS (Consumptions)

### 3.1 Ver Consumos Consolidados
```http
GET {{baseUrl}}/Consumptions/consolidated
```

**Con filtros:**
```http
GET {{baseUrl}}/Consumptions/consolidated?startDate=2026-01-01&endDate=2026-02-11&product=Sal&area=Producción&groupBy=product
```

**Respuesta esperada:**
```json
[
  {
    "productName": "Sal",
    "totalQuantity": 450.75,
    "unit": "kg",
    "formCount": 28,
    "areas": ["Producción", "Empaque"],
    "lastDate": "2026-02-11T00:00:00Z"
  }
]
```

### 3.2 Consumos por Producto
```http
GET {{baseUrl}}/Consumptions/by-product/Sal
```

**Con filtros:**
```http
GET {{baseUrl}}/Consumptions/by-product/Sal?startDate=2026-01-01&endDate=2026-02-11
```

### 3.3 Consumos por Área
```http
GET {{baseUrl}}/Consumptions/by-area/Producción
```

### 3.4 Lista de Productos
```http
GET {{baseUrl}}/Consumptions/products
```

**Respuesta esperada:**
```json
[
  {
    "name": "Sal",
    "totalConsumption": 450.75,
    "unit": "kg"
  },
  {
    "name": "Hielo",
    "totalConsumption": 1250.50,
    "unit": "kg"
  }
]
```

### 3.5 Lista de Áreas
```http
GET {{baseUrl}}/Consumptions/areas
```

### 3.6 Estadísticas
```http
GET {{baseUrl}}/Consumptions/stats
```

**Con filtros:**
```http
GET {{baseUrl}}/Consumptions/stats?startDate=2026-01-01&endDate=2026-02-11
```

### 3.7 Exportar a Excel ⭐
```http
GET {{baseUrl}}/Consumptions/export/excel
```

**Con filtros:**
```http
GET {{baseUrl}}/Consumptions/export/excel?startDate=2026-01-01&endDate=2026-02-11&product=Sal&area=Producción
```

**Nota:** Descargará un archivo `.xlsx`

### 3.8 Comparar Dos Períodos
```http
POST {{baseUrl}}/Consumptions/compare
Content-Type: application/json

{
  "period1": {
    "start": "2026-01-01",
    "end": "2026-01-31"
  },
  "period2": {
    "start": "2026-02-01",
    "end": "2026-02-11"
  }
}
```

**Respuesta esperada:**
```json
{
  "period1Total": 2500.50,
  "period2Total": 1850.25,
  "difference": -650.25,
  "percentageChange": -26.01,
  "products": [
    {
      "name": "Sal",
      "period1": 450.75,
      "period2": 380.50,
      "difference": -70.25,
      "percentageChange": -15.58
    }
  ]
}
```

---

## 🎯 PRUEBAS RECOMENDADAS (EN ORDEN)

### 1️⃣ Verificar que el Backend está corriendo
```http
GET http://localhost:5074/
```

### 2️⃣ Probar envío de Email
```http
POST http://localhost:5074/api/Alerts/test
Body: { "email": "tu-email@gmail.com" }
```

### 3️⃣ Ver formularios pendientes
```http
GET http://localhost:5074/api/Signatures/pending
```

### 4️⃣ Ver estadísticas de firmas
```http
GET http://localhost:5074/api/Signatures/stats
```

### 5️⃣ Ver consumos consolidados
```http
GET http://localhost:5074/api/Consumptions/consolidated
```

### 6️⃣ Ver productos disponibles
```http
GET http://localhost:5074/api/Consumptions/products
```

### 7️⃣ Exportar a Excel
```http
GET http://localhost:5074/api/Consumptions/export/excel
```

---

## 📝 VARIABLES DE ENTORNO (Postman)

Crear un Environment en Postman con:

```json
{
  "baseUrl": "http://localhost:5074/api",
  "testEmail": "tu-email@gmail.com"
}
```

Uso:
```http
GET {{baseUrl}}/Signatures/pending
POST {{baseUrl}}/Alerts/test
Body: { "email": "{{testEmail}}" }
```

---

## ⚠️ NOTAS IMPORTANTES

1. **Fechas:** Usar formato ISO 8601: `2026-02-11T10:30:00Z`
2. **Firmas:** `signatureImage` debe ser Base64 completo con prefijo `data:image/png;base64,`
3. **Filtros:** Todos los query parameters son opcionales
4. **Excel:** El navegador descargará el archivo automáticamente
5. **Emails:** Requiere configuración de Gmail App Password

---

## 🔍 RESPUESTAS DE ERROR COMUNES

### 400 Bad Request
```json
{
  "message": "Error en validación de datos"
}
```

### 404 Not Found
```json
{
  "message": "Formulario no encontrado"
}
```

### 500 Internal Server Error
```json
{
  "message": "Error interno del servidor"
}
```

---

## 🎉 ¡TODO LISTO!

Importa estos ejemplos en Postman y comienza a probar los nuevos módulos.

**Tip:** Comienza con el email de prueba para verificar que Gmail está configurado correctamente.
