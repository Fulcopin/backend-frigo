# ========================================
# AGREGAR CAMPO FECHA AL TEMPLATE
# ========================================

Write-Host "[INFO] Agregando campo 'Fecha' al template..." -ForegroundColor Cyan
Write-Host ""

# Configuracion de Azure SQL
$ServerName = "fdjfdfd-ff.database.windows.net"
$DatabaseName = "FormBuilder-rg"
$Username = "Superadmin"
$Password = "P12345678`$"
$ConnectionString = "Server=tcp:$ServerName,1433;Initial Catalog=$DatabaseName;Persist Security Info=False;User ID=$Username;Password=$Password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

try {
    Write-Host "[INFO] Conectando a Azure SQL Database..." -ForegroundColor Yellow
    $Connection = New-Object System.Data.SqlClient.SqlConnection
    $Connection.ConnectionString = $ConnectionString
    $Connection.Open()
    Write-Host "[OK] Conexion establecida" -ForegroundColor Green
    Write-Host ""
    
    # =====================================================
    # PASO 1: Ver HeaderFields actual
    # =====================================================
    Write-Host "[PASO 1] Verificando HeaderFields actual..." -ForegroundColor Yellow
    
    $Query1 = @"
SELECT HeaderFields
FROM Templates
WHERE Codigo = 'FRM-TINAS-15-VERTICAL'
"@
    
    $Command1 = $Connection.CreateCommand()
    $Command1.CommandText = $Query1
    $Reader1 = $Command1.ExecuteReader()
    
    $currentHeaderFields = $null
    if ($Reader1.Read()) {
        $currentHeaderFields = $Reader1["HeaderFields"]
        if ([string]::IsNullOrWhiteSpace($currentHeaderFields)) {
            Write-Host "   HeaderFields esta vacio o NULL" -ForegroundColor Gray
        } else {
            Write-Host "   HeaderFields actual:" -ForegroundColor Gray
            Write-Host "   $currentHeaderFields" -ForegroundColor White
        }
    } else {
        Write-Host "   [ERROR] Template FRM-TINAS-15-VERTICAL no encontrado" -ForegroundColor Red
        $Reader1.Close()
        $Connection.Close()
        exit 1
    }
    $Reader1.Close()
    Write-Host ""
    
    # =====================================================
    # PASO 2: Verificar si ya tiene campo "fecha"
    # =====================================================
    Write-Host "[PASO 2] Verificando si existe campo 'fecha'..." -ForegroundColor Yellow
    
    if ($currentHeaderFields -like '*"fecha"*') {
        Write-Host "   [OK] El campo 'fecha' ya existe en HeaderFields" -ForegroundColor Green
        Write-Host "   No es necesario actualizar" -ForegroundColor Gray
    } else {
        Write-Host "   [INFO] El campo 'fecha' NO existe" -ForegroundColor Yellow
        Write-Host "   Agregando campo 'fecha'..." -ForegroundColor Yellow
        
        # Crear nuevo JSON con campo fecha
        $newHeaderFields = @"
[
  {
    "name": "fecha",
    "label": "Fecha",
    "type": "date",
    "required": true,
    "placeholder": ""
  }
]
"@
        
        # Si ya tiene campos, necesitarías parsear el JSON y agregar
        # Por ahora, reemplazamos todo (asumiendo que está vacío)
        
        $Query2 = @"
UPDATE Templates
SET HeaderFields = '$($newHeaderFields -replace "'", "''")'
WHERE Codigo = 'FRM-TINAS-15-VERTICAL'
  AND (HeaderFields IS NULL OR HeaderFields = '[]' OR HeaderFields = '' OR LEN(LTRIM(RTRIM(HeaderFields))) = 0)
"@
        
        $Command2 = $Connection.CreateCommand()
        $Command2.CommandText = $Query2
        $RowsAffected = $Command2.ExecuteNonQuery()
        
        if ($RowsAffected -gt 0) {
            Write-Host "   [OK] Campo 'fecha' agregado exitosamente" -ForegroundColor Green
            Write-Host "   Filas actualizadas: $RowsAffected" -ForegroundColor Gray
        } else {
            Write-Host "   [ADVERTENCIA] No se actualizó ninguna fila" -ForegroundColor Yellow
            Write-Host "   El template podría tener campos existentes" -ForegroundColor Yellow
            Write-Host "   Necesitas agregar 'fecha' manualmente al JSON" -ForegroundColor Yellow
        }
    }
    Write-Host ""
    
    # =====================================================
    # PASO 3: Verificar resultado final
    # =====================================================
    Write-Host "[PASO 3] Verificando resultado final..." -ForegroundColor Yellow
    
    $Query3 = @"
SELECT 
    TemplateID,
    Codigo,
    Nombre,
    HeaderFields,
    CASE 
        WHEN HeaderFields LIKE '%fecha%' THEN 'SI'
        ELSE 'NO'
    END AS TieneCampoFecha
FROM Templates
WHERE Codigo = 'FRM-TINAS-15-VERTICAL'
"@
    
    $Command3 = $Connection.CreateCommand()
    $Command3.CommandText = $Query3
    $Reader3 = $Command3.ExecuteReader()
    
    if ($Reader3.Read()) {
        Write-Host "   Template ID: $($Reader3['TemplateID'])" -ForegroundColor Gray
        Write-Host "   Codigo: $($Reader3['Codigo'])" -ForegroundColor Gray
        Write-Host "   Nombre: $($Reader3['Nombre'])" -ForegroundColor Gray
        Write-Host "   Tiene campo fecha: $($Reader3['TieneCampoFecha'])" -ForegroundColor $(if ($Reader3['TieneCampoFecha'] -eq 'SI') { 'Green' } else { 'Red' })
        
        if ($Reader3['TieneCampoFecha'] -eq 'SI') {
            Write-Host ""
            Write-Host "================================================" -ForegroundColor Cyan
            Write-Host "[OK] CAMPO FECHA CONFIGURADO CORRECTAMENTE" -ForegroundColor Green
            Write-Host "================================================" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "[SIGUIENTE PASO]" -ForegroundColor Yellow
            Write-Host "   1. Reinicia el frontend (Ctrl+C y npm run dev)" -ForegroundColor White
            Write-Host "   2. Abre el formulario 'Registro 15 Tinas'" -ForegroundColor White
            Write-Host "   3. Veras el campo 'Fecha' en 'Informacion General'" -ForegroundColor White
            Write-Host "   4. Selecciona una fecha" -ForegroundColor White
            Write-Host "   5. Guarda el formulario" -ForegroundColor White
            Write-Host "   6. Exporta a PDF - la fecha aparecera en el encabezado" -ForegroundColor White
        } else {
            Write-Host ""
            Write-Host "[ADVERTENCIA] El campo fecha NO se detecto" -ForegroundColor Yellow
            Write-Host "   Ejecuta manualmente el script SQL en:" -ForegroundColor Gray
            Write-Host "   backend-frigo\Migrations\AgregarCampoFechaTemplate.sql" -ForegroundColor White
        }
    }
    $Reader3.Close()
    
    $Connection.Close()
    
} catch {
    Write-Host ""
    Write-Host "[ERROR] al ejecutar la migracion:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    if ($Connection.State -eq 'Open') {
        $Connection.Close()
    }
    exit 1
}

Write-Host ""
Write-Host "Presiona Enter para continuar..." -ForegroundColor Cyan
$null = Read-Host
