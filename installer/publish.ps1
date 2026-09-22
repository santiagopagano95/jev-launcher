# Helper compartido: publica la app una sola vez para reutilizar el output.
$script:JevRepoRoot = Split-Path -Parent $PSScriptRoot

function Invoke-JevPublish {
    param(
        [Parameter(Mandatory = $true)][string]$Output,
        [string]$Version = "1.0.0",
        [string]$Configuration = "Release"
    )
    $ErrorActionPreference = 'Stop'
    dotnet publish (Join-Path $script:JevRepoRoot 'src\JevLauncher.App\JevLauncher.App.csproj') `
        -c $Configuration -r win-x64 --self-contained false -p:Version=$Version `
        -o $Output
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo con codigo $LASTEXITCODE" }
}
