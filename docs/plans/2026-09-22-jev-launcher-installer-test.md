# Installer Tests + Unified Build Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add hermetic xUnit tests for the script-based installer and a single-publish build that produces byte-identical binaries in the ZIP and the Inno Setup.

**Architecture:** Tests live in the existing `JevLauncher.Tests` project and shell out to the real PowerShell scripts in a `%TEMP%` sandbox. A new `installer/publish.ps1` helper centralizes `dotnet publish`; `build.ps1`, `build-inno.ps1` and the new `build-all.ps1` dot-source it. `build-all.ps1` publishes once and fans the output out to the ZIP and Inno payload.

**Tech Stack:** C# / xUnit (`net10.0-windows`), Windows PowerShell 5.1, .NET 10 SDK, Inno Setup 6 (`ISCC.exe`).

---

### Task 1: Installer test infrastructure + install test (red)

**Files:**
- Create: `tests/JevLauncher.Tests/InstallerScriptTests.cs`

**Step 1: Write the failing test**

Create `tests/JevLauncher.Tests/InstallerScriptTests.cs` with the helper infrastructure and the first test:

```csharp
using System.Diagnostics;

namespace JevLauncher.Tests;

public class InstallerScriptTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "installer", "install.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("No se encontro la raiz del repo (installer/install.ps1).");
    }

    private static string NewSandbox()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jev-installer-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static (int ExitCode, string Output) RunPowerShell(string scriptPath, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);
        return (process.ExitCode, stdout + stderr);
    }

    [Fact]
    public void Install_CopiesPayloadAndUninstaller_ToSandbox()
    {
        var repo = FindRepoRoot();
        var sandbox = NewSandbox();
        try
        {
            var pkg = Path.Combine(sandbox, "pkg");
            Directory.CreateDirectory(Path.Combine(pkg, "app"));
            File.WriteAllText(Path.Combine(pkg, "app", "JevLauncher.App.exe"), "dummy");
            File.Copy(Path.Combine(repo, "installer", "install.ps1"), Path.Combine(pkg, "install.ps1"));
            File.Copy(Path.Combine(repo, "installer", "uninstall.ps1"), Path.Combine(pkg, "uninstall.ps1"));

            var installed = Path.Combine(sandbox, "installed");
            var (code, output) = RunPowerShell(Path.Combine(pkg, "install.ps1"),
                "-InstallDir", installed, "-SkipProcessKill", "-SkipShortcut", "-SkipRegistry", "-NoLaunch");

            Assert.True(code == 0, $"install.ps1 salio con {code}: {output}");
            Assert.True(File.Exists(Path.Combine(installed, "JevLauncher.App.exe")));
            Assert.True(File.Exists(Path.Combine(installed, "uninstall.ps1")));
        }
        finally
        {
            Directory.Delete(sandbox, true);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~InstallerScriptTests"`
Expected: FAIL — PowerShell reports `A parameter cannot be found that matches parameter name 'SkipProcessKill'` (exit code non-zero).

**Step 3: Commit the failing test**

```bash
git add tests/JevLauncher.Tests/InstallerScriptTests.cs
git commit -m "test(installer): failing install test with -SkipProcessKill"
```

---

### Task 2: Add `-SkipProcessKill` to `install.ps1` (green)

**Files:**
- Modify: `installer/install.ps1:1-19`

**Step 1: Add the switch to the param block**

Replace the param block and the process-kill block:

```powershell
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\JevLauncher",
    [string]$Version = "1.0.0",
    [switch]$SkipShortcut,
    [switch]$SkipRegistry,
    [switch]$NoLaunch,
    [switch]$SkipProcessKill
)
```

and

```powershell
# Cerrar cualquier instancia corriendo.
if (-not $SkipProcessKill) {
    Get-Process JevLauncher.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}
```

**Step 2: Run test to verify it passes**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~InstallerScriptTests"`
Expected: PASS.

**Step 3: Commit**

```bash
git add installer/install.ps1
git commit -m "feat(installer): add -SkipProcessKill to install.ps1"
```

---

### Task 3: Uninstall test (red) + switch (green)

**Files:**
- Modify: `tests/JevLauncher.Tests/InstallerScriptTests.cs`
- Modify: `installer/uninstall.ps1:1-11`

**Step 1: Add the failing test**

Append to `InstallerScriptTests`:

```csharp
    [Fact]
    public void Uninstall_RemovesInstallDir_InSandbox()
    {
        var repo = FindRepoRoot();
        var sandbox = NewSandbox();
        try
        {
            var installed = Path.Combine(sandbox, "installed");
            Directory.CreateDirectory(installed);
            File.WriteAllText(Path.Combine(installed, "JevLauncher.App.exe"), "dummy");
            File.Copy(Path.Combine(repo, "installer", "uninstall.ps1"), Path.Combine(sandbox, "uninstall.ps1"));

            var (code, output) = RunPowerShell(Path.Combine(sandbox, "uninstall.ps1"),
                "-InstallDir", installed, "-SkipProcessKill", "-SkipShortcut", "-SkipRegistry", "-KeepData");

            Assert.True(code == 0, $"uninstall.ps1 salio con {code}: {output}");
            Assert.False(Directory.Exists(installed));
        }
        finally
        {
            Directory.Delete(sandbox, true);
        }
    }
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~Uninstall_RemovesInstallDir"`
Expected: FAIL — unknown parameter `SkipProcessKill`.

**Step 3: Add the switch to `uninstall.ps1`**

```powershell
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\JevLauncher",
    [switch]$KeepData,
    [switch]$SkipShortcut,
    [switch]$SkipRegistry,
    [switch]$SkipProcessKill
)
```

and

```powershell
if (-not $SkipProcessKill) {
    Get-Process JevLauncher.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~InstallerScriptTests"`
Expected: PASS (both tests).

**Step 5: Commit**

```bash
git add tests/JevLauncher.Tests/InstallerScriptTests.cs installer/uninstall.ps1
git commit -m "feat(installer): add -SkipProcessKill to uninstall.ps1 and uninstall test"
```

---

### Task 4: Guard test — install fails without `app` folder

**Files:**
- Modify: `tests/JevLauncher.Tests/InstallerScriptTests.cs`

**Step 1: Add the test**

Append to `InstallerScriptTests`:

```csharp
    [Fact]
    public void Install_Fails_WhenAppFolderMissing()
    {
        var repo = FindRepoRoot();
        var sandbox = NewSandbox();
        try
        {
            var pkg = Path.Combine(sandbox, "pkg");
            Directory.CreateDirectory(pkg);
            File.Copy(Path.Combine(repo, "installer", "install.ps1"), Path.Combine(pkg, "install.ps1"));

            var (code, output) = RunPowerShell(Path.Combine(pkg, "install.ps1"),
                "-InstallDir", Path.Combine(sandbox, "installed"), "-SkipProcessKill", "-SkipShortcut", "-SkipRegistry", "-NoLaunch");

            Assert.NotEqual(0, code);
            Assert.Contains("app", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(sandbox, true);
        }
    }
```

**Step 2: Run test to verify it passes**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~InstallerScriptTests"`
Expected: PASS (three tests).

**Step 3: Commit**

```bash
git add tests/JevLauncher.Tests/InstallerScriptTests.cs
git commit -m "test(installer): install fails when app payload is missing"
```

---

### Task 5: Publish helper + refactor `build.ps1`

**Files:**
- Create: `installer/publish.ps1`
- Modify: `installer/build.ps1`

**Step 1: Create the helper**

`installer/publish.ps1`:

```powershell
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
```

**Step 2: Refactor `build.ps1` to use it**

`installer/build.ps1`:

```powershell
param(
    [string]$Version = "1.0.0",
    [string]$Output = "$PSScriptRoot\dist"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'publish.ps1')

$stage = Join-Path $Output 'JevLauncher'

Remove-Item $Output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'app') | Out-Null

Invoke-JevPublish -Output (Join-Path $stage 'app') -Version $Version

foreach ($file in 'install.ps1', 'uninstall.ps1', 'install.cmd', 'uninstall.cmd', 'README.txt') {
    Copy-Item (Join-Path $PSScriptRoot $file) $stage -Force
}

$zip = Join-Path $Output "JevLauncher-$Version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip

Write-Host "ZIP generado: $zip"
Write-Host "Contenido: app\ + install.cmd + uninstall.cmd + README.txt"
```

**Step 3: Verify the ZIP build**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File installer/build.ps1`
Expected: prints `ZIP generado: ...\installer\dist\JevLauncher-1.0.0.zip` and the ZIP exists.

**Step 4: Commit**

```bash
git add installer/publish.ps1 installer/build.ps1
git commit -m "refactor(installer): share dotnet publish via publish.ps1 helper"
```

---

### Task 6: Refactor `build-inno.ps1` to use the helper

**Files:**
- Modify: `installer/build-inno.ps1`

**Step 1: Replace the script body**

`installer/build-inno.ps1`:

```powershell
param(
    [string]$Version = "1.0.0",
    [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'publish.ps1')

$payload = Join-Path $PSScriptRoot 'inno-payload'

if (-not (Test-Path $Iscc)) {
    throw "No encontre ISCC.exe en '$Iscc'. Instala Inno Setup 6 (winget install JRSoftware.InnoSetup)."
}

Remove-Item $payload -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payload | Out-Null

Invoke-JevPublish -Output $payload -Version $Version

& $Iscc "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot 'inno\JevLauncher.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC fallo con codigo $LASTEXITCODE" }

$setup = Join-Path $PSScriptRoot "dist\JevLauncher-Setup-$Version.exe"
Write-Host "Instalador generado: $setup"
```

**Step 2: Verify the Setup build**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File installer/build-inno.ps1`
Expected: prints `Instalador generado: ...\installer\dist\JevLauncher-Setup-1.0.0.exe` and the file exists.

**Step 3: Commit**

```bash
git add installer/build-inno.ps1
git commit -m "refactor(installer): build-inno uses the publish helper"
```

---

### Task 7: Unified `build-all.ps1` (single publish → identical binaries)

**Files:**
- Create: `installer/build-all.ps1`

**Step 1: Create the script**

`installer/build-all.ps1`:

```powershell
param(
    [string]$Version = "1.0.0",
    [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'publish.ps1')

$dist = Join-Path $PSScriptRoot 'dist'
$stage = Join-Path $PSScriptRoot 'stage'
$pkg = Join-Path $PSScriptRoot 'stage-zip'
$payload = Join-Path $PSScriptRoot 'inno-payload'

Remove-Item $stage, $pkg, $payload, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage, (Join-Path $pkg 'app') | Out-Null

# 1) Publicar una sola vez.
Invoke-JevPublish -Output $stage -Version $Version

# 2) Inno payload = mismos bytes.
New-Item -ItemType Directory -Force -Path $payload | Out-Null
Copy-Item (Join-Path $stage '*') $payload -Recurse -Force

# 3) Paquete del ZIP = app + scripts.
Copy-Item (Join-Path $stage '*') (Join-Path $pkg 'app') -Recurse -Force
foreach ($file in 'install.ps1', 'uninstall.ps1', 'install.cmd', 'uninstall.cmd', 'README.txt') {
    Copy-Item (Join-Path $PSScriptRoot $file) $pkg -Force
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "JevLauncher-$Version.zip"
Compress-Archive -Path (Join-Path $pkg '*') -DestinationPath $zip

# 4) Setup de Inno.
if (-not (Test-Path $Iscc)) {
    throw "No encontre ISCC.exe en '$Iscc'. Instala Inno Setup 6 (winget install JRSoftware.InnoSetup)."
}
& $Iscc "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot 'inno\JevLauncher.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC fallo con codigo $LASTEXITCODE" }
$setup = Join-Path $dist "JevLauncher-Setup-$Version.exe"

# 5) Verificar que ZIP y Setup comparten los mismos binarios.
foreach ($file in 'JevLauncher.App.exe', 'JevLauncher.App.dll', 'JevLauncher.Core.dll') {
    $a = (Get-FileHash (Join-Path $stage $file) -Algorithm SHA256).Hash
    $b = (Get-FileHash (Join-Path $payload $file) -Algorithm SHA256).Hash
    $c = (Get-FileHash (Join-Path (Join-Path $pkg 'app') $file) -Algorithm SHA256).Hash
    if ($a -ne $b -or $a -ne $c) { throw "Los binarios no coinciden para $file" }
}

Write-Host "OK: ZIP y Setup comparten los mismos binarios."
Write-Host "ZIP:   $zip"
Write-Host "Setup: $setup"
```

**Step 2: Verify**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File installer/build-all.ps1`
Expected: prints `OK: ZIP y Setup comparten los mismos binarios.` and both `installer\dist\JevLauncher-1.0.0.zip` and `installer\dist\JevLauncher-Setup-1.0.0.exe` exist.

**Step 3: Commit**

```bash
git add installer/build-all.ps1
git commit -m "feat(installer): build-all.ps1 publishes once for ZIP and Setup"
```

---

### Task 8: Documentation

**Files:**
- Modify: `README.md` (Instalacion section)
- Modify: `installer/README.txt`

**Step 1: Update `README.md`**

In the `### Instalador (Inno Setup)` and `### ZIP con scripts` area, add:

```markdown
Para regenerar **ambos** con los mismos binarios:

```powershell
.\installer\build-all.ps1            # requiere Inno Setup 6
```
```

**Step 2: Update `installer/README.txt`**

Add a short note:

```text
BUILD (desarrolladores)
- installer\build-all.ps1 publica una vez y genera el ZIP y el Setup de Inno.
- installer\build.ps1 y installer\build-inno.ps1 generan cada uno por separado.
```

**Step 3: Commit**

```bash
git add README.md installer/README.txt
git commit -m "docs(installer): document build-all.ps1"
```

---

### Task 9: Final verification

**Step 1: Run the full test suite**

Run: `dotnet test`
Expected: all tests pass, including the three new `InstallerScriptTests`.

**Step 2: Run the unified build**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File installer/build-all.ps1`
Expected: `OK: ZIP y Setup comparten los mismos binarios.`

**Step 3: Confirm clean tree**

Run: `git status --short`
Expected: no uncommitted changes (build outputs are gitignored; if `installer/stage`, `installer/stage-zip` or `installer/inno-payload` show up, add them to `.gitignore` and commit).
