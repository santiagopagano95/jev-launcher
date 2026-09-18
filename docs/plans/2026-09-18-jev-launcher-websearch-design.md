# Jev Launcher — Resultados web inline (Brave)

`/web <consulta>` busca en Brave y muestra los resultados en la lista. Sin key,
cae al comportamiento actual (abre Google). Solo consume cuota cuando se invoca
`/web` explícitamente.

## Comportamiento

- `/web foo` (alias `search`, `s`) → hasta 5 resultados inline.
- Cada fila: título, `dominio · snippet` (mono), Enter abre la URL, `Ctrl+Enter` copia la URL.
- La última fila es siempre la acción "Search the web for foo" (abre el navegador).
- Sin key / error / sin resultados → solo esa fila-acción.
- `/yt`, `/gh`, `/ddg`… siguen abriendo el sitio (no consultan Brave).

## Arquitectura

- Core: `WebResult(Title, Url, Description)`, `IWebSearch`, `BraveWebSearch`
  (GET `api.search.brave.com/res/v1/web/search`, header `X-Subscription-Token`),
  `WebSearchResponse.Parse` (puro), `WebResultCandidates.Build`.
- `CommandScope.WebResults`; `/web` pasa a ese scope (mantiene `UrlTemplate` como fallback).
- `LauncherServices.WebSearch` (`IWebSearch?`).
- Engine: `Update` (sync) devuelve la fila-acción; `UpdateAsync` consulta Brave con
  descarte de respuestas viejas (misma secuencia que Jev) y caché por query (20 entradas).
- Settings/App: **Brave API key** cifrada con DPAPI + campo en Settings y *Test connection*.

## Verificación

- Tests: parser con JSON de ejemplo, mapeo a candidatos, engine con `IWebSearch` falso
  (resultados inline, fallback sin proveedor, descarte de respuestas viejas).
- En vivo contra Brave cuando haya key.

## Fuera de alcance

Reranking con Jev, paginación, imágenes/noticias, otros proveedores (SearXNG queda para después).
