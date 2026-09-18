# Jev Launcher — Comandos `/` (sin API)

Comandos con barra para acotar la búsqueda o disparar una acción web. Sin API de
búsqueda: los comandos web abren el navegador con la URL del buscador elegido.

## Concepto

- Query que arranca con `/` → la primera palabra es el comando.
- `/` solo → **menú de comandos** como filas. Enter en un comando **inserta**
  `/nombre ` en el input (no ejecuta).
- `/cmd argumento` ejecuta la acción.
- Nombre desconocido → menú con los comandos más parecidos (fuzzy), recuperable.

## Comandos

| Comando | Alias | Acción |
|---|---|---|
| `/web` | `g` | Google (`SearchTemplate` de Settings) |
| `/ddg` | | DuckDuckGo |
| `/bing` | | Bing |
| `/yt` | | YouTube |
| `/gh` | | GitHub |
| `/wiki` | `w` | Wikipedia |
| `/so` | `stack` | Stack Overflow |
| `/maps` | `m` | Google Maps |
| `/img` | `i` | Google Images |
| `/file` | | Solo archivos |
| `/app` | | Solo apps |
| `/toggle` | `toggles` | Solo toggles del sistema |
| `/recent` | | Archivos más recientes (filtrables por texto) |
| `/calc` | | Solo calculadora |
| `/settings` | | Abre Settings |
| `/quit` | | Cierra el launcher |
| `/help` | `?` | Muestra el menú de comandos |

## Comportamiento

- `/yt lofi` → fila "Search YouTube for lofi" con `Target` = URL completa; Enter abre el navegador.
- `/file presupuesto` → solo candidatos `OpenFile`.
- `/recent` → los archivos más recientes; `/recent pdf` → los filtra.
- `/` o `/to` → menú de comandos; Enter inserta.
- Las queries de comando **no llaman a Jev** (acción determinista, respuesta inmediata).

## Arquitectura

- `Commands.cs` (Core): registro `LauncherCommand` (Id, Title, Description, Aliases,
  Scope, UrlTemplate) + `CommandParser` (split nombre/argumento, resolve por id o alias).
- `CandidateKind` nuevos: `Command` (fila de paleta; `Target` = texto a insertar) y
  `OpenUrl` (fila de acción; `Target` = URL completa).
- `CommandCandidates` (Core): arma filas de paleta y filas `OpenUrl`.
- `Prefilter.BuildCandidates(..., CandidateKind? only)`: filtro de alcance.
- `LauncherEngine`: detecta comandos y resuelve sin Jev.
- `Executor`: abre `OpenUrl`. `JevQuestions.KindName`: `open_url`, `command` (paridad futura).
- App: Enter en `Command` inserta el texto y mantiene el panel abierto; `AppAction`
  (`settings`/`quit`/`help`) se maneja en la app.

## Verificación

- Tests Core: parseo/resolución, paleta, alcance por tipo, armado de URL.
- Test de pipeline: `/yt lofi` top `OpenUrl`; `/file` solo archivos; `/` paleta.
- Smoke: render del menú `/` a PNG.

## Fuera de alcance

Resultados web reales inline (API), historial de portapapeles, snippets,
acciones secundarias con `Ctrl+Enter`.
