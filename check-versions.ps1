Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "DIAGNOSTICO DEL HISTORIAL DE VERSIONES" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$connectionString = "Server=tcp:fdjfdfd-ff.database.windows.net,1433;Initial Catalog=FormBuilder-rg;User ID=Superadmin;Password=P12345678`$;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    Write-Host "Conexion exitosa" -ForegroundColor Green

    # Total de registros
    Write-Host "`n1. Total de registros en TemplateVersions:" -ForegroundColor Yellow
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM TemplateVersions"
    $count = $cmd.ExecuteScalar()
    Write-Host "   Total: $count registros" -ForegroundColor White

    # Buscar template nnjknjl
    Write-Host "`n2. Buscando template 'nnjknjl':" -ForegroundColor Yellow
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT TemplateID, Nombre, Version FROM Templates WHERE Nombre LIKE '%nnjknjl%'"
    $reader = $cmd.ExecuteReader()
    
    $templateId = $null
    if ($reader.Read()) {
        $templateId = $reader["TemplateID"]
        $templateName = $reader["Nombre"]
        $currentVersion = $reader["Version"]
        Write-Host "   Template encontrado:" -ForegroundColor Green
        Write-Host "   ID: $templateId" -ForegroundColor White
        Write-Host "   Nombre: $templateName" -ForegroundColor White
        Write-Host "   Version actual: $currentVersion" -ForegroundColor White
    } else {
        Write-Host "   Template NO encontrado" -ForegroundColor Red
    }
    $reader.Close()

    # Versiones del template
    if ($templateId) {
        Write-Host "`n3. Versiones guardadas para este template:" -ForegroundColor Yellow
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT Version, CreatedAt, ChangeDescription, ModifiedBy FROM TemplateVersions WHERE TemplateID = @templateId ORDER BY CreatedAt DESC"
        $param = $cmd.Parameters.Add("@templateId", [System.Data.SqlDbType]::Int)
        $param.Value = $templateId
        $reader = $cmd.ExecuteReader()
        
        $versionCount = 0
        while ($reader.Read()) {
            $versionCount++
            $ver = $reader["Version"]
            $created = $reader["CreatedAt"]
            $desc = $reader["ChangeDescription"]
            $modBy = $reader["ModifiedBy"]
            Write-Host "   Version: $ver" -ForegroundColor Cyan
            Write-Host "   Creada: $created" -ForegroundColor White
            Write-Host "   Descripcion: $desc" -ForegroundColor White
            Write-Host "   Modificado por: $modBy" -ForegroundColor White
            Write-Host ""
        }
        $reader.Close()
        
        if ($versionCount -eq 0) {
            Write-Host "   NO hay versiones guardadas" -ForegroundColor Red
            Write-Host "   CAUSA: No has cambiado el numero de version al editar" -ForegroundColor Yellow
        } else {
            Write-Host "   Total versiones: $versionCount" -ForegroundColor Green
        }
    }

    # Todas las plantillas con historial
    Write-Host "`n4. Plantillas con historial:" -ForegroundColor Yellow
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT t.Nombre, COUNT(tv.Version) as Total FROM Templates t LEFT JOIN TemplateVersions tv ON t.TemplateID = tv.TemplateID GROUP BY t.Nombre HAVING COUNT(tv.Version) > 0 ORDER BY Total DESC"
    $reader = $cmd.ExecuteReader()
    
    while ($reader.Read()) {
        $nombre = $reader["Nombre"]
        $total = $reader["Total"]
        Write-Host "   $nombre : $total versiones" -ForegroundColor White
    }
    $reader.Close()

    $connection.Close()
    
    Write-Host "`n==========================================" -ForegroundColor Cyan
    Write-Host "IMPORTANTE:" -ForegroundColor Yellow
    Write-Host "El sistema solo guarda versiones cuando" -ForegroundColor White
    Write-Host "CAMBIAS el NUMERO DE VERSION" -ForegroundColor White
    Write-Host "`nPara probar:" -ForegroundColor Green
    Write-Host "1. Edita tu plantilla 'nnjknjl'" -ForegroundColor White
    Write-Host "2. Cambia la version de '1' a '2'" -ForegroundColor White
    Write-Host "3. Guarda los cambios" -ForegroundColor White
    Write-Host "4. Verifica el historial" -ForegroundColor White
    Write-Host "==========================================" -ForegroundColor Cyan

} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
