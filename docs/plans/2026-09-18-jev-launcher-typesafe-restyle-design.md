# Jev Launcher — Restyle "TypeSafe"

Adaptar la piel del panel del launcher a la estética de [typesafe.ai](https://typesafe.ai/).
Sin cambios en la lógica (índice, fuzzy, Jev, ranking, executor): es solo UI.

## Tokens (tomados del sitio)

| Uso | Valor |
|---|---|
| Fondo del panel | `#1E1E1E` |
| Texto principal | `#FEFEFE` |
| Texto secundario | `#DEDEDE` |
| Texto atenuado / hints | `#C4C4C4` / `#ABBAB9` |
| Acento cálido (degrade) | `#F386A1` → `#D45BB6` |
| Verde "ready" | `#03AA5C` |
| Teal | `#09AEA1` |
| Bordes | `rgba(255,255,255,0.06–0.10)` |

## Tipografías

- **Inter** → UI (query, títulos de fila).
- **JetBrains Mono** → detalles, etiquetas de tipo y footer (la firma visual del sitio).
- Ambas SIL OFL: se empaquetan como recursos de la app.
- Fallback si no se pueden bajar: `Segoe UI Variable` + `Cascadia Mono`.

## Componentes

- **Panel**: 640px, corner 14, borde 1px sutil, fondo `#1E1E1E` con alpha (conserva el blur Acrylic).
- **Input**: Inter ~20px, caret blanco; **línea de acento** degrade rosa→magenta de 2px debajo,
  resaltada mientras hay un request a Jev en vuelo.
- **Filas** (~44px, máx. 7): glifo, título (Inter) y detalle (JetBrains Mono atenuado).
  Fila seleccionada: fondo `rgba(255,255,255,0.05)` + barra izquierda de 2px con el degrade.
- **Etiqueta de tipo**: mono, minúscula y espaciada (`open_app`, `system_toggle`, …).
- **Badge `↵`**: verde `#03AA5C` cuando la fila top está *ready*.
- **Footer**: mono, `ms · $costo`; tooltip con p50/p95/decisiones/tokens.
- **Settings**: mismos tokens y tipografías; botón Save con el degrade.

## Verificación

- `--smoke` renderiza el panel a un PNG (`RenderTargetBitmap`) y lo guarda; se revisa la imagen
  como evidencia visual.
- El smoke existente debe seguir reportando: listado con `Items=7`, calculator top, tray `IsCreated=True`,
  y sin crash log.

## Fuera de alcance

Ranking, Jev, índice, executor, hotkey y bandeja se mantienen igual.
