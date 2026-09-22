# Jev Launcher - Test del instalador y build unificado

Dos mejoras sobre el empaquetado existente (ver `2026-09-21-jev-launcher-installer-design.md`):

1. Un **test automatizado** del instalador script-based que corre con `dotnet test`.
2. Un **build unificado** que publica una sola vez para que el ZIP y el Setup de Inno
   queden con binarios identicos.

## 1. Cambios en los scripts

- `install.ps1`: nuevo switch `-SkipProcessKill`. Envuelve el cierre de instancias de
  `JevLauncher.App`. Sin el switch, el comportamiento actual no cambia.
- `uninstall.ps1`: mismo switch `-SkipProcessKill`.
- El resto de los parametros (`-InstallDir`, `-SkipShortcut`, `-SkipRegistry`, `-NoLaunch`,
  `-KeepData`) se mantienen.

## 2. Test automatizado (xUnit)

Archivo: `tests/JevLauncher.Tests/InstallerScriptTests.cs`.

- Localiza la raiz del repo subiendo desde `AppContext.BaseDirectory` hasta encontrar
  `installer/install.ps1`.
- Helper que ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File <script> <args>`
  y devuelve exit code + salida combinada.
- Cada test usa una carpeta sandbox unica en `%TEMP%` y limpia en `finally`.
- Casos:
  1. **Install**: sandbox con `pkg\app\JevLauncher.App.exe` (archivo dummy) y copia de
     `install.ps1` / `uninstall.ps1`; corre
     `install.ps1 -InstallDir <sandbox>\installed -SkipProcessKill -SkipShortcut -SkipRegistry -NoLaunch`;
     verifica exit 0 y que existan `JevLauncher.App.exe` y `uninstall.ps1` en el destino.
  2. **Uninstall**: corre
     `uninstall.ps1 -InstallDir <sandbox>\installed -SkipProcessKill -SkipShortcut -SkipRegistry -KeepData`;
     verifica que la carpeta de instalacion se borro.
  3. **Guard**: sin carpeta `app`, `install.ps1` falla (exit distinto de 0).

El test es hermetico: no requiere admin, red ni publish previo, y no toca registro ni
accesos directos. La funcionalidad de la app ya la cubre el smoke existente.

## 3. Build unificado

- `installer/publish.ps1` (helper, se dot-sourcea): funcion
  `Invoke-JevPublish -Output <dir> -Version <v>` con el `dotnet publish` comun.
- `installer/build.ps1` y `installer/build-inno.ps1`: delegan en el helper (compatibilidad
  con los comandos actuales).
- `installer/build-all.ps1`: publica **una sola vez** a `installer\stage`; desde ahi copia a
  `inno-payload` y arma el paquete del ZIP; genera `dist\JevLauncher-<version>.zip` y
  `dist\JevLauncher-Setup-<version>.exe` con ISCC; imprime una verificacion de hashes entre
  los binarios del ZIP y `inno-payload`.

Resultado: ZIP y Setup comparten exactamente los mismos binarios.

## 4. Documentacion

- `README.md` y `installer/README.txt`: mencionar `build-all.ps1` como la via canonica de
  release.

## Verificacion

- `dotnet test` en verde, incluyendo los tres tests nuevos.
- `.\installer\build-all.ps1` genera ambos artefactos y los hashes ZIP vs `inno-payload`
  coinciden.
