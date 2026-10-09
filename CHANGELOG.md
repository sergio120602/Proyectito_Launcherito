# Registro de cambios

Todos los cambios relevantes de **Launcherito** se documentan en este archivo.

El formato se basa en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [Sin publicar]

## [1.2] - 2026-10-09

### Añadido
- **Pestañas** en la barra superior: **Canciones**, **Artistas** y **Géneros**, cada una con su botón.
- **Clasificación con la API de Deezer** (gratuita, sin clave): se busca cada canción, se toma el género de su álbum y se descarga la foto de cada artista y de cada género (`MusicCatalog`). Si un álbum tiene varios géneros, se elige el más concreto en lugar de «Pop».
- **Artistas**: tarjetas con la foto redonda de cada artista y su número de canciones.
- **Géneros**: tarjetas con la imagen de cada género, ordenadas por número de canciones. Los subgéneros sin imagen en Deezer («Pop latino», «Flamenco») usan la foto del artista con más canciones de ese género.
- Al pulsar un artista o un género se ven sus canciones; al pulsar una, suena. La que está sonando aparece resaltada.
- En Artistas y Géneros el reproductor (barra, tiempos y botones) pasa a una barra inferior con el título y el artista.
- Indicador «Clasificando con Deezer… N/M» en la barra superior y aviso si no hay conexión. Las canciones pendientes se reintentan al añadir más.
- Caché en disco (`%LOCALAPPDATA%\Launcherito\catalogo.json`): cada canción, álbum, género y artista se consulta una sola vez. Se escribe primero en un archivo temporal para no dejarla a medias.

### Cambiado
- Al cargar muchas canciones, entran en tandas de 10 con animación: cada tanda empieza cuando la anterior ha terminado de colocarse, y ninguna aparece de golpe.

## [1.1.3] - 2026-10-09

### Cambiado
- Animaciones de entrada 2,5 veces más lentas, para que se vean bien.
- Se ve cómo se forma el mosaico: las portadas entran una tras otra (hasta 300 ms entre una y la siguiente, y todas empezadas en 4 s), empezando por la canción que suena y siguiendo por las más cercanas a ella.

### Corregido
- Al cargar canciones la ventana se quedaba en negro unos segundos: las primeras portadas en entrar quedaban fuera de la pantalla, porque el mosaico se desplaza enseguida hasta la canción que suena.

## [1.1.2] - 2026-10-09

### Añadido
- El título de la ventana muestra la versión del programa (p. ej. «Launcherito 1.1.2»).
- El ejecutable se deja también en la raíz de la carpeta del proyecto con la versión en el nombre (`Launcherito v1.1.2.exe`).

## [1.1.1] - 2026-10-09

### Cambiado
- Las carátulas del mosaico solo se ven desenfocadas mientras la portada se anima. Una vez en su sitio se ven nítidas (400 px). Cada carátula se lee una vez y de esos mismos datos salen las dos versiones: la nítida y una copia diminuta de 32 px que, estirada, se ve desenfocada durante el movimiento. Sigue sin usarse `BlurEffect`.

## [1.1] - 2026-10-09

### Añadido
- Animaciones de entrada aleatorias para cada canción que entra en el mosaico: gira, crece, choca (las demás se apartan a trompicones), cae botando, derrapa, se da la vuelta como una carta o parpadea (`TileAnimator`).
- Las portadas que cambian de sitio se desplazan en lugar de saltar.

### Mejorado
- Carátulas del mosaico muy desenfocadas: se decodifican a 32 px y se estiran con escalado lineal, sin `BlurEffect`. Gastan mucha menos memoria y CPU (unos 40 MB menos con 250 canciones).
- Lectura de etiquetas ID3 con un buffer de 64 KB (`BufferedStream`): menos lecturas pequeñas al disco al cargar las carátulas del mosaico.
- Al empezar a arrastrar la barra, la portada activa termina su animación para que la barra no se mueva bajo el ratón.

## [1.0] - 2026-10-09

Cambio de versión mayor: nueva interfaz.

### Añadido
- **Vista mosaico** con todas las canciones: portadas de distintos tamaños colocadas sin huecos (`MosaicPanel`). La canción que suena ocupa una portada grande con un borde de color.
- **Barra deslizante y controles dentro de la portada** que está sonando, con título y artista sobre un degradado.
- **Botón de cambio de vista** arriba a la derecha («Vista mosaico» ↔ «Vista original»). Al cargar varias canciones se pasa sola al mosaico.
- Pulsar cualquier portada del mosaico reproduce esa canción. Al pasar el ratón se ve su título.
- Barra superior con «＋ Añadir canciones» y el número de canciones cargadas.

### Mejorado
- Las portadas del mosaico se leen en segundo plano (como mucho 4 a la vez) y solo cuando están cerca de la zona visible. Las que se alejan se liberan, y al volver a la vista original se sueltan todas. Con 250 canciones la memoria se mantiene estable.
- La lectura de etiquetas está en una sola clase (`SongInfo`), compartida por las dos vistas.
- La ventana es más grande por defecto (1000×760) para aprovechar el mosaico.

## [0.3] - 2026-10-09

### Añadido
- Carga de varias canciones a la vez (selección múltiple en «Cargar Archivos» y en *Abrir con*).
- Lista de reproducción sin límite de canciones, ignorando las repetidas. «Añadir canciones» las suma sin cortar la que suena.
- Botones ⏮ y ⏭ para ir a la canción anterior o siguiente. Al llegar al final, la lista vuelve a empezar.
- Botón de modo aleatorio, **activado por defecto** (`Playlist.Shuffle = true`). Desactivado, las canciones pasan en orden alfabético.
- Al acabar una canción empieza automáticamente la siguiente.
- Indicador de posición en la lista (p. ej. `3 / 25`).

### Mejorado
- Barra deslizante: toda la franja (28 px) responde al clic, no solo la línea de 5 px.
- Al pulsar en cualquier punto de la barra salta ahí y se puede seguir arrastrando sin soltar.
- La canción cambia de posición una sola vez, al soltar, y la barra ya no rebota a la posición antigua.
- El tiempo actual solo se reescribe cuando cambia el segundo mostrado.

### Cambiado
- Si una canción no se puede reproducir, se quita de la lista y se pasa a la siguiente.

### Corregido
- El clic en la pista de la barra no siempre se detectaba como inicio de arrastre.
- Si se perdía el ratón a mitad de arrastre (p. ej. Alt+Tab), la barra se quedaba congelada.
- Las flechas del teclado movían la barra sin cambiar la posición de la canción.

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
