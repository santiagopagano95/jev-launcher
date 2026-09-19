# Jev Launcher — Apps de la Store en el índice

Suma al índice las apps que no tienen `.lnk` en el Menú Inicio (Store/UWP y algunas
clásicas) enumerando `shell:AppsFolder` vía COM.

## Fuente y formatos

`shell:AppsFolder` devuelve pares `Name` / `Path`, y el `Path` viene en dos formatos:

- **Ruta absoluta** (`C:\...\app.exe`) → se ejecuta esa ruta.
- **AppID** (`SpotifyAB.SpotifyMusic_…!Spotify`, `Chrome._crx_…`, `Brave`) →
  se lanza con `shell:AppsFolder\<AppID>` (verificado: arranca la app).

## Filtro de ruido

Se descartan:
- desinstaladores: nombre con `uninstall`/`desinstalar`, o ruta con `\unins`;
- documentación/links: ruta `.html/.htm/.txt/.url/.pdf` o que empieza con `http`,
  o nombre con `documentation`/`release notes`/`installation notes`/`user manual`;
- **duplicados** por título contra las apps `.lnk` ya indexadas (LOGI, Mensi, etc.).

## Arquitectura

- `StoreApps` (Core): `Enumerate()` (COM `Shell.Application`, late binding),
  `IsNoise(name, path)`, `ToCandidate(entry)`, `ToCandidates(entries, existingTitles)`.
  Las tres últimas puras y testeables.
- `LocalIndex.Build()` agrega las apps de la Store **después** de las `.lnk`, deduplicando
  por título. El build corre en el hilo de fondo, así que no bloquea la UI.
- El lanzamiento usa el `Executor` actual (Target = ruta o `shell:AppsFolder\…`).

## Verificación

- Tests: filtro de ruido (uninstall/docs/links), target para AppID vs ruta absoluta, dedupe.
- Smoke: cantidad de apps de la Store sumadas y `/app notepad` resolviendo.
- Manual: abrir una app de la Store.
