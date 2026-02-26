# Script para Agregar Fecha y Hora a Firmas Existentes
# Este script actualiza formularios antiguos que no tienen campos de hora en sus firmas

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MIGRACION: Agregar Hora a Firmas" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuracion de conexion
$serverName = "DESKTOP-0EL92HS\SQLEXPRESS"
$databaseName = "FormBuilder-rg"
$connectionString = "Server=$serverName;Database=$databaseName;Trusted_Connection=True;TrustServerCertificate=True;"

try {
    # Conectar a la base de datos
    Write-Host "Conectando a SQL Server..." -ForegroundColor Yellow
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    Write-Host "OK - Conexion exitosa" -ForegroundColor Green
    Write-Host ""

    # Obtener todos los formularios con firmas
    Write-Host "Obteniendo formularios con firmas..." -ForegroundColor Yellow
    $querySelect = @"
SELECT 
    FormID, 
    FirmasData,
    CreatedAt
FROM FilledForms 
WHERE FirmasData IS NOT NULL 
  AND FirmasData != ''
  AND FirmasData != '{}'
ORDER BY FormID
"@

    $command = $connection.CreateCommand()
    $command.CommandText = $querySelect
    $reader = $command.ExecuteReader()

    $formularios = @()
    while ($reader.Read()) {
        $formularios += [PSCustomObject]@{
            FormID = $reader["FormID"]
            FirmasData = $reader["FirmasData"].ToString()
            CreatedAt = $reader["CreatedAt"]
        }
    }
    $reader.Close()

    Write-Host "OK - Se encontraron $($formularios.Count) formularios con firmas" -ForegroundColor Green
    Write-Host ""

    if ($formularios.Count -eq 0) {
        Write-Host "INFO: No hay formularios para actualizar" -ForegroundColor Cyan
        $connection.Close()
        exit
    }

    # Procesar cada formulario
    $actualizados = 0
    $sinCambios = 0
    $errores = 0

    foreach ($form in $formularios) {
        try {
            Write-Host "Procesando FormID: $($form.FormID)..." -ForegroundColor Cyan

            # Parsear el JSON de firmas
            $firmasObj = $form.FirmasData | ConvertFrom-Json
            $modificado = $false

            # Convertir a hashtable para poder modificar
            $firmasHash = @{}
            $firmasObj.PSObject.Properties | ForEach-Object {
                $firmasHash[$_.Name] = $_.Value
            }

            # Iterar sobre cada firma
            foreach ($puesto in $firmasHash.Keys) {
                $firma = $firmasHash[$puesto]
                
                # Verificar si la firma tiene fecha pero no hora
                if ($firma.fecha -and -not $firma.hora) {
                    Write-Host "   Puesto: $puesto - Agregando hora automatica" -ForegroundColor Yellow
                    
                    $createdDate = [DateTime]$form.CreatedAt
                    $firmaFecha = [DateTime]::Parse($firma.fecha)
                    
                    # Si la fecha de la firma coincide con la fecha de creacion, usar la hora de creacion
                    if ($firmaFecha.Date -eq $createdDate.Date) {
                        $hora = $createdDate.ToString("HH:mm")
                    } else {
                        $hora = "08:00"
                    }
                    
                    $firma | Add-Member -NotePropertyName "hora" -NotePropertyValue $hora -Force
                    $firma | Add-Member -NotePropertyName "fechaHoraCapturada" -NotePropertyValue $true -Force
                    $modificado = $true
                    
                    Write-Host "      OK - Hora asignada: $hora" -ForegroundColor Green
                }
                # Verificar si no tiene ni fecha ni hora pero tiene firma
                elseif ($firma.firma -and -not $firma.fecha) {
                    Write-Host "   Puesto: $puesto - Agregando fecha y hora desde CreatedAt" -ForegroundColor Yellow
                    
                    $createdDate = [DateTime]$form.CreatedAt
                    $firma | Add-Member -NotePropertyName "fecha" -NotePropertyValue $createdDate.ToString("yyyy-MM-dd") -Force
                    $firma | Add-Member -NotePropertyName "hora" -NotePropertyValue $createdDate.ToString("HH:mm") -Force
                    $firma | Add-Member -NotePropertyName "fechaHoraCapturada" -NotePropertyValue $true -Force
                    $modificado = $true
                    
                    Write-Host "      OK - Fecha: $($firma.fecha), Hora: $($firma.hora)" -ForegroundColor Green
                }
            }

            # Si se modifico, actualizar en la base de datos
            if ($modificado) {
                $firmasActualizadas = $firmasHash | ConvertTo-Json -Depth 10 -Compress
                
                $queryUpdate = "UPDATE FilledForms SET FirmasData = @FirmasData, UpdatedAt = GETUTCDATE() WHERE FormID = @FormID"

                $cmdUpdate = $connection.CreateCommand()
                $cmdUpdate.CommandText = $queryUpdate
                $cmdUpdate.Parameters.AddWithValue("@FirmasData", $firmasActualizadas) | Out-Null
                $cmdUpdate.Parameters.AddWithValue("@FormID", $form.FormID) | Out-Null
                
                $rowsAffected = $cmdUpdate.ExecuteNonQuery()
                
                if ($rowsAffected -gt 0) {
                    Write-Host "   OK - FormID $($form.FormID) actualizado" -ForegroundColor Green
                    $actualizados++
                } else {
                    Write-Host "   WARNING - FormID $($form.FormID) - No actualizado" -ForegroundColor Yellow
                }
            } else {
                Write-Host "   INFO - FormID $($form.FormID) - Ya tiene fecha y hora" -ForegroundColor Gray
                $sinCambios++
            }

            Write-Host ""

        } catch {
            Write-Host "   ERROR procesando FormID $($form.FormID): $($_.Exception.Message)" -ForegroundColor Red
            $errores++
        }
    }

    # Resumen final
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "RESUMEN DE MIGRACION" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "OK - Formularios actualizados: $actualizados" -ForegroundColor Green
    Write-Host "INFO - Sin cambios necesarios: $sinCambios" -ForegroundColor Cyan
    Write-Host "ERROR - Errores: $errores" -ForegroundColor Red
    Write-Host "Total procesados: $($formularios.Count)" -ForegroundColor White
    Write-Host ""

} catch {
    Write-Host "ERROR CRITICO: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception.StackTrace -ForegroundColor Red
} finally {
    if ($connection.State -eq 'Open') {
        $connection.Close()
        Write-Host "Conexion cerrada" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Script completado" -ForegroundColor Green
Write-Host ""
Read-Host "Presiona Enter para salir"
