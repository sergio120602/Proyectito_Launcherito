# Registro de cambios

Todos los cambios relevantes de **Launcherito** se documentan en este archivo.

El formato se basa en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [Sin publicar]

## [1.6] - 2026-10-10

### Añadido
- **Las canciones se guardan**: cada `.mp3` que se carga (también con *Abrir con*) y cada canción de una lista de Spotify se apunta sola en `%LOCALAPPDATA%\Launcherito\canciones.json`, con su título y artista (`SongLibrary`). De las de Spotify se guardan todos sus datos, así vuelven a salir sin preguntar a Spotify.
- **Pantalla inicial con tres botones**: «Cargar Archivos», **«Tus canciones»** (carga todas las guardadas; muestra cuántas hay) y **«Borrar canciones»**. Los dos nuevos están desactivados mientras no haya nada guardado. Debajo, un aviso de que las canciones se guardan solas.
- **Borrar canciones**: panel con una casilla por canción (título, artista y si es `.mp3`, de Spotify o ya no se encuentra), «Seleccionar todas» y un botón rojo «Borrar N canciones» que pide confirmación. Solo se quitan de Launcherito: los archivos `.mp3` del PC no se borran. Si se borra la que suena, pasa a la siguiente; si no queda ninguna, se vuelve a la pantalla inicial.
- En la barra superior, «Borrar» y «♫ +N» (cargar las guardadas que faltan), que solo aparece si hay guardadas sin cargar.
- Si un `.mp3` guardado ya no está en su sitio, «Tus canciones» lo avisa y en el panel de borrar aparece marcado como «no se encuentra».

### Cambiado
- Las canciones de Spotify que ya tienes en `.mp3` también se quitan de las guardadas: basta con el `.mp3`.
- El ejecutable de la raíz del proyecto se llama siempre `Launcherito.exe` y se sobrescribe en cada versión, en lugar de dejar uno nuevo por versión (`Launcherito v1.x.exe`).

## [1.5] - 2026-10-10

### Añadido
- **Modo edición del mosaico**: botón «Editar» junto al cambio de vista. Mientras está activo se queda en morado y aparece «Dejar edición» en verde para salir; también se sale al cambiar a la vista original o a otra pestaña. Todo depende de una sola variable (`_isEditing`, en `SetEditing`).
- En modo edición las portadas se **cogen y se arrastran** a otro sitio: siguen al ratón y las demás se apartan con su animación. Cerca del borde de arriba o de abajo el mosaico se desplaza solo. Un clic sin arrastrar no reproduce la canción, para no hacerlo sin querer.
- Cada portada lleva un **tirador en la esquina** para agrandarla o encogerla de celda en celda. Al agrandarla, las grandes que quedan debajo pasan a ocupar una celda; al encogerla, la siguiente crece y ocupa el hueco. La que suena y la del vídeo no bajan de 2x2.
- El orden y los tamaños elegidos se guardan en `%LOCALAPPDATA%\Launcherito\mosaico.json` y se mantienen al cerrar el programa. Las canciones nuevas se colocan al final.

### Técnico
- `MosaicPanel` coloca las portadas por un número de orden (`MosaicPanel.Order`) en vez de por su posición en el panel: se reordenan sin sacarlas, así el vídeo de YouTube no se recarga al mover su portada.
- Nuevas clases `MosaicEditor` (arrastrar y redimensionar) y `MosaicArrangement` (guardar el orden y los tamaños).

## [1.4] - 2026-10-10

### Añadido
- **Vídeo de YouTube dentro de la portada**: al pulsar una canción de Spotify (en el mosaico o en Artistas y Géneros) su portada pasa a verse en grande y dentro se carga su vídeo de YouTube, que empieza solo. Lo que estuviera sonando se pausa. Una X en la esquina cierra el vídeo y la portada vuelve a su tamaño (`YouTubeVideo`).
- El vídeo se busca en YouTube por artista y título, sin clave, y se recuerda mientras la aplicación está abierta (`YouTubeLinks`). Si el primer resultado no se deja ver fuera de YouTube, se prueban los siguientes (hasta 5); si ninguno se puede, o no hay conexión, se abre en el navegador. Los enlaces del propio reproductor («Ver en YouTube») también se abren en el navegador.
- Las portadas de Spotify llevan una marca roja de YouTube en la esquina, y en las listas de Artistas y Géneros un icono propio.
- **Logo de Launcherito**: es el icono del `.exe`, de la barra de tareas y de la ventana, y encabeza el README.

### Cambiado
- Las canciones de Spotify ya no suenan con el fragmento de 30 s ni entran en la lista de reproducción: anterior, siguiente y el paso automático solo recorren los `.mp3`.
- Ya se añaden también las canciones que Spotify no daba con fragmento.
- Al reproducir una canción, cambiar a la vista original o a otra pestaña, el vídeo se cierra y se libera el navegador interno, que ocupa bastante memoria.
- Solo las 20 primeras portadas entran con su animación; las demás aparecen todas a la vez con un fundido rápido, así una lista larga no tarda en verse entera.

### Técnico
- WebView2 (el Edge que trae Windows 11) en su versión de composición (`WebView2CompositionControl`): se dibuja como un elemento más de WPF, así que respeta las esquinas redondeadas, el desplazamiento y las animaciones del mosaico. Por eso el proyecto apunta ahora a `net10.0-windows10.0.17763.0`.
- Los datos del navegador interno se guardan en `%LOCALAPPDATA%\Launcherito\WebView2`, no junto al `.exe`.

## [1.3] - 2026-10-09

### Añadido
- **Listas de Spotify**: botón verde «Añadir lista de Spotify» en la pantalla inicial y «＋ Lista de Spotify» en la barra superior. Se pega el enlace de una lista pública o de un álbum (también `intl-es/…?si=…` y `spotify:playlist:…`); si ya está copiado, aparece pegado solo.
- Las canciones entran en el mosaico con su animación, con la portada de Spotify (640 px, guardada en `%LOCALAPPDATA%\Launcherito\spotify`), y se clasifican en Artistas y Géneros con Deezer como las demás.
- Sin cuenta ni Premium (`SpotifyLibrary`): la API oficial de Spotify exige desde febrero de 2026 que el dueño de la app tenga Premium, así que se lee la página que Spotify ofrece para insertar listas en otras webs y su servicio oEmbed para las portadas.
- Spotify no da el audio completo: si la canción ya está cargada en `.mp3` no se repite y suena entera; si no, suena el fragmento de 30 s de Spotify, marcado como «fragmento de Spotify (30 s)». Si se añade el `.mp3` después, sustituye a la de Spotify. Se reconocen aunque cambien tildes, mayúsculas o añadidos como «(feat. X)».
- Resumen al añadir una lista: canciones añadidas, las que ya tenías en `.mp3`, las repetidas y las que no tienen fragmento. Mensajes claros si el enlace no es de Spotify, la lista es privada o no hay conexión.

### Limitaciones
- Solo listas públicas y álbumes, y como mucho sus 100 primeras canciones (lo que da la página de inserción de Spotify).

## [1.2.1] - 2026-10-09

### Cambiado
- **La ventana ya no se congela al cambiar de canción**: el título y el artista aparecen al momento y las etiquetas y la carátula se leen en segundo plano. El bloqueo medio al pasar de canción baja de ~9 ms a ~2 ms (medido con 90 canciones); si se cambia de canción mientras se lee, la lectura antigua se descarta.
- La carátula grande de la vista original solo se decodifica mientras se ve esa vista, y se suelta al pasar al mosaico o a Artistas/Géneros.
- Las fotos de las tarjetas de Artistas y Géneros se sueltan al salir de su pestaña y se recargan al volver.
- Las portadas del mosaico se decodifican al tamaño real con que se ven en pantalla (teniendo en cuenta el escalado de Windows), con un tope de 400 px; si una portada crece, se recarga más nítida.
- Pinceles y fuentes de iconos compartidos entre todas las portadas, tarjetas y filas en lugar de crear unos nuevos para cada una.

### Corregido
- El temporizador de las tandas de animación se detiene al cerrar la ventana, como los demás.

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
