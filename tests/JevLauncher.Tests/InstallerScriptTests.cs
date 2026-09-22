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
        var psi = NewStartInfo();
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        return Run(psi);
    }

    private static (int ExitCode, string Output) RunPowerShellCommand(string command)
    {
        var psi = NewStartInfo();
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(command);
        return Run(psi);
    }

    private static ProcessStartInfo NewStartInfo() => new()
    {
        FileName = "powershell.exe",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };

    private static (int ExitCode, string Output) Run(ProcessStartInfo psi)
    {
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw new TimeoutException("powershell.exe no termino en 120 segundos.");
        }
        var output = stdout.GetAwaiter().GetResult() + Environment.NewLine + stderr.GetAwaiter().GetResult();
        return (process.ExitCode, output);
    }

    private static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData("install.ps1")]
    [InlineData("uninstall.ps1")]
    public void Scripts_DeclareSkipProcessKill(string script)
    {
        var repo = FindRepoRoot();
        var path = Path.Combine(repo, "installer", script).Replace("'", "''");
        var command =
            "$errors=$null;$tokens=$null;" +
            $"$ast=[System.Management.Automation.Language.Parser]::ParseFile('{path}',[ref]$tokens,[ref]$errors);" +
            "($ast.ParamBlock.Parameters | ForEach-Object { \"$($_.Name.VariablePath.UserPath)=$($_.StaticType.Name)\" }) -join ','";
        var (code, output) = RunPowerShellCommand(command);
        Assert.True(code == 0, $"PowerShell salio con {code}: {output}");
        Assert.Contains("SkipProcessKill=SwitchParameter", output, StringComparison.Ordinal);
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
            TryDelete(sandbox);
        }
    }

    [Fact]
    public void Install_SkipProcessKill_LeavesRunningAppAlive()
    {
        var repo = FindRepoRoot();
        var sandbox = NewSandbox();
        Process? sentinel = null;
        try
        {
            var sentinelExe = Path.Combine(sandbox, "JevLauncher.App.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), sentinelExe);
            sentinel = Process.Start(new ProcessStartInfo(sentinelExe, "-n 60 127.0.0.1")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            Assert.NotNull(sentinel);
            Thread.Sleep(700);

            var pkg = Path.Combine(sandbox, "pkg");
            Directory.CreateDirectory(Path.Combine(pkg, "app"));
            File.WriteAllText(Path.Combine(pkg, "app", "JevLauncher.App.exe"), "dummy");
            File.Copy(Path.Combine(repo, "installer", "install.ps1"), Path.Combine(pkg, "install.ps1"));
            File.Copy(Path.Combine(repo, "installer", "uninstall.ps1"), Path.Combine(pkg, "uninstall.ps1"));

            var (code, output) = RunPowerShell(Path.Combine(pkg, "install.ps1"),
                "-InstallDir", Path.Combine(sandbox, "installed"), "-SkipProcessKill", "-SkipShortcut", "-SkipRegistry", "-NoLaunch");

            Assert.True(code == 0, $"install.ps1 salio con {code}: {output}");
            sentinel.Refresh();
            Assert.False(sentinel.HasExited, "install.ps1 con -SkipProcessKill mato una app JevLauncher.App en ejecucion");
        }
        finally
        {
            try { if (sentinel is { HasExited: false }) { sentinel.Kill(); sentinel.WaitForExit(5000); } } catch { /* best effort */ }
            TryDelete(sandbox);
        }
    }

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
            TryDelete(sandbox);
        }
    }

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
            Assert.Contains("carpeta 'app'", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(sandbox);
        }
    }
}
