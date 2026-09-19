param(
    [string]$Configuration = 'Release',
    [string]$Output = "$PSScriptRoot\publish"
)

$ErrorActionPreference = 'Stop'

dotnet publish "$PSScriptRoot\src\JevLauncher.App\JevLauncher.App.csproj" `
    -c $Configuration -r win-x64 --self-contained false -o $Output

Write-Host "Published to $Output"
Write-Host "Executable: $Output\JevLauncher.App.exe"
