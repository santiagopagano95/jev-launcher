param([switch]$Show)
$ErrorActionPreference = 'Stop'
if (-not $env:TYPESAFE_API_KEY) {
  Write-Host 'TYPESAFE_API_KEY is not set — launcher will run in local matching only mode.'
}
dotnet run --project src/JevLauncher.App
