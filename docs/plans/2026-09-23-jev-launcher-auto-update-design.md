# Jev Launcher - Actualizacion automatica desde GitHub Releases

El launcher chequea GitHub Releases, avisa si hay una version nueva y, si el usuario
acepta, descarga el Setup, verifica su checksum y lo aplica en silencio. Los releases
se publican con GitHub Actions al pushear un tag `v*`.

## Decisiones

- UX: **avisar y preguntar** (nada silencioso sin consentimiento).
- Aplicar: descargar `JevLauncher-Setup-<v>.exe` y ejecutarlo con
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
- Releases: workflow de GitHub Actions en tag `v*`.
- Verificacion: **SHA256** del Setup contra un asset `.sha256` del release.
- Frecuencia: al iniciar (~10 s de delay) + cada 6 h + manual desde la bandeja.

## Arquitectura

### `JevLauncher.Core` (sin UI, testeable)

- `ReleaseInfo`: `Tag`, `Version` (System.Version), `SetupUrl`, `ChecksumUrl`,
  `Notes`, `HtmlUrl`.
- `UpdateChecker`:
  - `CheckAsync(HttpClient, Version current, CancellationToken)`.
  - GET `https://api.github.com/repos/santiagopagano95/jev-launcher/releases/latest`
    con `User-Agent` y `Accept: application/vnd.github+json`.
  - Parsea `tag_name` (`v1.1.0` -> `1.1.0`), elige el asset `JevLauncher-Setup-*.exe`
    y su `.sha256` hermano, y compara con la version actual.
  - El parseo (JSON -> `ReleaseInfo`) es una funcion pura para poder testearlo.
- `UpdateDownloader`:
  - `DownloadAsync(ReleaseInfo, string destDir, CancellationToken)`.
  - Baja el Setup y el `.sha256` a `%LOCALAPPDATA%\JevLauncher\updates\`.
  - Calcula SHA256 del Setup y lo compara; si no coincide, no devuelve ruta ejecutable.

### `JevLauncher.App` (UI / orquestacion)

- Al iniciar: delay ~10 s, chequeo en background, timer de 6 h.
- Si hay version nueva: dialogo con **Actualizar ahora / Mas tarde / Ver cambios**.
- Si acepta: descarga con progreso, verifica checksum, lanza un helper detached
  `cmd /c "<setup>" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART && start "" "<app>"`,
  y cierra la app para que el instalador reemplace archivos y la reabra al terminar.
- Item **"Buscar actualizaciones"** en el menu de bandeja: chequeo manual; muestra
  "Ya estas al dia" o el dialogo de update.

## Version actual

- Se lee de `Assembly.GetEntryAssembly().GetName().Version` (stamped por `-p:Version`;
  `build-all.ps1` ya pasa `-Version`).
- Comparacion con `System.Version` (3 componentes). `releases/latest` excluye
  pre-releases.

## CI de releases (`.github/workflows/release.yml`)

- Trigger: push de tag `v*`.
- `windows-latest`: checkout, `setup-dotnet` 10, instalar Inno Setup 6 (choco),
  correr `installer/build-all.ps1 -Version <tag sin v>`.
- Calcular `SHA256` del Setup y publicar el release con
  `JevLauncher-Setup-<v>.exe`, `JevLauncher-Setup-<v>.exe.sha256` y el ZIP.

## Ajustes

- `Settings.CheckForUpdates` (bool, default `true`) para desactivar el chequeo
  automatico. El manual sigue disponible.

## Testing

- Unit tests (xUnit) con un `HttpMessageHandler` falso (patron de `JevClientTests`):
  parseo del JSON de release, seleccion de assets, comparacion de versiones
  (nueva/igual/vieja), checksum valido/invalido, y que un checksum malo no devuelva
  ruta ejecutable.

## Verificacion

- `dotnet test` en verde con los tests nuevos.
- Simular un release local (JSON de ejemplo) para validar la deteccion.
- El workflow se valida publicando un tag de prueba cuando se corte el primer release.
