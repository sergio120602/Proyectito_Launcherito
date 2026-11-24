using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Launcherito;

/// <summary>De dónde viene una canción que no es un .mp3 del PC.</summary>
public enum OnlineSource { Spotify, YouTubeMusic }

/// <summary>
/// Canción de una lista de Spotify o de YouTube Music. Dentro de Launcherito se identifica por
/// <see cref="Key"/>. No tiene .mp3: suena con su vídeo de YouTube (<see cref="YouTubeLinks"/>).
/// </summary>
/// <param name="Id">Id de Spotify o, en YouTube Music, el id del vídeo.</param>
/// <param name="CoverUrl">Portada que da la propia lista (YouTube Music); null si se busca aparte.</param>
public sealed record OnlineTrack(OnlineSource Source, string Id, string Title, string Artists, TimeSpan Duration,
                                 string? CoverUrl = null)
{
    public string Key => OnlineSongs.KeyPrefix(Source) + Id;

    /// <summary>Primer artista ("Drake, Future" → "Drake"), el que se usa para agrupar y para Deezer.</summary>
    public string MainArtist => Artists.Split(',')[0].Trim();
}

/// <summary>Lista o álbum ya leído.</summary>
/// <param name="CoverUrl">Portada de la propia lista, si el servicio la da (Spotify); null si no.</param>
public sealed record OnlineList(OnlineSource Source, string Name, IReadOnlyList<OnlineTrack> Tracks,
                                string? CoverUrl = null);

/// <summary>Enlace de una lista reconocido: de qué servicio es, qué es ("playlist", "album"…) y su id.</summary>
public sealed record ListLink(OnlineSource Source, string Type, string Id);

/// <summary>No se ha podido leer una lista; el mensaje se muestra tal cual.</summary>
public sealed class ListImportException(string message) : Exception(message);

/// <summary>
/// Las canciones de Spotify y YouTube Music que conoce Launcherito, con lo que tienen en común:
/// reconocer sus enlaces, leer sus listas, sus portadas y si ya se tienen en .mp3.
/// </summary>
public static class OnlineSongs
{
    private static readonly ConcurrentDictionary<string, OnlineTrack> Tracks = new();
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcherito");
    private static readonly HttpClient CoverClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string KeyPrefix(OnlineSource source) => source switch
    {
        OnlineSource.Spotify => SpotifyLibrary.KeyPrefix,
        _ => YouTubeMusicLibrary.KeyPrefix,
    };

    /// <summary>Nombre del servicio para los textos ("Spotify", "YouTube Music").</summary>
    public static string Name(OnlineSource source) => source == OnlineSource.Spotify ? "Spotify" : "YouTube Music";

    /// <summary>De qué servicio es la clave, o null si es un .mp3.</summary>
    public static OnlineSource? SourceOf(string key) =>
        SpotifyLibrary.IsSpotify(key) ? OnlineSource.Spotify
        : YouTubeMusicLibrary.IsYouTubeMusic(key) ? OnlineSource.YouTubeMusic
        : null;

    /// <summary>La canción no es un .mp3: viene de Spotify o de YouTube Music y suena con su vídeo.</summary>
    public static bool IsOnline(string key) => SourceOf(key) is not null;

    public static bool TryGet(string key, out OnlineTrack track) => Tracks.TryGetValue(key, out track!);

    /// <summary>Vuelve a dar a conocer una canción guardada («Tus canciones») sin pedirla otra vez.</summary>
    public static void Register(OnlineTrack track) => Tracks[track.Key] = track;

    /// <summary>Reconoce un enlace de una lista o un álbum de Spotify o de YouTube Music; null si no lo es.</summary>
    public static ListLink? ParseLink(string? text) =>
        SpotifyLibrary.ParseLink(text) ?? YouTubeMusicLibrary.ParseLink(text);

    /// <summary>Descarga la lista o el álbum y registra sus canciones.</summary>
    /// <exception cref="ListImportException">No se puede leer (sin conexión, privada o inexistente).</exception>
    public static async Task<OnlineList> LoadAsync(ListLink link)
    {
        var list = link.Source == OnlineSource.Spotify
            ? await SpotifyLibrary.LoadAsync(link.Type, link.Id)
            : await YouTubeMusicLibrary.LoadAsync(link.Type, link.Id);
        foreach (var track in list.Tracks)
            Register(track);
        return list;
    }

    /// <summary>
    /// Bytes de la portada de una canción, o null si no se puede descargar. Se guarda en disco, así
    /// solo se descarga una vez. Bloquea el hilo: llamarlo fuera del hilo de la interfaz.
    /// </summary>
    public static byte[]? GetCoverData(OnlineTrack track) => track.Source == OnlineSource.Spotify
        ? CachedCover("spotify", track.Id, SpotifyLibrary.CoverUrls(track))
        : CachedCover("youtube-music", track.Id, YouTubeMusicLibrary.CoverUrls(track));

    /// <summary>
    /// Portada guardada en %LOCALAPPDATA%\Launcherito\<paramref name="folder"/>, o la primera de
    /// <paramref name="urls"/> que se pueda descargar (se piden una a una, solo si hace falta).
    /// </summary>
    private static byte[]? CachedCover(string folder, string id, IEnumerable<string> urls)
    {
        string directory = Path.Combine(CacheRoot, folder);
        string file = Path.Combine(directory, id + ".jpg");
        try
        {
            if (File.Exists(file))
                return File.ReadAllBytes(file);

            foreach (string url in urls)
            {
                using var response = CoverClient.Send(new HttpRequestMessage(HttpMethod.Get, url));
                if (!response.IsSuccessStatusCode)
                    continue;   // p. ej. un vídeo sin la imagen grande: se prueba la siguiente
                using var memory = new MemoryStream();
                response.Content.ReadAsStream().CopyTo(memory);
                byte[] bytes = memory.ToArray();

                // Se escribe primero en un archivo temporal para no dejar una portada a medias.
                Directory.CreateDirectory(directory);
                string temp = file + ".tmp";
                File.WriteAllBytes(temp, bytes);
                File.Move(temp, file, overwrite: true);
                return bytes;
            }
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or IOException or UnauthorizedAccessException)
        {
            return null;   // sin portada esta vez; se reintentará la próxima vez que se necesite
        }
    }

    /// <summary>
    /// Clave para reconocer la misma canción en Spotify, YouTube Music y un .mp3 aunque se escriba
    /// distinto: sin mayúsculas, tildes, signos ni añadidos como "(feat. X)" o "- Remastered".
    /// </summary>
    public static string MatchKey(string artist, string title)
    {
        string main = artist;
        foreach (var separator in new[] { ",", "&", ";", " feat", " ft.", " x " })
        {
            int cut = main.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (cut > 0)
                main = main[..cut];
        }
        return Simplify(main) + "|" + Simplify(MusicCatalog.CleanTitle(title));
    }

    /// <summary>Solo letras y números, en minúscula y sin tildes.</summary>
    internal static string Simplify(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }
}
