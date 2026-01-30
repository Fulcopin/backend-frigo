# ========================================
# SCRIPT PARA EJECUTAR MIGRACION EN AZURE SQL DATABASE
# ========================================

Write-Host "[INFO] Ejecutando migracion para agregar FechaVersion..." -ForegroundColor Cyan
Write-Host ""

# Configuracion de Azure SQL
$ServerName = "fdjfdfd-ff.database.windows.net"
$DatabaseName = "FormBuilder-rg"
$Username = "Superadmin"
$Password = "P12345678`$"

# Construir la cadena de conexion
$ConnectionString = "Server=tcp:$ServerName,1433;Initial Catalog=$DatabaseName;Persist Security Info=False;User ID=$Username;Password=$Password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

try {
    Write-Host "[INFO] Conectando a Azure SQL Database..." -ForegroundColor Yellow
    Write-Host "   Servidor: $ServerName" -ForegroundColor Gray
    Write-Host "   Base de datos: $DatabaseName" -ForegroundColor Gray
    Write-Host ""
    
    # Crear conexion
    $Connection = New-Object System.Data.SqlClient.SqlConnection
    $Connection.ConnectionString = $ConnectionString
    $Connection.Open()
    
    Write-Host "[OK] Conexion establecida exitosamente" -ForegroundColor Green
    Write-Host ""
    
    # =====================================================
    # PASO 1: Agregar la columna FechaVersion
    # =====================================================
    Write-Host "[PASO 1] Verificando y agregando columna FechaVersion..." -ForegroundColor Yellow
    
    $Step1SQL = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FilledForms]') AND name = 'FechaVersion')
BEGIN
    ALTER TABLE [dbo].[FilledForms]
    ADD [FechaVersion] DATETIME2 NULL;
    SELECT 'Columna FechaVersion agregada correctamente' AS Resultado;
END
ELSE
BEGIN
    SELECT 'La columna FechaVersion ya existe' AS Resultado;
END
"@
    
    $Command1 = $Connection.CreateCommand()
    $Command1.CommandText = $Step1SQL
    $Command1.CommandTimeout = 120
    $Reader1 = $Command1.ExecuteReader()
    
    if ($Reader1.Read()) {
        Write-Host "   $($Reader1['Resultado'])" -ForegroundColor Gray
    }
    $Reader1.Close()
    Write-Host ""
    
    # =====================================================
    # PASO 2: Actualizar registros existentes
    # =====================================================
    Write-Host "[PASO 2] Actualizando registros existentes..." -ForegroundColor Yellow
    
    $Step2SQL = @"
UPDATE [dbo].[FilledForms]
SET FechaVersion = CreatedAt
WHERE FechaVersion IS NULL AND TemplateVersion IS NOT NULL;

SELECT @@ROWCOUNT AS RegistrosActualizados;
"@
    
    $Command2 = $Connection.CreateCommand()
    $Command2.CommandText = $Step2SQL
    $Command2.CommandTimeout = 120
    $Reader2 = $Command2.ExecuteReader()
    
    if ($Reader2.Read()) {
        Write-Host "   Registros actualizados: $($Reader2['RegistrosActualizados'])" -ForegroundColor Gray
    }
    $Reader2.Close()
    Write-Host ""
    
    # =====================================================
    # PASO 3: Verificar instalacion
    # =====================================================
    Write-Host "[PASO 3] Verificando que la columna FechaVersion existe..." -ForegroundColor Yellow
    
    $Step3SQL = @"
SELECT 
    COLUMN_NAME AS Columna, 
    DATA_TYPE AS Tipo, 
    IS_NULLABLE AS Nullable
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FilledForms' AND COLUMN_NAME = 'FechaVersion'
"@
    
    $Command3 = $Connection.CreateCommand()
    $Command3.CommandText = $Step3SQL
    $Reader3 = $Command3.ExecuteReader()
    
    if ($Reader3.Read()) {
        Write-Host "[OK] Columna FechaVersion encontrada:" -ForegroundColor Green
        Write-Host "   - Nombre: $($Reader3['Columna'])" -ForegroundColor Gray
        Write-Host "   - Tipo: $($Reader3['Tipo'])" -ForegroundColor Gray
        Write-Host "   - Permite NULL: $($Reader3['Nullable'])" -ForegroundColor Gray
    } else {
        Write-Host "[ERROR] La columna FechaVersion NO se encontro" -ForegroundColor Red
    }
    $Reader3.Close()
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "[OK] MIGRACION COMPLETADA CON EXITO" -ForegroundColor Green
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "[INFO] Ahora puedes iniciar el backend con:" -ForegroundColor Yellow
    Write-Host "   dotnet run" -ForegroundColor White
    Write-Host ""
    
    $Connection.Close()
    
} catch {
    Write-Host ""
    Write-Host "[ERROR] al ejecutar la migracion:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "[INFO] Sugerencias:" -ForegroundColor Yellow
    Write-Host "   1. Verifica que las credenciales sean correctas" -ForegroundColor Gray
    Write-Host "   2. Verifica que tu IP este permitida en el firewall de Azure" -ForegroundColor Gray
    Write-Host "   3. Verifica la conexion a internet" -ForegroundColor Gray
    Write-Host ""
    if ($Connection.State -eq 'Open') {
        $Connection.Close()
    }
    exit 1
}
