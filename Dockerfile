# Etapa 1: Usar la imagen del SDK de .NET 9 para compilar el proyecto
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copiar el archivo del proyecto (.csproj) y restaurar las dependencias primero
# Esto aprovecha el caché de capas de Docker
COPY *.csproj .
RUN dotnet restore

# Copiar el resto del código fuente de la aplicación
COPY . .

# Publicar la aplicación en modo Release en la carpeta 'out'
RUN dotnet publish -c Release -o out

# Etapa 2: Usar la imagen de runtime de ASP.NET 9, que es más ligera
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copiar solo la aplicación publicada desde la etapa de compilación
COPY --from=build /app/out .

# El comando para iniciar tu API. Render asignará el puerto automáticamente.
ENTRYPOINT ["dotnet", "FormBuilder.API.dll"]