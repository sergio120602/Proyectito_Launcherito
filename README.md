<p align="center">
  <img src="Launcherito_logo.png" alt="Logo de Launcherito" width="420">
</p>

# 🎵 Launcherito

> Tu música, a tu manera. Un launcher de música bonito, rápido y totalmente personalizable.

![Versión](https://img.shields.io/badge/versión-1.9.1-blueviolet)
![Estado](https://img.shields.io/badge/estado-en%20desarrollo-orange)
![Plataforma](https://img.shields.io/badge/plataforma-Windows-blue)

---

## ✨ ¿Qué es Launcherito?

**Launcherito** es un launcher de escritorio para gestionar y reproducir tu biblioteca musical desde un único lugar. La idea es sencilla: que escuchar música sea tan agradable como la propia música. Para ello combina una interfaz cuidada al detalle con un sistema de personalización que te deja cambiar prácticamente todo lo que ves.

## 🧩 Qué hace ya

- **Vista mosaico**: todas tus canciones como un mosaico de portadas de distintos tamaños. La que suena se ve en grande, con la barra y los controles dentro de la propia portada.
- **Vista original**: arriba se ve el **vídeo de YouTube de la canción que suena** (en lugar de la carátula) y se maneja con los controles del programa: reproducir/pausar, la barra, anterior y siguiente. Si la canción no tiene vídeo o no hay conexión, se ve la carátula y suena el `.mp3`. Se cambia entre las dos vistas con el botón de arriba a la derecha.
- Carga de varios `.mp3` a la vez (o *Abrir con → Launcherito*) y lista de reproducción sin límite.
- Anterior / siguiente, modo aleatorio (activado por defecto) u orden alfabético, y paso automático a la siguiente canción.
- **Modo edición del mosaico**: con el botón *Editar* (se queda en morado; para salir, *Dejar edición* en verde) las portadas se cogen y se llevan a otro sitio, y con el tirador de su esquina se agrandan o se encogen. Al agrandar una, las grandes que tapa se encogen; al encogerla, la siguiente crece y ocupa el hueco. El orden y los tamaños se guardan en `%LOCALAPPDATA%\Launcherito\mosaico.json`.
- Las portadas del mosaico se cargan en segundo plano solo cuando se ven, para que una biblioteca grande no dispare la memoria.
- **Pestañas Artistas y Géneros**: las canciones se clasifican automáticamente con la [API de Deezer](https://developers.deezer.com/api) (gratuita, sin clave). Cada artista tiene su foto y cada género su imagen; al pulsar uno se ven sus canciones. Lo consultado se guarda en `%LOCALAPPDATA%\Launcherito\catalogo.json`, así que solo se pregunta una vez por canción.
- **Listas de Spotify**: en la pantalla inicial (o con *＋ Lista de Spotify* arriba) se pega el enlace de una lista pública o de un álbum y sus canciones entran en el mosaico con su portada. No hace falta cuenta ni Premium. Spotify no da el audio completo: las canciones que ya tienes en `.mp3` suenan enteras y no se repiten; las demás llevan una marca roja de YouTube y **suenan como cualquier otra canción, con el sonido de su vídeo de YouTube**: entran en la lista, se manejan con los mismos controles (anterior, siguiente, aleatorio, la barra) y en la vista original se ve el vídeo. Si una no tiene vídeo que se pueda ver dentro de la aplicación, se pasa a la siguiente. Se leen hasta 100 canciones por lista y las portadas se guardan en `%LOCALAPPDATA%\Launcherito\spotify`.
- **Tus canciones**: todo lo que cargas (`.mp3` y listas de Spotify) se guarda solo en `%LOCALAPPDATA%\Launcherito\canciones.json`. Al abrir el programa, el botón *Tus canciones* las vuelve a cargar todas, y *Borrar canciones* deja elegir con casillas cuáles quitar (los archivos `.mp3` del PC no se tocan).
- **Inicio y buscador**: el botón *Inicio* vuelve al menú principal sin parar la música (y *Volver al reproductor* regresa). El buscador de arriba (o `Ctrl+F`) encuentra canciones por su nombre o su artista, sin importar mayúsculas ni tildes; al pulsar una, suena.

## 🚀 Características previstas

### 🎧 Gestión de música
- Importación automática de tu biblioteca local (MP3, FLAC, WAV, OGG, M4A).
- Organización por artistas, álbumes, géneros y años.
- Listas de reproducción manuales e **inteligentes** (por ejemplo: "las más escuchadas este mes").
- Búsqueda instantánea mientras escribes.
- Lectura y edición de metadatos y carátulas.

### 🎨 Interfaz bonita y personalizable
- **Temas**: claro, oscuro y temas creados por ti.
- **Color dinámico**: la interfaz se adapta a los colores de la carátula que está sonando.
- Fondos animados, efectos de desenfoque (glassmorphism) y transiciones suaves.
- Visualizador de audio en tiempo real con varios estilos.
- Disposición de paneles modular: mueve, oculta o redimensiona cada sección.
- Mini‑reproductor flotante siempre visible.

### ⚡ Comodidad
- Atajos de teclado globales (play/pausa, siguiente, anterior, volumen).
- Integración con los controles multimedia de Windows.
- Ecualizador con presets.
- Letras sincronizadas (cuando estén disponibles).
- Estadísticas de escucha: tus artistas y canciones favoritas.

## 🗺️ Hoja de ruta

| Versión | Objetivo |
|---------|----------|
| **0.1** | Creación del proyecto, repositorio y documentación inicial ✅ |
| **0.2** | Aplicación base: ventana, carga de un .mp3, carátula y reproductor sencillo ✅ |
| **0.3** | Carga de varias canciones, lista de reproducción, anterior/siguiente y modo aleatorio ✅ |
| **1.0** | 🎉 Nueva interfaz: vista mosaico con controles dentro de la portada y cambio de vista ✅ |
| **1.1** | Animaciones de entrada en el mosaico y portadas desenfocadas más ligeras ✅ |
| **1.1.1** | Portadas desenfocadas solo durante la animación y nítidas al quedarse quietas ✅ |
| **1.1.2** | La ventana muestra la versión del programa ✅ |
| **1.1.3** | Animaciones más lentas: se ve cómo se forma el mosaico desde la canción que suena ✅ |
| **1.2** | Pestañas Canciones / Artistas / Géneros: clasificación con la API de Deezer y fotos de cada artista y género ✅ |
| **1.2.1** | La ventana no se congela al cambiar de canción y menos memoria en las vistas que no se ven ✅ |
| **1.3** | Listas y álbumes de Spotify desde un enlace, con sus portadas y fragmentos; las que tienes en .mp3 suenan enteras ✅ |
| **1.4** | El vídeo de YouTube de las canciones de Spotify se ve dentro de su portada ✅ |
| **1.5** | Modo edición: mover las portadas del mosaico y cambiar su tamaño, guardado entre sesiones ✅ |
| **1.6** | Las canciones se guardan: «Tus canciones» para volver a cargarlas y «Borrar canciones» ✅ |
| **1.7** | Botón Inicio para volver al menú principal y buscador de canciones ✅ |
| **1.8** | Vista original con el vídeo de YouTube de la canción, manejado con los controles del programa ✅ |
| **1.9** | Las canciones de Spotify suenan en el reproductor con su vídeo de YouTube ✅ |
| **1.9.1** | Marca de procedencia en cada portada: tus .mp3 en morado, las de Spotify en verde ✅ |
| 2.0 | Menú de carga: una canción, varias o una carpeta entera |
| 2.1 | Sistema de temas y personalización |
| 2.2 | Visualizador y color dinámico |
| 2.3 | Ecualizador, atajos globales y mini‑reproductor |
| 2.4 | Letras sincronizadas y estadísticas |

## 📦 Instalación

### Opción 1: descargar el ejecutable
1. Ve a [Releases](https://github.com/sergio120602/Proyectito_Launcherito/releases) y descarga el `.zip` de la última versión.
2. Descomprímelo y abre `Launcherito.exe`. No necesita instalación ni tener .NET instalado.
3. También puedes abrir un `.mp3` directamente con *clic derecho → Abrir con → Launcherito.exe*.

### Opción 2: compilar desde el código
Requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) en Windows.

```bash
git clone https://github.com/sergio120602/Proyectito_Launcherito.git
cd Proyectito_Launcherito/src/Launcherito
dotnet run                                   # ejecutar en modo desarrollo
dotnet publish -c Release -o ../../publish   # generar publish/Launcherito.exe
```

## 🛠️ Tecnología
- **C# / WPF** sobre **.NET 10**
- [TagLibSharp](https://github.com/mono/taglib-sharp) para leer metadatos y carátulas
- [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) para ver los vídeos de YouTube dentro de las portadas

## 🔖 Control de versiones

El proyecto sigue un esquema de versiones `MAYOR.MENOR.PARCHE` (el parche se usa para correcciones y optimizaciones):

- La versión actual se guarda en el archivo [`VERSION`](VERSION).
- Todos los cambios de cada versión se documentan en [`CHANGELOG.md`](CHANGELOG.md).
- Cada versión publicada queda marcada en Git con una etiqueta (`v0.1`, `v0.2`, …).

## 🤝 Contribuir

Las ideas y sugerencias son bienvenidas: abre un *issue* o envía un *pull request*.

## 📄 Licencia

Proyecto personal. Licencia por definir.

---

Hecho con 💜 y mucha música por **Sergio**.
