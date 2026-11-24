# Registro de cambios

Todos los cambios relevantes de **Launcherito** se documentan en este archivo.

El formato se basa en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [Sin publicar]

### Añadido
- **Del momento** (Windows y Android): pestaña con las playlists más escuchadas de Spotify (Top 50 España y Global, Éxitos España, Today's Top Hits, Viva Latino, Baila Reggaeton, Pop Rising y RapCaviar), con su portada. También hay un botón «Del momento» en la pantalla inicial.
  - **Se renuevan cada día a las 7:00** con la tarea programada de Windows «Launcherito - Playlists del momento», que abre `Launcherito.exe --actualizar-del-momento` sin ventana. Si el PC estaba apagado, la tarea se hace al encenderlo, y la aplicación también las renueva si al abrirla (o mientras está abierta) ya ha pasado la hora. «Actualizar ahora» las renueva al momento.
  - Al abrir una se ven sus canciones por puesto. Al pulsar una, o con *Reproducir*, suena la playlist: los datos son de Spotify y la música es el **vídeo de YouTube** de cada canción, que se ve en la vista original como el de las demás. Las que ya tienes en `.mp3` suenan enteras.
  - *Guardar en mis playlists* la copia tal como está ese día a «Tus playlists». *Ver en Spotify* la abre en Spotify.
  - No llenan «Tus canciones» (cambian cada día), pero una canción que se pone en Favoritos o en una playlist se guarda allí para que no se pierda.
- **Buscar canciones en YouTube y quedártelas** (Windows y Android). Barra «Buscar canciones en YouTube…» en la pantalla inicial, «＋ Desde YouTube» arriba y, en el buscador de canciones, una última fila «Buscar «…» en YouTube» (si no hay ninguna canción tuya que coincida, Enter la elige).
  - Panel con pestañas **Canciones** (de álbumes, con su portada) y **Vídeos** (videoclips, directos, versiones…), hasta 20 resultados con imagen, artista y duración.
  - **Añadir** la guarda en «Tus canciones» y la pone en el mosaico; **▶** además la pone a sonar. Suenan con su vídeo, como las de YouTube Music, y se pueden meter en Favoritos y en playlists como cualquier otra.
  - También se puede pegar el **enlace de un vídeo** (`youtube.com/watch?v=…`, `youtu.be/…`, `music.youtube.com/watch?v=…`, Shorts). Si se pega el de una lista o un álbum, se abre el cuadro de listas de siempre.
  - Las que ya tienes en `.mp3` no se repiten: el botón dice «Ya la tienes en .mp3» y ▶ pone el archivo.
- **Favoritos y playlists propias** (Windows y Android). Se guardan solas en `%LOCALAPPDATA%\Launcherito\playlists.json`.
  - **Favoritos**: corazón junto a los controles (a la izquierda del aleatorio) para marcar la canción que suena; se pone rosa si ya es favorita. Favoritos es una lista fija: no se puede renombrar ni borrar.
  - **Pestaña Playlists** (y botón «Tus playlists» en la pantalla inicial, aunque no haya nada cargado): una tarjeta para Favoritos, otra por cada playlist y «Nueva playlist». Al abrir una se ven sus canciones y los botones *Reproducir*, *Añadir canciones*, *Renombrar* y *Borrar playlist*. La X de cada canción la quita de la playlist.
  - **Vista mosaico o vista lista** en cada playlist (botón «Vista lista» / «Vista mosaico»; cada playlist recuerda la suya y empieza en mosaico). El mosaico es como el de siempre: portadas de los álbumes de distintos tamaños; al pulsar una suena la playlist desde ella. Con **Editar** (en verde «Dejar edición» mientras dura) las portadas se mueven y se agrandan o encogen con el tirador. **Moverlas cambia el orden de la playlist** (el que se sigue sin aleatorio, también si está sonando) y el tamaño de cada portada se guarda en esa playlist.
  - **Añadir canciones**: panel con casillas entre las de «Tus canciones» (`.mp3`, Spotify y YouTube Music), cada una con la **carátula de su álbum**, y filtro por canción o artista sin importar mayúsculas ni tildes. Se abre solo al crear una playlist nueva. Las carátulas se cargan en segundo plano, primero las que se ven.
  - **Clic derecho** en una portada del mosaico o en una canción de Artistas, Géneros o una playlist, y el botón **＋** de los controles: menú con «Añadir a Favoritos» / «Quitar de Favoritos», cada playlist (marcada si ya la tiene; al pulsarla se pone o se quita) y «Nueva playlist…», que la crea con esa canción dentro.
  - **Escuchar una playlist**: *Reproducir* deja en el mosaico solo sus canciones y pasa a la vista de canciones. Con el aleatorio apagado suenan **en el orden de la playlist**. Arriba se ve «Escuchando «…»» y *Ver todas* devuelve todas tus canciones sin cortar la que suena. Al pulsar una canción en el detalle, la playlist empieza por ella.
  - Lo que se añade o se quita de la playlist que está sonando entra o sale del mosaico al momento.
- **Barra de reproducción abajo al desplazarse por el mosaico** (Windows y Android). Mientras la portada que suena se ve, los controles siguen dentro de ella; en cuanto se pierde de vista al bajar o subir, aparece abajo una barra con la canción, la barra de tiempo y los mismos botones (pausa, anterior, siguiente…), para parar o cambiar la música sin ir a buscarla. **«Ir a la canción»** (o pulsar su título) vuelve a la portada y la barra se va sola. En Artistas, Géneros y Playlists la barra de abajo sigue como siempre.

- **Android: lo mismo que en Windows**, adaptado al móvil:
  - **Favoritos y playlists**: el corazón y el botón de añadir a una playlist van en los controles (la posición en la lista pasa a la línea de los tiempos para que quepan). **Mantener pulsada** una portada o una canción abre el menú con Favoritos, cada playlist (marcada si ya la tiene) y «Nueva playlist…». Pestaña **Playlists** y botón «Tus playlists» en la pantalla inicial; cada playlist se ve en **mosaico** (con «Editar» las portadas se mueven, manteniéndolas pulsadas, y se agrandan con su esquina; moverlas cambia el orden de la playlist) o en **lista** (la X quita la canción). Al crear una playlist se abre el panel para elegir sus canciones, cada una con la carátula de su álbum y con filtro.
  - **Buscar en YouTube**: barra en la pantalla inicial, «Buscar en YouTube» en el menú ⋮ y la última fila del buscador. Pestañas Canciones y Vídeos; ▶ la pone a sonar y «Añadir» la guarda. También vale el enlace de un vídeo.
  - **Del momento**: pestaña y botón en la pantalla inicial, con las mismas 8 playlists de Spotify. Se renuevan cada día a las 7:00 con **WorkManager** (sigue programado tras reiniciar el móvil y espera a tener conexión) y la app también las renueva al abrirse si ya ha pasado la hora.
  - Arriba, mientras suena solo una playlist: «Escuchando «…»» y «Ver todas».
- **Botón para actualizar** (Windows y Android). Al final de la pantalla inicial se ve la versión instalada y «Buscar actualizaciones». Al abrir la app se mira sin molestar si hay una versión nueva en GitHub; si la hay, sale **«Actualizar a la X»** (en Windows también arriba en el reproductor; en Android, también en el menú ⋮). Se pregunta antes y tus canciones, playlists y ajustes se quedan como están.
  - **Windows**: se descarga el `.zip` de la versión, el `Launcherito.exe` nuevo ocupa el sitio del actual y Launcherito se vuelve a abrir solo. Si la carpeta no se puede escribir (p. ej. *Archivos de programa*), lo explica y ofrece la página de GitHub.
  - **Android**: se descarga el `.apk` y se abre el instalador del sistema, que pide confirmar. La primera vez Android pide permitir que Launcherito instale apps. Si la versión nueva está firmada con otra clave (Android no la instalaría encima), se avisa en lugar de fallar.
  - Las versiones anteriores a esta no tienen el botón: para llegar a la primera que lo tiene hay que actualizar a mano una vez.

### Cambiado
- Los controles de reproducción son algo más anchos para que quepan el corazón y el ＋ (dentro de una portada pequeña se reducen como antes).
- Al borrar canciones de «Tus canciones» también se quitan de Favoritos y de las playlists. Si una canción de Spotify o YouTube Music se cambia por el `.mp3` que ya tienes, en las playlists pasa a estar el `.mp3`, en el mismo sitio.

### Corregido
- Una lista de Spotify cuya portada trae el ancho vacío ya no cierra el programa (pasaba al leer la portada de la propia lista).

### Técnico
- Android: `UserPlaylists`, `TrendingPlaylists` y `TrendingWorker` (nuevos) guardan `playlists.json` y `del-momento.json` en los archivos de la app, con el mismo formato que en Windows. El mosaico (`Mosaic.kt`) recibe un `MosaicModel` (sus portadas y qué hacer con ellas), así sirve para el reproductor y para cada playlist. Nueva dependencia: `androidx.work:work-runtime-ktx`. Pantallas nuevas en `Playlists.kt` y `PlaylistDialogs.kt`.
- `TrendingPlaylists` (nuevo) lee las playlists del momento con la misma página de Spotify para insertar listas que ya se usaba (sin cuenta ni claves: la API oficial pide Premium al dueño de la app y desde finales de 2024 no da las playlists editoriales de Spotify a las apps nuevas). Se guardan en `%LOCALAPPDATA%\Launcherito\del-momento.json` y cada intento se apunta en `del-momento.log`. Si una no se puede leer, se queda la del día anterior. La pestaña está en `MainWindow.Trending.cs`.
- `App` ya no abre la ventana con `StartupUri`: con `--actualizar-del-momento` solo actualiza y se cierra. `OnlineList` trae también la portada de la lista (`CoverUrl`).
- `YouTubeMusicLibrary.SearchAsync` usa la búsqueda de la web de YouTube Music (`youtubei/v1/search`, con sus filtros de canciones y de vídeos). `LoadVideoAsync` lee un vídeo suelto con el servicio público oEmbed de YouTube. Las canciones encontradas se guardan como las de YouTube Music (`ytmusic:<id del vídeo>`). La ventana está en `MainWindow.YouTube.cs`.
- El mosaico de una playlist (`MainWindow.PlaylistMosaic.cs`) usa las mismas piezas que el mosaico de siempre (`MosaicPanel`, `MosaicTile`, `MosaicEditor`, `TileAnimator`) con su propio panel. Cada playlist guarda `ListView` y `Spans` (tamaño de cada portada) en `playlists.json`; los archivos anteriores se leen igual.
- `UserPlaylists` (nuevo) guarda Favoritos y las playlists: cada una con un id, su nombre y las claves de sus canciones en orden. La lógica de la ventana está en `MainWindow.Playlists.cs`.
- `Playlist` admite un orden propio (`SetCustomOrder`) que se usa sin aleatorio, y `Clear`. El mosaico sigue en orden alfabético.
- `CategoryCard` acepta icono y fondo propios (tarjetas de las playlists). `SongLibrary.TryGet` busca una canción guardada por su clave.
- Barra de reproducción: en Windows, la de abajo (`PlayerBar`) sale de la vista de categorías y pasa a una fila propia del reproductor que comparten todas las vistas. `UpdatePlayerBar` decide si se ve y `MoveControls` lleva allí el único juego de controles cuando la franja de abajo de la portada que suena (donde van sus controles) queda fuera de lo visible; se comprueba en cada desplazamiento. En Android, `MosaicView` hace la misma cuenta con su desplazamiento y pone `BottomBar` encima del mosaico, con animación. Al final del mosaico se deja también el sitio que tapa la barra.
- Actualizaciones: `Updater` (Windows, `MainWindow.Update.cs`) y `AppUpdater` (Android, `Updates.kt`) preguntan a la API de GitHub por la última Release y, si no responde (admite 60 consultas por hora desde una conexión sin cuenta), miran a qué versión lleva `releases/latest` y usan los nombres fijos de los archivos: **`Launcherito-vX-win-x64.zip`** y **`Launcherito-vX.apk`**. En Windows el `.exe` abierto se renombra a `Launcherito.old.exe` (se deja renombrar, no sobrescribir) y se borra al abrir la siguiente vez. En Android, permiso `REQUEST_INSTALL_PACKAGES` y un `FileProvider` (`res/xml/update_paths.xml`) para pasar el `.apk` al instalador.
- **Importante al publicar una versión**: la etiqueta debe coincidir con `VERSION`, los archivos deben llamarse como arriba y el `.apk` debe firmarse **siempre con la misma clave**. El de la v1.10.1 está firmado con una clave de depuración distinta de la de este PC, así que un `.apk` compilado aquí no se puede instalar encima de él. Conviene crear una clave propia de publicación y usarla a partir de ahora.

## [1.11] - 2026-11-20

### Añadido
- **Listas y álbumes de YouTube Music**, en Windows y en Android. Botón «Añadir lista de YouTube Music» en la pantalla inicial y «＋ Lista de YouTube Music» arriba (en Android, en el menú ⋮). Se pega el enlace de una lista pública o no listada, o de un álbum, sin cuenta ni Premium, y sus canciones entran en el mosaico con su portada.
  - Valen los enlaces de `music.youtube.com/playlist?list=…`, `music.youtube.com/browse/MPREb_…` (página de un álbum) y cualquier enlace de `youtube.com` o `youtu.be` con `list=`.
  - Se leen hasta 500 canciones por lista (Spotify se queda en 100).
  - Cada canción **suena con su propio vídeo** (ya viene en la lista, no hace falta buscarlo). Si ese vídeo no se puede ver dentro de la aplicación, se prueba con los que da YouTube al buscar la canción.
  - De los videoclips se limpia el título: «Artista - Canción (Official Video)» queda en «Canción». En los subidos por cualquiera, el artista es el de delante del guion, no el canal.
  - Las que ya tienes en `.mp3` suenan enteras y no se repiten, como con Spotify.
  - Los mix y las listas privadas («Me gusta», «Ver más tarde») no se pueden leer sin entrar en la cuenta: se explica con un aviso.
- Marca de procedencia de YouTube Music en cada portada: un círculo rojo con un aro y el triángulo de reproducir. En «Borrar canciones» salen como «YouTube Music», en rojo.
- Android: al compartir una lista o un álbum desde la app de YouTube Music y elegir Launcherito, se abre el cuadro con el enlace ya puesto.

### Cambiado
- El cuadro del enlace sirve para los dos servicios: toma el título y el color del botón con que se abre, pero admite el enlace de Spotify o de YouTube Music.
- Los avisos de la barra de arriba van en verde si son de Spotify y en rojo si son de YouTube Music. En Android, los de Spotify antes iban en gris.

### Técnico
- `OnlineSongs` reúne lo común a Spotify y YouTube Music: el registro de canciones (`OnlineTrack`, antes `SpotifyTrack`), reconocer enlaces, leer listas, la caché de portadas y la clave para reconocer un `.mp3` que ya se tiene. `SpotifyLibrary` solo lee Spotify.
- `YouTubeMusicLibrary` usa el mismo servicio que la web de YouTube Music (`youtubei/v1/browse`, cliente `WEB_REMIX`), de 100 en 100 canciones. Busca los datos por nombre de campo y no por su ruta exacta, para aguantar mejor los cambios de YouTube.
- Las canciones de YouTube Music se identifican como `ytmusic:<id del vídeo>`. En «Tus canciones» se guarda también la dirección de su portada (`CoverUrl`). Los `canciones.json` anteriores se siguen leyendo igual.

## [1.10.1] - 2026-10-10

### Añadido
- **Android: letra pequeña o grande.** Se elige en la pantalla inicial («Letra: Pequeña · Grande») o en el menú ⋮ («Letra grande») y se recuerda. La primera vez se elige sola: grande si el móvil tiene la letra aumentada.
  - **Pequeña**: diseño compacto; la letra no pasa del tamaño normal aunque el móvil la tenga más grande. Caben 3 portadas por fila.
  - **Grande**: respeta la letra del móvil (entre 1,3× y 1,5×). El número de canciones y los botones «Editar» y «Vista original» van en filas separadas, caben 2 portadas por fila y los botones de reproducir, anterior y siguiente son más grandes.

### Cambiado
- Android: el botón de la vista original pasa a ser como en Windows, con icono y texto («Vista original» / «Vista mosaico»), junto a «Editar» bajo las pestañas. Antes era un icono suelto en la barra de arriba que no se reconocía.
- Android: la ayuda del modo edición va en su propia línea y el buscador dice solo «Buscar».

### Corregido
- Android: con la letra del móvil aumentada quedaban grandes huecos negros encima y debajo de las pestañas. El botón «Editar» no cabía y su texto se partía letra a letra, estirando la fila. Ahora las pestañas y los botones nunca parten su texto y, si las pestañas no caben, se desplazan de lado.

## [1.10] - 2026-10-10

### Añadido
- **Versión para Android** en la carpeta `android` (Android 8 o posterior), con lo mismo que la de Windows: vista mosaico con los controles dentro de la portada que suena, **vista original** con el vídeo de YouTube de la canción (botón «Vista original» / «Vista mosaico»), pestañas Artistas y Géneros con Deezer, listas y álbumes de Spotify, «Tus canciones», «Borrar canciones», buscador y modo edición del mosaico.
- **Música en segundo plano**: los `.mp3` siguen sonando con la app cerrada o la pantalla apagada, con una notificación con la carátula, pausa, anterior y siguiente (que siguen el orden de la lista de Launcherito). También en la pantalla de bloqueo y con los botones de los auriculares; la música se pausa al desconectarlos.
- **Compartir desde Spotify**: al compartir una lista o un álbum desde la app de Spotify y elegir Launcherito, se abre el cuadro de la lista con el enlace ya puesto.
- *Abrir con → Launcherito* con un `.mp3` del móvil.

### Cambiado (respecto a Windows)
- Las canciones se eligen con el selector de archivos de Android y se identifican por su dirección `content://`; la app guarda el permiso para volver a leerlas al abrirse de nuevo.
- Modo edición: las portadas se mueven **manteniéndolas pulsadas** (arrastrar sin más desplaza el mosaico) y el botón para salir se llama «Listo».
- En el móvil caben 3 portadas por fila; la que suena ocupa todo el ancho para que quepan los controles. El título de cada portada se ve siempre (no hay «pasar el ratón»).
- El botón Atrás deshace paso a paso: el buscador, la edición, el artista o género abierto, la pestaña y, por último, vuelve al menú principal.

### Técnico
- Kotlin, Jetpack Compose y Media3 (ExoPlayer en un `MediaSessionService`). La lógica de `MainWindow.xaml.cs` está en `LauncheritoViewModel`; `Playlist`, `SongLibrary`, `MusicCatalog`, `SpotifyLibrary`, `YouTubeLinks` y `MosaicArrangement` son la traducción directa de las clases de C#.
- Las carátulas las carga Coil con un `Fetcher` propio (`SongCover`) que lee la imagen del `.mp3` o de la caché de Spotify, solo para las portadas cercanas a la pantalla.
- `YouTubeVideo` usa un WebView con la misma página que en Windows; como un WebView no se puede hacer transparente, mientras no debe verse queda tapado por la carátula (o por la pantalla, en el hueco oculto).
- La versión de la app de Android sale del archivo `VERSION`.
- Pendiente de probar en un móvil real: el vídeo de YouTube (el emulador no decodifica VP9) y si las canciones de Spotify siguen sonando con la app en segundo plano.

## [1.9.1] - 2026-10-10

### Añadido
- **Marca de procedencia en cada portada del mosaico**, arriba a la izquierda: un círculo morado con una nota si es un `.mp3` tuyo y uno verde con las tres ondas del logo si viene de Spotify. Las de Spotify conservan además la marca roja de YouTube a la derecha.

## [1.9] - 2026-10-10

### Cambiado
- **Las canciones de Spotify suenan en el reproductor**, como las `.mp3`: entran en la lista de reproducción (anterior, siguiente, aleatorio, paso automático, posición «3 / 75») y se manejan con los controles del programa. El sonido sale de su vídeo de YouTube.
- **Vista original**: con una canción de Spotify se ve su vídeo, igual que con las `.mp3`.
- **Mosaico, Artistas y Géneros**: la portada que suena muestra sus controles y el vídeo va en un hueco invisible: se oye pero no se ve. Al cambiar de vista el mismo vídeo pasa de un sitio a otro sin cortarse.
- Al pulsar una portada de Spotify suena (antes se abría su vídeo, con los controles de YouTube, dentro de la portada). El artista lleva la marca «vídeo de YouTube».
- Si una canción de Spotify no tiene vídeo que se pueda ver dentro de la aplicación, se pasa a la siguiente (sin dar vueltas si no tiene ninguna); sin conexión con YouTube se para con un aviso.

### Corregido
- La primera canción tras cargar desde el menú principal podía no arrancar en vídeo: el vídeo se preparaba con el reproductor aún oculto (sin tamaño, y YouTube no reproduce por debajo de 200 px). Ahora el reproductor se muestra antes y el hueco del vídeo oculto está fuera de él, así que también sigue sonando en el menú principal.

### Técnico
- Ya no hay «accesos directos» aparte (`_shortcuts`): el mosaico es la lista de reproducción. Se quitan `OpenVideoAsync`, `CloseVideo` y el vídeo con controles de YouTube dentro de la portada.
- `WantedVideoHost` decide dónde va el vídeo (vista original, hueco invisible o ninguno) y `UpdateSongSource` mueve el sonido al cambiar de vista; `MoveSongVideo` cambia el navegador de sitio sin cerrarlo.

## [1.8] - 2026-10-10

### Añadido
- **Vídeo de YouTube en la vista original**: en lugar de la carátula se ve el vídeo de la canción que suena, y el sonido sale del vídeo. Se maneja solo con los controles del programa: reproducir/pausar, la barra (también arrastrándola), anterior, siguiente y el paso automático al acabar. El vídeo no tiene controles de YouTube ni responde a los clics.
- Mientras se ve el vídeo, el marco pasa de cuadrado a 16:9 y la barra muestra la duración del vídeo. Mientras se busca se ve la carátula con «Buscando el vídeo en YouTube…».
- Al cambiar entre la vista original y el mosaico la canción sigue por el mismo segundo: del `.mp3` al vídeo y del vídeo al `.mp3`.
- Si la canción no tiene vídeo que se pueda ver dentro de la aplicación, o no hay conexión, se queda la carátula y suena el `.mp3`, con un aviso arriba. Se buscan hasta 5 vídeos por canción, por artista y título.

### Técnico
- `YouTubeVideo` tiene un modo controlado (`controlled: true`): sin controles de YouTube, órdenes `Play`, `Pause`, `Seek`, `Load` (otra canción en el mismo navegador, sin crear otro) y `Hold`, y avisos `Playing`, `Ended` y `Progress` (cada 250 ms). Las órdenes que llegan antes de que cargue la página se guardan y se envían después.
- `YouTubeLinks.FindVideosAsync` sirve para cualquier canción (artista y título), no solo para las de Spotify.
- Al salir de la vista original el navegador del vídeo se libera.

## [1.7] - 2026-10-10

### Añadido
- **Botón «Inicio»** a la izquierda de las pestañas: vuelve al menú principal sin parar la música. Allí aparece un botón verde **«Volver al reproductor»** (con el nombre de la que suena) mientras haya canciones cargadas. Cargar archivos, «Tus canciones» o una lista de Spotify desde el menú también lleva de vuelta al reproductor.
- **Buscador de canciones** en la barra superior (también con `Ctrl+F`): mientras se escribe, aparece una lista con las canciones cuyo nombre (o artista) contiene lo escrito, sin distinguir mayúsculas ni tildes. Primero las que empiezan por ello; la parte que coincide sale en morado y el artista a la derecha. Al pulsar una suena (o se ve su vídeo, si es de Spotify); Enter elige la primera y Esc borra la búsqueda. Busca también entre las guardadas en «Tus canciones» que aún no están cargadas, y las carga al elegirlas. Como mucho 50 resultados.

### Cambiado
- La barra superior pasa a tener dos filas: arriba Inicio, las pestañas, el buscador, Editar y el cambio de vista; debajo, añadir canciones, Tus canciones, Borrar y el número de canciones.
- La fila de canción del detalle de artistas y géneros y la de los resultados del buscador salen de la misma función (`CreateSongRow`).

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
