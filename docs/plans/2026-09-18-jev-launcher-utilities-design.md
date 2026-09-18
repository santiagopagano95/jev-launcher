# Jev Launcher — Redes, servicios y utilidades

Amplía los comandos `/` con redes sociales, servicios de streaming, y un paquete de
utilidades locales (snippets, historial de portapapeles, texto, ventanas, notas, timer,
acciones secundarias). Sin APIs externas.

## Comandos web nuevos

`/x` (`tw`), `/reddit` (`r`), `/ig`, `/tiktok` (`tt`), `/li`, `/fb`, `/bsky`, `/hn`,
`/spotify`, `/netflix`, `/ytmusic`, `/twitch`, `/prime`, `/disney`.

- **Con argumento** → búsqueda (`/netflix dark` → netflix.com/search?q=dark).
- **Sin argumento** → abre el sitio (`HomeUrl`); si el comando no tiene `HomeUrl`, muestra su fila de paleta.

## Utilidades

| Comando | Comportamiento | Persistencia |
|---|---|---|
| `/snip [filtro]` | Lista/filtra snippets; Enter copia | `settings.json` (`Snippets: [{name,text}]`) |
| `/clip [filtro]` | Historial de los últimos 50 textos; Enter re-copia | En memoria (sesión) |
| `/uuid [n]` | Genera n UUIDs y los copia | — |
| `/b64 <texto>` / `/b64d <texto>` | Base64 ida/vuelta | — |
| `/hash <texto>` | SHA-256 en hex | — |
| `/color <hex>` | Normaliza (`#f386a1` → `#F386A1`) y copia | — |
| `/win [filtro]` | Ventanas abiertas; Enter enfoca (restaura si minimizada) | — |
| `/note [texto]` | Sin texto: lista las últimas (Enter copia). Con texto: guarda | `%APPDATA%\JevLauncher\notes.txt` |
| `/timer 5m` | Temporizador; al terminar notifica en la bandeja | — |

**`Ctrl+Enter`** sobre cualquier fila copia la ruta (archivos/apps) o la URL (web), sin ejecutar.
El footer lo recuerda.

## Arquitectura

- `CandidateKind` nuevos: `Copy`, `FocusWindow`, `SaveNote`, `Timer`.
- `LauncherCommand` gana `HomeUrl`. `CommandScope` nuevos: `Snippets`, `Clipboard`,
  `Utility`, `Windows`, `Notes`, `Timer`.
- Core: `UtilityCommands` (puro), `NotesStore`, `ClipboardHistory`, `WindowList`
  (`EnumWindows`/`SetForegroundWindow`), `Snippet`, `LauncherServices`.
- `LauncherEngine(…, LauncherServices? services = null)`: resuelve los scopes nuevos sin Jev.
- App: escucha el portapapeles (`AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE`),
  maneja `Timer` (DispatcherTimer + notificación), `SaveNote` y `Ctrl+Enter`.
- Settings: editor de snippets (`nombre = texto`, una por línea).

## Verificación

- Tests Core: utilidades (uuid, base64 ida/vuelta, hash, color), snippets, historial,
  notas, `HomeUrl`, URLs de redes/servicios con y sin argumento, y resolución en el engine.
- Smoke: render del menú y de una utilidad a PNG; clipboard/ventanas/timer a mano.

## Fuera de alcance

Resultados web inline con API, historial persistente con imágenes, snippets con
variables/plantillas, UI rica para editar snippets.
