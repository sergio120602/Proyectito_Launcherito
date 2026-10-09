# 🎵 Launcherito

> Tu música, a tu manera. Un launcher de música bonito, rápido y totalmente personalizable.

![Versión](https://img.shields.io/badge/versión-0.3-blueviolet)
![Estado](https://img.shields.io/badge/estado-en%20desarrollo-orange)
![Plataforma](https://img.shields.io/badge/plataforma-Windows-blue)

---

## ✨ ¿Qué es Launcherito?

**Launcherito** es un launcher de escritorio para gestionar y reproducir tu biblioteca musical desde un único lugar. La idea es sencilla: que escuchar música sea tan agradable como la propia música. Para ello combina una interfaz cuidada al detalle con un sistema de personalización que te deja cambiar prácticamente todo lo que ves.

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
| 0.4 | Escaneo de la biblioteca local, vista de la lista y búsqueda |
| 0.5 | Sistema de temas y personalización |
| 0.6 | Visualizador, color dinámico y animaciones |
| 0.7 | Ecualizador, atajos globales y mini‑reproductor |
| 0.8 | Letras sincronizadas y estadísticas |
| 0.9 | Pulido, rendimiento y corrección de errores |
| **1.0** | 🎉 Primera versión estable |

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
