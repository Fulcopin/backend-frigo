# Script para probar cambio de version y verificar historial
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PRUEBA DE CAMBIO DE VERSION" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$connectionString = "Server=tcp:fdjfdfd-ff.database.windows.net,1433;Initial Catalog=FormBuilder-rg;User ID=Superadmin;Password=P12345678`$;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    Write-Host "Conexion exitosa`n" -ForegroundColor Green

    # Estado ANTES
    Write-Host "ESTADO ANTES DEL CAMBIO:" -ForegroundColor Yellow
    Write-Host "========================`n" -ForegroundColor Yellow
    
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT Version FROM Templates WHERE Nombre LIKE '%nnjknjl%'"
    $versionActual = $cmd.ExecuteScalar()
    Write-Host "Version actual en Templates: $versionActual" -ForegroundColor White
    
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM TemplateVersions WHERE TemplateID = (SELECT TemplateID FROM Templates WHERE Nombre LIKE '%nnjknjl%')"
    $countAntes = $cmd.ExecuteScalar()
    Write-Host "Versiones guardadas en TemplateVersions: $countAntes" -ForegroundColor White
    
    Write-Host "`n----------------------------------------`n" -ForegroundColor Cyan
    Write-Host "INSTRUCCIONES:" -ForegroundColor Yellow
    Write-Host "1. Abre el navegador" -ForegroundColor White
    Write-Host "2. Ve a 'Gestionar Plantillas'" -ForegroundColor White
    Write-Host "3. Edita la plantilla 'nnjknjl'" -ForegroundColor White
    Write-Host "4. Cambia el campo 'Version' de '$versionActual' a '2'" -ForegroundColor White
    Write-Host "5. Guarda la plantilla" -ForegroundColor White
    Write-Host "`nPresiona ENTER cuando hayas guardado los cambios..." -ForegroundColor Green
    Read-Host
    
    # Estado DESPUES
    Write-Host "`nESTADO DESPUES DEL CAMBIO:" -ForegroundColor Yellow
    Write-Host "=========================`n" -ForegroundColor Yellow
    
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT Version FROM Templates WHERE Nombre LIKE '%nnjknjl%'"
    $versionNueva = $cmd.ExecuteScalar()
    Write-Host "Version actual en Templates: $versionNueva" -ForegroundColor White
    
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM TemplateVersions WHERE TemplateID = (SELECT TemplateID FROM Templates WHERE Nombre LIKE '%nnjknjl%')"
    $countDespues = $cmd.ExecuteScalar()
    Write-Host "Versiones guardadas en TemplateVersions: $countDespues" -ForegroundColor White
    
    # Verificacion
    Write-Host "`n----------------------------------------" -ForegroundColor Cyan
    Write-Host "VERIFICACION:" -ForegroundColor Yellow
    
    if ($versionNueva -ne $versionActual) {
        Write-Host "[OK] Version cambio de '$versionActual' a '$versionNueva'" -ForegroundColor Green
    } else {
        Write-Host "[!] Version NO cambio (sigue en '$versionActual')" -ForegroundColor Red
        Write-Host "    Asegurate de cambiar el numero de version" -ForegroundColor Yellow
    }
    
    if ($countDespues -gt $countAntes) {
        Write-Host "[OK] Se guardo nuevo snapshot ($countAntes -> $countDespues)" -ForegroundColor Green
        Write-Host "    El historial ahora tiene mas versiones!" -ForegroundColor Green
        
        # Mostrar versiones
        Write-Host "`nVERSIONES GUARDADAS:" -ForegroundColor Yellow
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT Version, CreatedAt, ChangeDescription FROM TemplateVersions WHERE TemplateID = (SELECT TemplateID FROM Templates WHERE Nombre LIKE '%nnjknjl%') ORDER BY CreatedAt DESC"
        $reader = $cmd.ExecuteReader()
        
        while ($reader.Read()) {
            Write-Host "  Version: $($reader['Version'])" -ForegroundColor Cyan
            Write-Host "  Creada: $($reader['CreatedAt'])" -ForegroundColor White
            Write-Host "  Descripcion: $($reader['ChangeDescription'])" -ForegroundColor White
            Write-Host ""
        }
        $reader.Close()
        
    } else {
        Write-Host "[!] NO se guardo snapshot ($countDespues versiones)" -ForegroundColor Red
        Write-Host "    El numero de version no cambio" -ForegroundColor Yellow
    }
    
    $connection.Close()
    
    Write-Host "----------------------------------------" -ForegroundColor Cyan
    Write-Host "`nAhora ve al frontend y abre el 'Historial de Versiones'" -ForegroundColor Green
    Write-Host "Deberas ver la nueva version en la lista!" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan

} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
