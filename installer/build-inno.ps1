param(
    [string]$Version = "1.0.0",
    [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$payload = Join-Path $PSScriptRoot 'inno-payload'

if (-not (Test-Path $Iscc)) {
    throw "No encontre ISCC.exe en '$Iscc'. Instala Inno Setup 6 (winget install JRSoftware.InnoSetup)."
}

Remove-Item $payload -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payload | Out-Null

dotnet publish (Join-Path $root 'src\JevLauncher.App\JevLauncher.App.csproj') `
    -c Release -r win-x64 --self-contained false -p:Version=$Version `
    -o $payload

& $Iscc "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot 'inno\JevLauncher.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC fallo con codigo $LASTEXITCODE" }

$setup = Join-Path $PSScriptRoot "dist\JevLauncher-Setup-$Version.exe"
Write-Host "Instalador generado: $setup"
