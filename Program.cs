using FormBuilder.API.Data;
using FormBuilder.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
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
});

// DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Email y Alertas
builder.Services.AddScoped<IEmailService, GmailService>();
builder.Services.AddHostedService<AlertBackgroundService>();

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
app.MapControllers();                    // 5️⃣ Controladores al final

app.Run();