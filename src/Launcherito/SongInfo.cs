using System.IO;
using System.Windows.Media.Imaging;

namespace Launcherito;

/// <summary>Título, artista y carátula de una canción, leídos de sus etiquetas ID3.</summary>
public sealed record SongInfo(string Title, string Artist, BitmapImage? Cover)
{
    /// <summary>
    /// Lee las etiquetas de un .mp3. Se puede llamar desde cualquier hilo: la carátula se devuelve congelada.
    /// </summary>
    /// <param name="maxCoverSize">Ancho máximo en píxeles con el que se decodifica la carátula.</param>
    public static SongInfo Read(string path, int maxCoverSize)
    {
        string title = Path.GetFileNameWithoutExtension(path);
        string artist = "Artista desconocido";
        byte[]? coverData = null;

        try
        {
            // El bloque using equivale al try-with-resources de Java: el archivo se cierra al salir
            // del bloque, antes de decodificar la carátula. ReadStyle.None evita analizar todo el
            // audio para calcular su duración, que aquí ya obtiene el reproductor.
            using (var file = TagLib.File.Create(path, TagLib.ReadStyle.None))
            {
                if (!string.IsNullOrWhiteSpace(file.Tag.Title))
                    title = file.Tag.Title;
                if (!string.IsNullOrWhiteSpace(file.Tag.FirstPerformer))
                    artist = file.Tag.FirstPerformer;
                if (file.Tag.Pictures.Length > 0)
                    coverData = file.Tag.Pictures[0].Data.Data;
            }
        }
        catch (Exception)
        {
            // Etiquetas ilegibles: se usa el nombre del archivo y la carátula por defecto.
        }

        return new SongInfo(title, artist, coverData is null ? null : LoadImage(coverData, maxCoverSize));
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
