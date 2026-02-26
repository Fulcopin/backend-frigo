# ⚡ FIX RÁPIDO: Error de Autenticación Gmail

## 🚨 Error que estás viendo:
```
Error sending email: The SMTP server requires a secure connection 
or the client was not authenticated. 
5.7.0 Authentication Required
```

---

## ✅ SOLUCIÓN EN 5 MINUTOS

### 1️⃣ Obtener Contraseña de Aplicación Gmail

1. Ve a: https://myaccount.google.com/apppasswords
2. Si no puedes entrar, primero activa verificación en 2 pasos: https://myaccount.google.com/signinoptions/two-step-verification
3. Selecciona **"Correo"** y **"Otro"**
4. Escribe: **"Frigolab"**
5. Copia la contraseña de 16 caracteres (ejemplo: `abcd efgh ijkl mnop`)

### 2️⃣ Editar appsettings.json

Abre el archivo:
```powershell
notepad C:\Users\fupifigu\Desktop\diagramas\sillos\dinamic-generador\backend-frigo\appsettings.json
```

Cambia estas líneas:
```json
"SenderEmail": "TU-EMAIL@gmail.com",          → "SenderEmail": "fstue1@gmail.com",
"SenderPassword": "xxxx xxxx xxxx xxxx",      → "SenderPassword": "TU-CONTRASEÑA-DE-16-CARACTERES",
```

**EJEMPLO REAL:**
```json
{
  "GmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "EnableSsl": true,
    "SenderEmail": "fstue1@gmail.com",
    "SenderPassword": "abcdefghijklmnop",
    "SenderName": "Frigolab Alertas"
  }
}
```

### 3️⃣ Reiniciar Backend

En la terminal del backend, presiona **Ctrl+C** y luego:
```powershell
dotnet run
```

### 4️⃣ Probar

Abre Postman y ejecuta:
```http
POST http://localhost:5074/api/Alerts/test
Content-Type: application/json

{
  "email": "fstue1@gmail.com"
}
```

Deberías recibir un email de prueba en 5-10 segundos.

---

## ❓ NO FUNCIONA TODAVÍA?

### Verifica:
- ✅ Usaste contraseña de **aplicación** (16 caracteres), NO tu contraseña normal
- ✅ Copiaste la contraseña completa sin espacios extra
- ✅ El email es exactamente `fstue1@gmail.com`
- ✅ Reiniciaste el backend después de editar

### Si sigue sin funcionar:
1. Genera una NUEVA contraseña de aplicación: https://myaccount.google.com/apppasswords
2. Borra la contraseña anterior en Gmail
3. Usa la nueva en `appsettings.json`
4. Reinicia backend

---

## 📖 Documentación Completa

Ver: `GUIA_CONFIGURAR_GMAIL.md` para más detalles.

---

**⏱️ Tiempo estimado: 5 minutos**
**🎯 Éxito esperado: 99%**
