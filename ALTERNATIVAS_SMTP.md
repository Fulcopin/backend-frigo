# 🔧 ALTERNATIVA: Usar Otro Proveedor SMTP (Sin Gmail)

Si tienes problemas con Gmail, puedes usar otros proveedores:

---

## 📧 OPCIÓN 1: Mailtrap (Para Testing - GRATIS)

### Ventajas:
- ✅ No requiere dominio propio
- ✅ Funciona inmediatamente
- ✅ Ver todos los emails en una bandeja web
- ✅ Perfecto para desarrollo

### Configuración:

1. Crear cuenta en: https://mailtrap.io/
2. Ir a "Email Testing" → "Inboxes" → "My Inbox"
3. Copiar credenciales SMTP
4. Editar `appsettings.json`:

```json
"GmailSettings": {
  "SmtpServer": "sandbox.smtp.mailtrap.io",
  "SmtpPort": 2525,
  "EnableSsl": true,
  "SenderEmail": "alertas@frigolab.com",
  "SenderPassword": "TU-PASSWORD-DE-MAILTRAP",
  "SenderName": "Frigolab Alertas"
}
```

---

## 📧 OPCIÓN 2: SendGrid (Para Producción - 100 emails/día GRATIS)

### Ventajas:
- ✅ 100 emails gratis por día
- ✅ API key simple
- ✅ Mejor deliverability que Gmail
- ✅ Estadísticas de emails

### Configuración:

1. Crear cuenta en: https://sendgrid.com/
2. Ir a "Settings" → "API Keys" → "Create API Key"
3. Copiar API Key
4. Instalar paquete:
```powershell
dotnet add package SendGrid
```

5. Actualizar `GmailService.cs`:

```csharp
using SendGrid;
using SendGrid.Helpers.Mail;

public async Task<bool> SendAlertEmailAsync(string toEmail, string subject, string body)
{
    try
    {
        var apiKey = _configuration["SendGridSettings:ApiKey"];
        var client = new SendGridClient(apiKey);
        
        var from = new EmailAddress(_configuration["SendGridSettings:SenderEmail"], 
                                     _configuration["SendGridSettings:SenderName"]);
        var to = new EmailAddress(toEmail);
        
        var msg = MailHelper.CreateSingleEmail(from, to, subject, "", body);
        var response = await client.SendEmailAsync(msg);
        
        return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error sending email to {toEmail}");
        return false;
    }
}
```

6. Editar `appsettings.json`:
```json
"SendGridSettings": {
  "ApiKey": "SG.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "SenderEmail": "alertas@frigolab.com",
  "SenderName": "Frigolab Alertas"
}
```

---

## 📧 OPCIÓN 3: Mailgun (100 emails/día GRATIS)

Similar a SendGrid, con API key.

1. Crear cuenta en: https://www.mailgun.com/
2. Verificar dominio o usar sandbox domain
3. Obtener API key
4. Configurar similar a SendGrid

---

## 📧 OPCIÓN 4: Outlook/Hotmail (SMTP Directo)

### Configuración:

```json
"GmailSettings": {
  "SmtpServer": "smtp-mail.outlook.com",
  "SmtpPort": 587,
  "EnableSsl": true,
  "SenderEmail": "tu-email@outlook.com",
  "SenderPassword": "TU-CONTRASEÑA-OUTLOOK",
  "SenderName": "Frigolab Alertas"
}
```

**Nota:** Outlook también requiere "App Password" si tienes 2FA activado.

---

## 📧 OPCIÓN 5: SMTP Propio (Si tienes servidor)

Si tienes un servidor con dominio propio (ejemplo: `@frigolab.com`):

```json
"GmailSettings": {
  "SmtpServer": "smtp.tudominio.com",
  "SmtpPort": 587,
  "EnableSsl": true,
  "SenderEmail": "alertas@frigolab.com",
  "SenderPassword": "TU-CONTRASEÑA",
  "SenderName": "Frigolab Alertas"
}
```

---

## 🎯 RECOMENDACIÓN

### Para Desarrollo:
**Mailtrap** - Rápido, gratis, sin complicaciones

### Para Producción:
**SendGrid** - Profesional, confiable, 100 emails/día gratis

### Para Uso Personal:
**Gmail con App Password** - Funciona bien si configuras correctamente

---

## 📞 SOPORTE

Si prefieres alguna de estas alternativas, avísame y te ayudo a configurarla.

**¡Éxito! 🚀**
