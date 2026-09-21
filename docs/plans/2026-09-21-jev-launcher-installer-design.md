# Jev Launcher - Empaquetado e instalacion

Empaquetado por script, sin dependencias externas: publica la app y arma un ZIP con
un instalador y un desinstalador. Todo queda en `HKCU` y `%LOCALAPPDATA%` (sin admin).

## Que hace el instalador

- Publica la app (framework-dependent, win-x64; requiere .NET 10).
- Copia los binarios a `%LOCALAPPDATA%\Programs\JevLauncher`.
- Crea el acceso directo "Jev Launcher" en el Menu Inicio.
- Registra la entrada en "Aplicaciones instaladas"
  (`HKCU\...\Uninstall\JevLauncher`) con desinstalador.
- Cierra cualquier instancia corriendo antes de copiar.
- Lanza la app al terminar (se puede omitir).

## Que hace el desinstalador

- Cierra la app.
- Borra la entrada de arranque con Windows (`HKCU\...\Run\JevLauncher`).
- Borra el acceso del Menu Inicio y la entrada de desinstalacion.
- Borra la carpeta de instalacion.
- Borra los datos (`%APPDATA%\JevLauncher`) salvo que se pase `-KeepData`.

## Archivos

- `installer/build.ps1` - publica y arma `installer/dist/JevLauncher-<version>.zip`.
- `installer/install.ps1` / `install.cmd` - instalar.
- `installer/uninstall.ps1` / `uninstall.cmd` - desinstalar.
- `installer/README.txt` - instrucciones para el usuario.

Los scripts aceptan `-InstallDir`, `-SkipShortcut`, `-SkipRegistry` y `-NoLaunch`
para poder probarse en un sandbox sin tocar el sistema.

## Verificacion

- `build.ps1` genera el ZIP (dentro del proyecto).
- Prueba en sandbox: `install.ps1 -InstallDir <proyecto>\installer\test -SkipShortcut -SkipRegistry -NoLaunch`
  y correr el `.exe` resultante.
- `uninstall.ps1` con el mismo `-InstallDir` limpia el sandbox.
