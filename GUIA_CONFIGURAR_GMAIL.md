# 📧 GUÍA COMPLETA: Configurar Gmail para Alertas

## ❌ Error Actual

```
Error sending email: The SMTP server requires a secure connection 
or the client was not authenticated. 
The server response was: 5.7.0 Authentication Required.
```

**Causa:** Las credenciales de Gmail en `appsettings.json` están con valores placeholder.

---

## ✅ SOLUCIÓN PASO A PASO

### **PASO 1: Obtener Contraseña de Aplicación de Gmail**

#### 1.1 Ir a tu Cuenta de Google
- Abre tu navegador
- Ve a: https://myaccount.google.com/security

#### 1.2 Activar Verificación en 2 Pasos
- En la sección "Acceso a Google", busca "Verificación en 2 pasos"
- Si no está activada, haz clic en "Activar" y sigue los pasos
- Esto es **OBLIGATORIO** para generar contraseñas de aplicación

#### 1.3 Generar Contraseña de Aplicación
- Una vez activada la verificación en 2 pasos, regresa a:
  https://myaccount.google.com/apppasswords
- O busca "Contraseñas de aplicaciones" en la página de seguridad
- Haz clic en "Contraseñas de aplicaciones"
- Selecciona:
  - **App:** Correo
  - **Dispositivo:** Otro (nombre personalizado)
  - Escribe: **"Frigolab Backend"**
- Haz clic en "Generar"
- **COPIA** la contraseña de 16 caracteres que aparece
  - Ejemplo: `abcd efgh ijkl mnop`
  - **IMPORTANTE:** Esta contraseña solo se muestra una vez

---

### **PASO 2: Configurar appsettings.json**

#### 2.1 Abrir archivo de configuración
```powershell
cd C:\Users\fupifigu\Desktop\diagramas\sillos\dinamic-generador\backend-frigo
notepad appsettings.json
```

#### 2.2 Editar sección GmailSettings

Reemplaza:
```json
"GmailSettings": {
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "EnableSsl": true,
  "SenderEmail": "TU-EMAIL@gmail.com",
  "SenderPassword": "xxxx xxxx xxxx xxxx",
  "SenderName": "Frigolab Alertas"
}
```

Por (con tus datos reales):
```json
"GmailSettings": {
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "EnableSsl": true,
  "SenderEmail": "fstue1@gmail.com",
  "SenderPassword": "abcd efgh ijkl mnop",
  "SenderName": "Frigolab Alertas"
}
```

**REEMPLAZA:**
- `fstue1@gmail.com` → Tu email de Gmail real
- `abcd efgh ijkl mnop` → La contraseña de 16 caracteres que generaste

**IMPORTANTE:** 
- Puedes escribir la contraseña con o sin espacios (ambos funcionan)
- **NO uses tu contraseña normal de Gmail**, usa la contraseña de aplicación

#### 2.3 Guardar y cerrar el archivo

---

### **PASO 3: Reiniciar Backend**

#### 3.1 Detener backend actual
En la terminal donde está corriendo, presiona `Ctrl+C`

#### 3.2 Iniciar nuevamente
```powershell
cd C:\Users\fupifigu\Desktop\diagramas\sillos\dinamic-generador\backend-frigo
dotnet run
```

---

### **PASO 4: Probar Configuración**

#### 4.1 Usando Postman

```http
POST http://localhost:5074/api/Alerts/test
Content-Type: application/json

{
  "email": "fstue1@gmail.com"
}
```

**Respuesta esperada:**
```json
{
  "success": true,
  "message": "Email de prueba enviado exitosamente"
}
```

#### 4.2 Verificar email recibido
- Revisa tu bandeja de entrada de `fstue1@gmail.com`
- Deberías recibir un email con asunto: "🧪 Prueba de Alertas - Frigolab"

---

## 🔍 SOLUCIÓN DE PROBLEMAS

### **Error: "Authentication Required"**
**Causa:** Contraseña incorrecta o no es contraseña de aplicación
**Solución:**
1. Verifica que copiaste la contraseña completa (16 caracteres)
2. Asegúrate de que es una contraseña de aplicación, no tu contraseña normal
3. Genera una nueva contraseña de aplicación si es necesario

### **Error: "Invalid credentials"**
**Causa:** Email incorrecto
**Solución:**
1. Verifica que el email en `SenderEmail` sea exactamente el de tu cuenta Gmail
2. No uses alias, usa el email principal

### **Error: "Connection timeout"**
**Causa:** Firewall o antivirus bloqueando SMTP
**Solución:**
1. Temporalmente desactiva el firewall/antivirus
2. Verifica que el puerto 587 esté abierto
3. Prueba cambiar el puerto a 465 y `UseDefaultCredentials = false`

### **Error: "Less secure app access"**
**Nota:** Google eliminó esta opción en 2022
**Solución:** DEBES usar contraseñas de aplicación (no hay alternativa)

---

## 📝 EJEMPLO COMPLETO DE appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=FormBuilder-rg;Trusted_Connection=True;TrustServerCertificate=True;"
  },
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

---

## 🎯 CHECKLIST RÁPIDO

- [ ] Verificación en 2 pasos activada en Google
- [ ] Contraseña de aplicación generada (16 caracteres)
- [ ] Contraseña copiada completa
- [ ] `appsettings.json` editado con email real
- [ ] `appsettings.json` editado con contraseña de aplicación
- [ ] Backend reiniciado con `dotnet run`
- [ ] Probado endpoint `/api/Alerts/test`
- [ ] Email de prueba recibido correctamente

---

## 🔐 SEGURIDAD

### **NUNCA subas appsettings.json a Git con credenciales reales**

#### Opción 1: Variables de Entorno (RECOMENDADO)

Edita `Program.cs`:
```csharp
builder.Configuration.AddEnvironmentVariables();
```

Crea archivo `.env` (git-ignorado):
```
GMAIL_EMAIL=fstue1@gmail.com
GMAIL_PASSWORD=abcdefghijklmnop
```

Edita `appsettings.json`:
```json
"GmailSettings": {
  "SenderEmail": "${GMAIL_EMAIL}",
  "SenderPassword": "${GMAIL_PASSWORD}",
  ...
}
```

#### Opción 2: appsettings.Development.json

Crea archivo `appsettings.Development.json`:
```json
{
  "GmailSettings": {
    "SenderEmail": "fstue1@gmail.com",
    "SenderPassword": "abcdefghijklmnop"
  }
}
```

Añade a `.gitignore`:
```
appsettings.Development.json
appsettings.Production.json
.env
```

---

## 📞 LINKS ÚTILES

- **Generar contraseña de aplicación:** https://myaccount.google.com/apppasswords
- **Seguridad de cuenta:** https://myaccount.google.com/security
- **Verificación en 2 pasos:** https://myaccount.google.com/signinoptions/two-step-verification

---

## ✅ SIGUIENTE PASO

Una vez configurado Gmail correctamente:
1. El backend podrá enviar emails automáticamente
2. Las alertas de formularios faltantes se enviarán a las 18:00
3. Las alertas de firmas pendientes se enviarán después de 24 horas
4. Podrás probar todos los endpoints de alertas

**¡Éxito! 🚀**
