# Jev Launcher — Residencia permanente

El launcher ya queda residente mientras corre; el problema es que **nada lo arranca
con Windows** y que relanzarlo no hacía nada visible.

## Cambios

### A. Arranque con Windows
- Checkbox **"Start with Windows"** en Settings (activado por defecto).
- Escribe/borra `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `JevLauncher = "<exe>"`.
- Se aplica al guardar Settings y al arrancar la app (así el default ON toma efecto solo).

### B. Relanzar muestra el panel
- Dos señales nombradas: `Local\JevLauncher.ShowSignal` y `Local\JevLauncher.ToggleSignal`.
- Primera instancia: espera ambas (`WaitHandle.WaitAny`) y muestra u oculta.
- Segunda instancia: lanzamiento normal → `show`; `--toggle` → `toggle`; luego sale.
- Así `start.cmd` (o el `.exe`) siempre trae el panel en vez de salir en silencio.

### C. Sin cierres accidentales
- Cerrar el panel (Alt+F4 / X) lo **oculta** en vez de destruirlo (`Closing` cancelado).
- La app solo termina desde la bandeja → **Quit** (o `/quit`), que marca `PrepareForExit()`.

## Verificación
- `--smoke` sigue saliendo bien (marca `PrepareForExit` antes de `Shutdown`).
- Prueba real: arrancar la app, lanzarla de nuevo (normal) y ver en el log `ShowPanel`.
- `Autostart.IsEnabled()` verdadero tras activarlo; la clave aparece en `HKCU\...\Run`.
- Tests: el helper de autostart es registry-based (se verifica a mano); el resto son cambios de lifecycle.
