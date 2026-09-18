# Jev Launcher para Windows — Design

Port con paridad de funcionalidad del [Jev Launcher de macOS](https://github.com/dabit3/jev-experiments/tree/main/jev-launcher)
a Windows. Un launcher tipo Spotlight que lee intención, no strings: en cada
keystroke manda la query, contexto local y los mejores candidatos a Jev
(TypeSafe), recibe tres juicios tipados y re-rankea en vivo.

## Stack

- **.NET 10 + WPF** (Windows). El runtime 10.0.11 ya está instalado; falta el SDK
  (`winget install Microsoft.DotNet.SDK.10`).
- Sin dependencias de UI en el núcleo lógico.
- HttpClient + System.Text.Json para Jev. P/Invoke para APIs de Windows.

## Estructura de proyectos

```
launcher/
  JevLauncher.sln
  src/
    JevLauncher.Core/      (net10.0-windows, sin UI, testeable)
    JevLauncher.App/       (net10.0-windows, WPF)
  tests/JevLauncher.Tests/ (xUnit)
  run.ps1
```

`Core`: `LocalIndex`, `Fuzzy`, `Calculator`, `Ranker`, `JevClient`, `Executor`,
`Models`, `Stats`.
`App`: ventana del panel, hotkey global, bandeja, wiring, settings.

## Índice local (`LocalIndex`)

- **Apps**: accesos `.lnk` del Menú Inicio (máquina + usuario) más apps de la
  Store vía enumeración de `shell:AppsFolder` (COM `Shell.Application`). Se
  evita escanear `.exe`, que es frágil en Windows.
- **Archivos**: `Downloads`, `Desktop`, `Documents` (nivel 0 + un anidado, tope
  400 por carpeta, con edad de modificación en el subtítulo).
- **Toggles**: 7 entradas fijas siempre presentes (modo oscuro, Wi-Fi, suspender,
  bloquear, vaciar papelera, archivos ocultos, silenciar audio).
- Se reconstruye en background cada vez que se muestra el panel; al terminar,
  se re-rankea.

## Fuzzy (`Fuzzy`)

Port 1:1 del scorer de macOS: exact / prefix / word-initial / subsequence sobre
título y keywords, con stopwords eliminadas.

## Calculadora (`Calculator`)

Parser de descenso recursivo portado: `+ - * / ^ ( )`, `x` como multiplicación,
`sqrt`, porcentajes (`15% of 240`, `200 * 10%`), prefijo opcional `calc`/`=`.
Sin `NSExpression`/eval. El resultado se copia al portapapeles al ejecutar.

## Integración Jev (`JevClient` + `Ranker`)

- `POST https://api.typesafe.ai/v1/systemone`, `model: jev-latest`.
- Auth: bearer desde `TYPESAFE_API_KEY` (env) o settings.
- **State**: `query`, `query_note`, `context` (frontmost_app vía
  `GetForegroundWindow` + nombre de proceso, recent_apps, clipboard_kind vía
  Win32, time_of_day, weekday) y `candidates` (top 13 fuzzy + resultado
  aritmético + web search, tope 15, ids `c0…c14`).
- **Preguntas** en una sola llamada, con la misma redacción que el original:
  - `target` — Choice sobre `c0…cN` + `none`.
  - `action` — Choice sobre `open_app`, `open_file`, `web_search`, `calculate`,
    `system_toggle`, `run_shortcut`, `unclear`.
  - `ready` — Noul.
- **Ranking**: `score = 0.65·P(target) + 0.20·P(action matches kind) + 0.15·fuzzy`.
  Sin respuesta (fallo, sin key, o todavía nada) → orden fuzzy puro; nunca se
  espera la red.
- **Stale**: número de secuencia por keystroke; solo se aplica la respuesta más
  nueva. Mientras hay un request en vuelo, se mantiene el juicio anterior
  atenuado para no flickear.
- Badge verde ↵ si `ready ≥ 0.6` o `P(target) ≥ 0.9`.

## Ejecutor (`Executor`)

- `open_app` / `open_file` / `web_search`: `Process.Start` con ShellExecute.
- `calculate`: copia el resultado al portapapeles.
- **Toggles**:

  | Toggle | Implementación |
  |---|---|
  | Modo oscuro/claro | registro `AppsUseLightTheme` + `WM_SETTINGCHANGE` |
  | Wi-Fi on/off | `netsh interface set interface` (puede requerir admin) |
  | Suspender | `SetSuspendState` (powrprof) |
  | Bloquear | `LockWorkStation` |
  | Vaciar papelera | `SHEmptyRecycleBin` |
  | Archivos ocultos | registro `Hidden` + `SHChangeNotify` |
  | Silenciar audio | CoreAudio `IAudioEndpointVolume` |

- `run_shortcut` se mantiene como kind para paridad, sin fuente propia (los
  `.lnk` ya entran como apps). No hay equivalente a `shortcuts list`.

## UI, hotkey y bandeja (`App`, WPF)

- Ventana sin bordes, `Topmost`, sin ícono en taskbar, blur Acrylic/Mica vía
  `DwmSetWindowAttribute` (`DWMWA_SYSTEMBACKDROP_TYPE`,
  `DWMWA_WINDOW_CORNER_PREFERENCE`). `AllowsTransparency=false` para no romper
  el blur.
- **Alt+Space** global vía `RegisterHotKey`; si ya está tomado, avisa y permite
  elegir otro en Settings.
- Se oculta al perder foco, con debounce corto para no cerrarse al clickear una
  fila.
- Layout espejo escalado por DPI: header de input, filas de ~44px hasta 7, footer
  de ~40px. Íconos reales vía `SHGetFileInfo`; glifos de Segoe Fluent Icons para
  toggles/calc/web.
- Footer: último round-trip en ms y costo acumulado; hover muestra p50/p95,
  cantidad de decisiones y tokens.
- Punto indicador de request en vuelo; rayo verde cuando la fila top está ready.
- Bandeja con `NotifyIcon`: Toggle Launcher, Settings…, Quit.
- Estado vacío con chips de ejemplo clickeables y, sin key,
  `TYPESAFE_API_KEY is not set — local matching only`.

## Settings y errores

- Settings en `%APPDATA%\JevLauncher\settings.json`; API key cifrada con DPAPI.
- Sin key → modo fuzzy con mensaje claro.
- Fallo de red/timeout → orden fuzzy, sin romper el panel.
- Fallo de toggle por permisos (Wi-Fi) → aviso en el footer del panel.

## Testing y build

- `tests/JevLauncher.Tests` (xUnit): calculadora, fuzzy, prefilter, ranker,
  construcción de request y parseo de respuesta, stats de latencia/costo,
  recency, parseo de interfaces Wi-Fi. Ninguno toca la red.
- Harness de integración tipo `curl` (CLI) para probar contra Jev real.
- `run.ps1` setea `TYPESAFE_API_KEY` y corre `dotnet run`.

## Límites explícitos (YAGNI)

- Sin plugins, sin búsqueda semántica de archivos, sin leer títulos de ventana,
  tabs o contenido del portapapeles (solo el *tipo*).
- Sin equivalente a `shortcuts list` de macOS.
