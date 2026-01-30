// ===== PASO 1: AÑADIR ESTOS 'using' EN LA PARTE SUPERIOR =====
using FormBuilder.API.Data;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          // Aquí pones la dirección de tu aplicación de React
                          policy.WithOrigins(
                            "http://localhost:5173",
                            "http://localhost:5174",  // ✅ Agregado para tu frontend actual
                            "https://frigo-fron.onrender.com") 
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

// 2b. Registrar el DbContext para la conexión a la base de datos
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =======================================================================


builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


// ====================== INICIO DE LA CORRECCIÓN ======================
// El orden aquí es CRÍTICO.
// La política de CORS debe aplicarse ANTES de la redirección a HTTPS.
// De esta forma, el servidor puede responder correctamente a las peticiones
// de "pre-vuelo" (preflight) sin intentar redirigirlas.

// PASO 1: Activar CORS.
app.UseCors(MyAllowSpecificOrigins);

// PASO 2: (Opcional pero recomendado) Redirigir a HTTPS.
//app.UseHttpsRedirection();
// ======================= FIN DE LA CORRECCIÓN ========================


app.UseAuthorization();

app.MapControllers();

app.Run();