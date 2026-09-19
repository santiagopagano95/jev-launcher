# Jev Launcher — Comandos que abren la app de escritorio

Algunos comandos web tienen una app de escritorio con un esquema de URI propio
(Spotify: `spotify:`). Cuando el esquema está registrado, el comando abre la app
en vez del sitio; si no, cae a la URL web.

## Comportamiento

- `/spotify lofi` → `spotify:search:lofi` → abre la app con la búsqueda hecha.
- `/spotify` (sin argumento) → `spotify:` → abre la app.
- Sin el esquema registrado → URL web (comportamiento actual).
- El resto de los comandos no cambia.

## Arquitectura

- `LauncherCommand` gana `DesktopUri` (plantilla con `{0}`), `DesktopHome` y la
  propiedad derivada `Scheme`.
- `Protocols.IsRegistered(scheme)` en Core (registro `HKCR\<scheme>`), inyectable
  vía `LauncherServices.IsProtocolRegistered` para poder testearlo.
- `CommandCandidates.DesktopSearch` / `DesktopHome` arman las filas `OpenUrl`.
- El engine decide app vs web al resolver el comando.
- `Executor` no cambia: ya abre `OpenUrl` con ShellExecute (un URI `spotify:` incluido).

## Verificación

- Tests: con esquema registrado usa el URI de la app; sin él usa la web; el home
  abre la app.
- Prueba real abriendo la app de Spotify.
