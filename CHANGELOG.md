# Registro de cambios

Todos los cambios relevantes de **Launcherito** se documentan en este archivo.

El formato se basa en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [Sin publicar]

## [0.2.1] - 2026-10-09

### Añadido
- Se puede abrir un `.mp3` pasado como argumento (*Abrir con → Launcherito*).

### Mejorado
- Unos 90 MB menos de RAM: el `.exe` ya no se comprime, así que no descomprime .NET en memoria al arrancar.
- El archivo `.mp3` se lee con un bloque `using` (equivalente al *try-with-resources* de Java) que lo cierra antes de procesar la carátula, y sin analizar todo el audio (`ReadStyle.None`).
- Las carátulas muy grandes se reducen a 640 px al cargarlas para no ocupar decenas de MB.
- La barra de progreso deja de actualizarse mientras la ventana está minimizada.
- Al cerrar la ventana se paran el temporizador y el reproductor y se sueltan sus eventos.

### Corregido
- Si un archivo no se podía reproducir, el temporizador seguía funcionando y el archivo quedaba abierto; ahora se libera y se vuelve a la pantalla inicial.

## [0.2] - 2026-10-09

### Añadido
- Primera aplicación de escritorio (C# / WPF, .NET 10), publicada como un único `Launcherito.exe`.
- Pantalla inicial con fondo negro y un botón grande «Cargar Archivo».
- Carga de canciones en formato `.mp3` (solo se admite este formato).
- Muestra la carátula, el título y el artista de la canción leyendo sus etiquetas ID3.
- Reproductor básico: botón de reproducir/pausar, barra deslizante para avanzar o retroceder y tiempo actual/total.
- Opción «Cargar otro archivo» para cambiar de canción.

## [0.1] - 2026-10-09

### Añadido
- Creación del proyecto y del repositorio `Proyectito_Launcherito`.
- `README.md` con la descripción del proyecto, características previstas y hoja de ruta.
- Sistema de control de versiones: archivo `VERSION`, este `CHANGELOG.md` y etiquetas de Git.
- `.gitignore` inicial.
