# Etapa 1: Usar la imagen del SDK de .NET para la compilación
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia solo el archivo .csproj desde la raíz del proyecto.
# Esto es clave para que la caché de Docker funcione eficientemente.
COPY FormBuilder.API.csproj .

# Restaura las dependencias del proyecto.
RUN dotnet restore

# Copia todo el resto del código fuente del proyecto
COPY . .

# Publica la aplicación, ESPECIFICANDO el archivo .csproj para evitar ambigüedad.
RUN dotnet publish "FormBuilder.API.csproj" -c Release -o /app/publish

# ---

# Etapa 2: Usar la imagen de runtime de ASP.NET, que es más ligera
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copia solo la aplicación publicada desde la etapa de compilación
COPY --from=build /app/publish .

# El comando para iniciar tu API. Render asignará el puerto automáticamente.
ENTRYPOINT ["dotnet", "FormBuilder.API.dll"]