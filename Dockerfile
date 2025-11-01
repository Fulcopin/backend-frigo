# Etapa 1: Usar la imagen del SDK de .NET para la compilación
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia solo el archivo .csproj a una carpeta con el mismo nombre que el proyecto
# Esto es clave para que la caché de Docker funcione eficientemente.
COPY ["FormBuilder.API/FormBuilder.API.csproj", "FormBuilder.API/"]

# Restaura las dependencias del proyecto
RUN dotnet restore "FormBuilder.API/FormBuilder.API.csproj"

# Copia todo el resto del código fuente del proyecto
COPY . .

# Cambia el directorio de trabajo al del proyecto antes de publicar
WORKDIR "/src/FormBuilder.API"

# Publica la aplicación. Se creará en /app/publish
# El proyecto a publicar se infiere del directorio de trabajo actual
RUN dotnet publish -c Release -o /app/publish

# ---

# Etapa 2: Usar la imagen de runtime de ASP.NET, que es más ligera
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copia solo la aplicación publicada desde la etapa de compilación
COPY --from=build /app/publish .

# El comando para iniciar tu API. Render asignará el puerto automáticamente.
ENTRYPOINT ["dotnet", "FormBuilder.API.dll"]