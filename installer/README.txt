Jev Launcher
============

Un launcher tipo Spotlight para Windows (Alt+Space) que aprende de tu uso y usa Jev
(TypeSafe) para re-rankear resultados.

REQUISITOS
- Windows 10/11 x64
- .NET 10 Runtime (si no lo tenes, instalalo desde https://dotnet.microsoft.com/download)

INSTALAR
- Doble clic en install.cmd  (o:  powershell -ExecutionPolicy Bypass -File install.ps1)
- Instala en %LOCALAPPDATA%\Programs\JevLauncher, crea el acceso en el Menu Inicio
  y registra la desinstalacion en "Aplicaciones instaladas".
- No requiere permisos de administrador.

USAR
- Alt+Space abre/cierra el panel.
- Escribi para buscar apps, archivos, toggles o calculos.
- Ctrl+K abre el menu de acciones de la fila (copiar ruta/URL, abrir carpeta, etc.).
- Ctrl+Enter copia la ruta o URL de la fila.
- Comandos: /web /yt /gh /spotify /file /find /clip /snip /note /timer /stats ...
- Escribi "/" para ver todos los comandos.

CONFIGURAR LA IA (opcional)
- Icono de bandeja -> Settings -> pega tu TYPESAFE_API_KEY (se guarda cifrada con DPAPI).
- Sin key, el launcher funciona igual en modo local (fuzzy + calculadora + toggles).

DESINSTALAR
- Doble clic en uninstall.cmd, o desde "Aplicaciones instaladas" en Windows.
- Por defecto borra tambien los datos (%APPDATA%\JevLauncher). Usa -KeepData para conservarlos.
