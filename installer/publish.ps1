# Helper compartido: publica la app una sola vez para reutilizar el output.
$script:JevInstallerDir = $PSScriptRoot
$script:JevRepoRoot = Split-Path -Parent $PSScriptRoot

function Invoke-JevPublish {
    param(
        [Parameter(Mandatory = $true)][string]$Output,
        [string]$Version = "1.0.0",
        [string]$Configuration = "Release"
    )
    $ErrorActionPreference = 'Stop'
    dotnet publish (Join-Path $script:JevRepoRoot 'src\JevLauncher.App\JevLauncher.App.csproj') `
        -c $Configuration -r win-x64 --self-contained false "-p:Version=$Version" `
        -o $Output
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo con codigo $LASTEXITCODE" }
    if (-not (Test-Path (Join-Path $Output 'JevLauncher.App.exe'))) {
        throw "dotnet publish no genero JevLauncher.App.exe en $Output"
    }
}

function Get-JevInstallerFiles {
    'install.ps1', 'uninstall.ps1', 'install.cmd', 'uninstall.cmd', 'README.txt'
}

function Remove-JevDir {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
        if (Test-Path $Path) { throw "No pude borrar $Path (archivos en uso?)" }
    }
}

function Assert-JevIscc {
    param([string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
    if (-not (Test-Path $Iscc)) {
        throw "No encontre ISCC.exe en '$Iscc'. Instala Inno Setup 6 (winget install JRSoftware.InnoSetup)."
    }
    return $Iscc
}

function Invoke-JevInnoSetup {
    param(
        [string]$Version = "1.0.0",
        [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $Iscc = Assert-JevIscc -Iscc $Iscc
    & $Iscc "/DMyAppVersion=$Version" (Join-Path $script:JevInstallerDir 'inno\JevLauncher.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC fallo con codigo $LASTEXITCODE" }
}
