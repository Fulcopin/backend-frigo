# Script para corregir todas las referencias a propiedades en ConsumptionsController

$file = "c:\Users\fupifigu\Desktop\diagramas\sillos\dinamic-generador\backend-frigo\Controllers\ConsumptionsController.cs"

# Leer el contenido
$content = Get-Content $file -Raw

# Reemplazos
$content = $content -replace 'f\.FechaCreacion', 'f.CreatedAt'
$content = $content -replace 'form\.FechaCreacion', 'form.CreatedAt'

# Guardar
$content | Set-Content $file -NoNewline

Write-Host "✅ Correcciones aplicadas a ConsumptionsController.cs" -ForegroundColor Green
