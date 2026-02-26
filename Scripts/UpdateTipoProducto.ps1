# Script para actualizar TipoProducto en la base de datos
# Elimina emojis y los reemplaza por texto plano

$connectionString = "Server=.\SQLEXPRESS;Database=FormBuilder-rg;Trusted_Connection=True;TrustServerCertificate=True"

Write-Host "Conectando a la base de datos..." -ForegroundColor Yellow

try {
    # Crear conexion
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    
    Write-Host "Conectado exitosamente" -ForegroundColor Green
    
    # 1. Ver datos actuales
    Write-Host ""
    Write-Host "Formularios con TipoProducto ANTES de actualizar:" -ForegroundColor Cyan
    $querySelect = "SELECT FormID, TipoProducto, CreatedAt, FilledBy FROM FilledForms WHERE TipoProducto IS NOT NULL ORDER BY CreatedAt DESC"
    $commandSelect = New-Object System.Data.SqlClient.SqlCommand($querySelect, $connection)
    $reader = $commandSelect.ExecuteReader()
    
    while ($reader.Read()) {
        $formId = $reader["FormID"]
        $tipoProducto = $reader["TipoProducto"]
        $createdAt = $reader["CreatedAt"]
        $filledBy = $reader["FilledBy"]
        Write-Host "  FormID: $formId | Tipo: $tipoProducto | Fecha: $createdAt | Por: $filledBy"
    }
    $reader.Close()
    
    # 2. Actualizar Camaron
    Write-Host ""
    Write-Host "Actualizando formularios con Camaron..." -ForegroundColor Yellow
    $queryUpdateCamaron = "UPDATE FilledForms SET TipoProducto = 'CAMARON' WHERE TipoProducto LIKE '%Camar%'"
    $commandUpdateCamaron = New-Object System.Data.SqlClient.SqlCommand($queryUpdateCamaron, $connection)
    $rowsAffectedCamaron = $commandUpdateCamaron.ExecuteNonQuery()
    Write-Host "  $rowsAffectedCamaron formularios actualizados (CAMARON)" -ForegroundColor Green
    
    # 3. Actualizar Pescado
    Write-Host ""
    Write-Host "Actualizando formularios con Pescado..." -ForegroundColor Yellow
    $queryUpdatePescado = "UPDATE FilledForms SET TipoProducto = 'PESCADO' WHERE TipoProducto LIKE '%Pescado%'"
    $commandUpdatePescado = New-Object System.Data.SqlClient.SqlCommand($queryUpdatePescado, $connection)
    $rowsAffectedPescado = $commandUpdatePescado.ExecuteNonQuery()
    Write-Host "  $rowsAffectedPescado formularios actualizados (PESCADO)" -ForegroundColor Green
    
    # 4. Ver datos actualizados
    Write-Host ""
    Write-Host "Formularios con TipoProducto DESPUES de actualizar:" -ForegroundColor Cyan
    $querySelectAfter = "SELECT FormID, TipoProducto, CreatedAt, FilledBy FROM FilledForms WHERE TipoProducto IS NOT NULL ORDER BY CreatedAt DESC"
    $commandSelectAfter = New-Object System.Data.SqlClient.SqlCommand($querySelectAfter, $connection)
    $readerAfter = $commandSelectAfter.ExecuteReader()
    
    while ($readerAfter.Read()) {
        $formId = $readerAfter["FormID"]
        $tipoProducto = $readerAfter["TipoProducto"]
        $createdAt = $readerAfter["CreatedAt"]
        $filledBy = $readerAfter["FilledBy"]
        Write-Host "  FormID: $formId | Tipo: $tipoProducto | Fecha: $createdAt | Por: $filledBy"
    }
    $readerAfter.Close()
    
    # 5. Resumen
    Write-Host ""
    Write-Host "RESUMEN:" -ForegroundColor Green
    $queryCount = "SELECT COUNT(*) as Total FROM FilledForms WHERE TipoProducto IN ('CAMARON', 'PESCADO')"
    $commandCount = New-Object System.Data.SqlClient.SqlCommand($queryCount, $connection)
    $total = $commandCount.ExecuteScalar()
    Write-Host "  Total formularios con tipo de producto: $total" -ForegroundColor Green
    
    $connection.Close()
    Write-Host ""
    Write-Host "Actualizacion completada exitosamente" -ForegroundColor Green
    
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}
