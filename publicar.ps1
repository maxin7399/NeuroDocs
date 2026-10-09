# Genera publish\NeuroDocs-<versión>.zip listo para entregar.
# Uso (desde la raíz del repositorio):  .\publicar.ps1

$ErrorActionPreference = "Stop"

$proyecto = Join-Path $PSScriptRoot "src\NeuroDocs\NeuroDocs.csproj"
[xml]$csproj = Get-Content $proyecto -Encoding UTF8
$version = $csproj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No se encontró <Version> en NeuroDocs.csproj" }

$nombre = "NeuroDocs-$version"
$carpeta = Join-Path $PSScriptRoot "publish\$nombre"
$zip = Join-Path $PSScriptRoot "publish\$nombre.zip"

if (Test-Path $carpeta) { Remove-Item $carpeta -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "Publicando NeuroDocs $version..." -ForegroundColor Cyan
dotnet publish $proyecto -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $carpeta
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló" }

# Instrucciones para el usuario final, dentro de la carpeta
@"
NeuroDocs $version
==================

INSTALACIÓN
1. Antes de descomprimir: clic derecho sobre el .zip > Propiedades > marcar "Desbloquear" > Aceptar.
2. Descomprimir la carpeta en un lugar fijo, por ejemplo Documentos.
3. Clic derecho sobre NeuroDocs.exe > Enviar a > Escritorio (crear acceso directo).
4. Abrir NeuroDocs > Configuración > pegar la clave de Gemini > Probar conexión > Guardar.

REQUISITOS
- Windows 10 u 11.
- Microsoft Word instalado y activado (se usa para generar el PDF).

ACTUALIZAR A UNA VERSIÓN NUEVA
Cerrar NeuroDocs, borrar la carpeta anterior y descomprimir la nueva en el mismo lugar.
La configuración (clave de Gemini y modelo) se conserva.

No modificar ni mover las carpetas Plantillas e IA: la aplicación las necesita junto a NeuroDocs.exe.
"@ | Set-Content (Join-Path $carpeta "LEEME.txt") -Encoding UTF8

Compress-Archive -Path $carpeta -DestinationPath $zip
$tamano = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Listo: $zip ($tamano MB)" -ForegroundColor Green
