using System.IO;
using System.Windows.Media.Imaging;

namespace Launcherito;

/// <summary>Título, artista y carátula de una canción, leídos de sus etiquetas ID3 o de Spotify.</summary>
public sealed record SongInfo(string Title, string Artist, BitmapImage? Cover)
{
    /// <summary>Artista que se muestra cuando la canción no lo trae en sus etiquetas.</summary>
    public const string UnknownArtist = "Artista desconocido";

    /// <summary>Copia diminuta de la carátula, solo si se pide al leerla (null si no).</summary>
    public BitmapImage? Preview { get; init; }

    // TagLib lee las etiquetas en trozos de 1 KB saltando por el archivo. Con un buffer de 64 KB
    // la cabecera ID3 (y casi siempre la carátula) se trae del disco en una sola lectura.
    private const int ReadBufferSize = 64 * 1024;

    /// <summary>
    /// Lee las etiquetas de un .mp3. Se puede llamar desde cualquier hilo: la carátula se devuelve congelada.
    /// </summary>
    /// <param name="maxCoverSize">Ancho máximo en píxeles con el que se decodifica la carátula; 0 para no leerla.</param>
    /// <param name="previewSize">Si es mayor que 0, ancho de una segunda copia diminuta, sacada de los mismos datos.</param>
    public static SongInfo Read(string path, int maxCoverSize, int previewSize = 0)
    {
        if (SpotifyLibrary.TryGet(path, out var track))
            return ReadSpotify(track, maxCoverSize, previewSize);

        string title = Path.GetFileNameWithoutExtension(path);
        string artist = UnknownArtist;
        byte[]? coverData = null;

        try
        {
            // El bloque using equivale al try-with-resources de Java: el archivo se cierra al salir
            // del bloque, antes de decodificar la carátula. ReadStyle.None evita analizar todo el
            // audio para calcular su duración, que aquí ya obtiene el reproductor.
            // bufferSize 0 desactiva el buffer propio de FileStream para que no haya dos buffers seguidos.
            using (var stream = new BufferedStream(
                       new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0),
                       ReadBufferSize))
            using (var file = TagLib.File.Create(
                       new ReadOnlyAbstraction(path, stream), TagLib.ReadStyle.None))
            {
                if (!string.IsNullOrWhiteSpace(file.Tag.Title))
                    title = file.Tag.Title;
                if (!string.IsNullOrWhiteSpace(file.Tag.FirstPerformer))
                    artist = file.Tag.FirstPerformer;
                if (maxCoverSize > 0 && file.Tag.Pictures.Length > 0)
                    coverData = file.Tag.Pictures[0].Data.Data;
            }
        }
        catch (Exception)
        {
            // Etiquetas ilegibles: se usa el nombre del archivo y la carátula por defecto.
        }

        return new SongInfo(title, artist, coverData is null ? null : LoadImage(coverData, maxCoverSize))
        {
            Preview = coverData is null || previewSize <= 0 ? null : LoadImage(coverData, previewSize),
        };
    }

    /// <summary>Título que se puede mostrar antes de leer las etiquetas: el de Spotify o el nombre del archivo.</summary>
    public static string FallbackTitle(string path) =>
        SpotifyLibrary.TryGet(path, out var track) ? track.Title : Path.GetFileNameWithoutExtension(path);

    /// <summary>
    /// Canción de Spotify: los datos ya se conocen y la portada se descarga (o sale de la caché). El
    /// artista lleva la marca del fragmento para que se vea en el mosaico y en el reproductor.
    /// </summary>
    private static SongInfo ReadSpotify(SpotifyTrack track, int maxCoverSize, int previewSize)
    {
        byte[]? coverData = maxCoverSize > 0 ? SpotifyLibrary.GetCoverData(track) : null;
        return new SongInfo(track.Title, $"{track.Artists}  ·  fragmento de Spotify (30 s)",
            coverData is null ? null : LoadImage(coverData, maxCoverSize))
        {
            Preview = coverData is null || previewSize <= 0 ? null : LoadImage(coverData, previewSize),
        };
    }

    /// <summary>Entrega a TagLib un stream de solo lectura ya abierto; el stream lo cierra quien lo creó.</summary>
    private sealed class ReadOnlyAbstraction(string name, Stream stream) : TagLib.File.IFileAbstraction
    {
        public string Name => name;
        public Stream ReadStream => stream;
        public Stream WriteStream => throw new NotSupportedException("Launcherito no modifica las etiquetas.");
        public void CloseStream(Stream s) { }
    }

    private static BitmapImage? LoadImage(byte[] data, int maxSize)
    {
        try
        {
            var image = new BitmapImage();
            using var stream = new MemoryStream(data);
            image.BeginInit();
            // OnLoad copia los píxeles al crearla, así el stream puede liberarse al salir del método.
            image.CacheOption = BitmapCacheOption.OnLoad;
            // Limita la resolución decodificada: una carátula de 3000x3000 ocuparía ~36 MB en memoria.
            // DelayCreation lee solo la cabecera para conocer el tamaño sin decodificar la imagen.
            int width = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).PixelWidth;
            if (width > maxSize)
                image.DecodePixelWidth = maxSize;
            stream.Position = 0;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
