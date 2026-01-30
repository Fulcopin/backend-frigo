# Diagnóstico rápido del historial de versiones
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "DIAGNÓSTICO DEL HISTORIAL DE VERSIONES" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

$connectionString = "Server=tcp:fdjfdfd-ff.database.windows.net,1433;Initial Catalog=FormBuilder-rg;User ID=Superadmin;Password=P12345678`$;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    Write-Host "✓ Conexión exitosa a la base de datos`n" -ForegroundColor Green

    # 1. Contar registros totales en TemplateVersions
    Write-Host "1. Total de registros en TemplateVersions:" -ForegroundColor Yellow
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM TemplateVersions"
    $count = $cmd.ExecuteScalar()
    Write-Host "   Total: $count registros`n" -ForegroundColor White

    # 2. Buscar el template "nnjknjl"
    Write-Host "2. Buscando template 'nnjknjl':" -ForegroundColor Yellow
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT TemplateID, Nombre, Version FROM Templates WHERE Nombre LIKE '%nnjknjl%'"
    $reader = $cmd.ExecuteReader()
    
    $templateId = $null
    if ($reader.Read()) {
        $templateId = $reader["TemplateID"]
        $templateName = $reader["Nombre"]
        $currentVersion = $reader["Version"]
        Write-Host "   ✓ Template encontrado:" -ForegroundColor Green
        Write-Host "     ID: $templateId" -ForegroundColor White
        Write-Host "     Nombre: $templateName" -ForegroundColor White
        Write-Host "     Versión actual: $currentVersion`n" -ForegroundColor White
    } else {
        Write-Host "   ✗ Template NO encontrado`n" -ForegroundColor Red
    }
    $reader.Close()

    # 3. Si encontramos el template, buscar sus versiones
    if ($templateId) {
        Write-Host "3. Versiones en TemplateVersions para este template:" -ForegroundColor Yellow
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = @"
SELECT 
    Version,
    CreatedAt,
    ChangeDescription,
    ModifiedBy
FROM TemplateVersions 
WHERE TemplateID = @templateId
ORDER BY CreatedAt DESC
"@
        $cmd.Parameters.AddWithValue("@templateId", $templateId) | Out-Null
        $reader = $cmd.ExecuteReader()
        
        $versionCount = 0
        while ($reader.Read()) {
            $versionCount++
            Write-Host "   Versión: $($reader['Version'])" -ForegroundColor Cyan
            Write-Host "     Creada: $($reader['CreatedAt'])" -ForegroundColor White
            Write-Host "     Descripción: $($reader['ChangeDescription'])" -ForegroundColor White
            Write-Host "     Modificado por: $($reader['ModifiedBy'])`n" -ForegroundColor White
        }
        $reader.Close()
        
        if ($versionCount -eq 0) {
            Write-Host "   ✗ NO hay versiones guardadas para este template" -ForegroundColor Red
            Write-Host "   ℹ CAUSA PROBABLE: No has cambiado el número de versión al editar`n" -ForegroundColor Yellow
        } else {
            Write-Host "   ✓ Total de versiones encontradas: $versionCount`n" -ForegroundColor Green
        }
    }

    # 4. Ver todas las plantillas con versiones guardadas
    Write-Host "4. Resumen de todas las plantillas con historial:" -ForegroundColor Yellow
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = @"
SELECT 
    t.Nombre,
    COUNT(tv.Version) as CantidadVersiones
FROM Templates t
LEFT JOIN TemplateVersions tv ON t.TemplateID = tv.TemplateID
GROUP BY t.Nombre
HAVING COUNT(tv.Version) > 0
ORDER BY CantidadVersiones DESC
"@
    $reader = $cmd.ExecuteReader()
    
    while ($reader.Read()) {
        Write-Host "   $($reader['Nombre']): $($reader['CantidadVersiones']) versiones" -ForegroundColor White
    }
    $reader.Close()

    $connection.Close()
    
    Write-Host "`n================================================" -ForegroundColor Cyan
    Write-Host "IMPORTANTE:" -ForegroundColor Yellow
    Write-Host "El sistema solo guarda versiones cuando CAMBIAS" -ForegroundColor White
    Write-Host "el NÚMERO DE VERSIÓN en la plantilla." -ForegroundColor White
    Write-Host "`nPara probar:" -ForegroundColor Green
    Write-Host "1. Edita tu plantilla 'nnjknjl'" -ForegroundColor White
    Write-Host "2. Cambia la versión de '1' a '2'" -ForegroundColor White
    Write-Host "3. Guarda los cambios" -ForegroundColor White
    Write-Host "4. Verifica el historial nuevamente" -ForegroundColor White
    Write-Host "================================================`n" -ForegroundColor Cyan

} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
