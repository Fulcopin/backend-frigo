using FormBuilder.API.Data;
using FormBuilder.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

/*builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "http://localhost:3000",
                "https://frigo-fron.onrender.com",  // ✅ Producción Render
                "http://192.168.0.88:8096"           // ✅ Servidor empresa
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // ✅ AGREGADO: permite envío de token Bearer
        });
});*/

// Configuración CORS - permitir cualquier origen
builder.Services.AddCors(options =>
{
   options.AddPolicy(name: MyAllowSpecificOrigins,
                     policy =>
                     {
                        // Permitir cualquier origen
                        policy.AllowAnyOrigin()
                               .AllowAnyHeader()
                               .AllowAnyMethod();
                     });
});

// DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Email y Alertas
builder.Services.AddScoped<IEmailService, GmailService>();
builder.Services.AddHostedService<AlertBackgroundService>();

// ✅ Módulo de Personal: validación de reglas de tiempo por proceso
builder.Services.AddScoped<PersonalValidationService>();

// 📋 Cuadro de cumplimiento (pantalla) y su resumen por correo cada X horas.
builder.Services.AddScoped<CumplimientoService>();
builder.Services.AddHostedService<ResumenCumplimientoService>();

// ✅ Regla de horarios (hora de fin vs inicio) para formularios llenados.
//    Se configura en appsettings.json → "ReglasHorario" (opcional).
builder.Services.Configure<ReglasHorarioOptions>(builder.Configuration.GetSection("ReglasHorario"));
builder.Services.AddSingleton<IValidadorHorarios, ValidadorHorarios>();

// ✅ HttpClient para ProxyController
builder.Services.AddHttpClient(); // ✅ AGREGADO: necesario para IHttpClientFactory
builder.Services.AddHttpClient("ExternalApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
});

builder.Services.AddOpenApi();

// Documento OpenAPI (el "swagger") para poder probar la API desde Postman o
// cualquier cliente: se sirve en /openapi/v1.json. Usa el paquete
// Microsoft.AspNetCore.OpenApi que el proyecto ya tenia; no agrega dependencias.
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ⚠️ ORDEN CRÍTICO DEL PIPELINE:
app.UseRouting();                        // 1️⃣ AGREGADO: Routing primero
app.UseCors(MyAllowSpecificOrigins);     // 2️⃣ CORS antes de Auth
//app.UseHttpsRedirection();             // Comentado (Render maneja HTTPS)
app.UseAuthentication();                 // 3️⃣ AGREGADO: Autenticación
app.UseAuthorization();                  // 4️⃣ Autorización
app.MapOpenApi();                        // 4️⃣ /openapi/v1.json (contrato de la API)
app.MapControllers();                    // 5️⃣ Controladores al final

// --- Parche de esquema (idempotente) ---
// TemplateChangeLogs.CambioRealizado nacio como nvarchar(1000): al guardar una
// descripcion mas larga el INSERT fallaba y el navegador mostraba "Failed to
// fetch". Se amplia a nvarchar(max) al arrancar para no depender de correr el
// script SQL a mano. Si el usuario de SQL no puede alterar la tabla, se deja
// una advertencia en el log y la API sigue funcionando igual.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.ExecuteSqlRaw(
        "IF COL_LENGTH('dbo.TemplateChangeLogs','CambioRealizado') BETWEEN 1 AND 8000 " +
        "ALTER TABLE dbo.TemplateChangeLogs ALTER COLUMN CambioRealizado nvarchar(max) NOT NULL;");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "No se pudo ampliar TemplateChangeLogs.CambioRealizado a nvarchar(max)");
}

app.Run();
