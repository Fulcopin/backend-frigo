// ===== PASO 1: AÑADIR ESTOS 'using' EN LA PARTE SUPERIOR =====
using FormBuilder.API.Data;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// ===== PASO 2: AÑADIR ESTAS LÍNEAS AQUÍ (Configuración de DB y CORS) =====

// 2a. Configuración de CORS para permitir que tu app de React se conecte
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          // Aquí pones la dirección de tu aplicación de React
                          policy.WithOrigins("http://localhost:5173",
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

app.UseHttpsRedirection();

// ===== PASO 3: AÑADIR ESTA LÍNEA AQUÍ (Para activar CORS) =====
app.UseCors(MyAllowSpecificOrigins);
// ==========================================================

app.UseAuthorization();

app.MapControllers();

app.Run();