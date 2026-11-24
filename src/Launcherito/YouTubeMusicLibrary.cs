using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launcherito;

/// <summary>
/// Lee listas y álbumes de YouTube Music sin cuenta ni clave, con el mismo servicio que usa su web
/// (music.youtube.com/youtubei/v1/browse): da el título, los artistas, la duración, la portada y el
/// vídeo de cada canción, de 100 en 100. Cada canción suena con su propio vídeo de YouTube (no hace
/// falta buscarlo, como con las de Spotify). Las canciones leídas se guardan en <see cref="OnlineSongs"/>.
/// </summary>
public static class YouTubeMusicLibrary
{
    public const string KeyPrefix = "ytmusic:";

    /// <summary>Como mucho se leen estas canciones de una lista (5 peticiones de 100).</summary>
    public const int MaxTracks = 500;

    /// <summary>Resultados que se muestran de una búsqueda (los que da YouTube Music en la primera página).</summary>
    public const int MaxSearchResults = 20;

    private const string BrowseUrl = "https://music.youtube.com/youtubei/v1/browse?prettyPrint=false";
    private const string SearchUrl = "https://music.youtube.com/youtubei/v1/search?prettyPrint=false";
    // Filtros de la búsqueda de YouTube Music: solo canciones o solo vídeos (los mismos que usa su web).
    private const string SongsFilter = "EgWKAQIIAWoMEA4QChADEAQQCRAF";
    private const string VideosFilter = "EgWKAQIQAWoMEA4QChADEAQQCRAF";
    // Versión del cliente web de YouTube Music con la que se presenta la petición. Si YouTube dejara
    // de aceptarla, basta con poner una más reciente (se ve en las peticiones de music.youtube.com).
    private const string ClientVersion = "1.20241106.01.00";

    private static readonly HttpClient Http = CreateClient();

    // music.youtube.com/playlist?list=ID, www.youtube.com/watch?v=…&list=ID, youtu.be/…?list=ID
    private static readonly Regex ListPattern = new(
        @"(?:youtube\.com|youtu\.be)/\S*?[?&]list=(?<id>[A-Za-z0-9_-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // music.youtube.com/browse/MPREb_… (página de un álbum) o …/browse/VLPL… (página de una lista)
    private static readonly Regex BrowsePattern = new(
        @"music\.youtube\.com/browse/(?<id>MPREb_[A-Za-z0-9_-]+|VL[A-Za-z0-9_-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // "Artista - Canción" en el título de un vídeo
    private static readonly Regex DashPattern = new(@"^(?<left>.+?)\s+[-–—]\s+(?<right>.+)$", RegexOptions.Compiled);
    // "(Official Video)", "[Lyric Video]", "(Videoclip Oficial)"… en el título de un vídeo
    private static readonly Regex VideoTagPattern = new(
        @"\s*[\(\[][^\)\]]*\b(?:official|oficial|video|v[ií]deo|videoclip|audio|lyrics?|letra|visuali[sz]er|mv|hd|hq|4k)\b[^\)\]]*[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Tamaño en la dirección de una portada de Google ("=w120-h120-l90-rj")
    private static readonly Regex ThumbSizePattern = new(@"=w\d+-h\d+", RegexOptions.Compiled);
    // Enlace de un vídeo: youtube.com/watch?v=ID (también music.youtube.com), youtu.be/ID, /shorts/ID, /embed/ID, /live/ID
    private static readonly Regex VideoLinkPattern = new(
        @"(?:youtube\.com/(?:watch\?(?:\S*?&)?v=|shorts/|embed/|live/)|youtu\.be/)(?<id>[A-Za-z0-9_-]{11})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Duración escrita entre los datos de un resultado de búsqueda ("3:58", "1:02:03")
    private static readonly Regex DurationPattern = new(@"^\d{1,2}(?::\d{2}){1,2}$", RegexOptions.Compiled);

    public static bool IsYouTubeMusic(string key) => key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    /// <summary>Id del vídeo de una canción de YouTube Music, o null si la clave es de otra cosa.</summary>
    public static string? VideoId(string key) => IsYouTubeMusic(key) ? key[KeyPrefix.Length..] : null;

    /// <summary>Reconoce el enlace de una lista ("playlist") o un álbum ("album") de YouTube Music, o null.</summary>
    public static ListLink? ParseLink(string? text)
    {
        if (text is null)
            return null;
        var browse = BrowsePattern.Match(text);
        if (browse.Success)
        {
            string id = browse.Groups["id"].Value;
            return id.StartsWith("MPREb_", StringComparison.Ordinal)
                ? new ListLink(OnlineSource.YouTubeMusic, "album", id)
                : new ListLink(OnlineSource.YouTubeMusic, "playlist", id[2..]);   // sin el "VL"
        }
        var list = ListPattern.Match(text);
        return list.Success ? new ListLink(OnlineSource.YouTubeMusic, "playlist", list.Groups["id"].Value) : null;
    }

    /// <summary>Id del vídeo de un enlace de YouTube o YouTube Music, o null si no es el enlace de un vídeo.</summary>
    public static string? VideoIdFromLink(string? text) =>
        text is not null && VideoLinkPattern.Match(text) is { Success: true } match ? match.Groups["id"].Value : null;

    /// <summary>
    /// Busca en YouTube Music: solo canciones (con la portada de su álbum) o solo vídeos (videoclips,
    /// directos, versiones…). Las canciones encontradas no se registran hasta que se añaden.
    /// </summary>
    /// <exception cref="ListImportException">Sin conexión o YouTube Music no responde.</exception>
    public static async Task<IReadOnlyList<OnlineTrack>> SearchAsync(string query, bool videos)
    {
        using var page = await PostAsync(SearchUrl,
            new Dictionary<string, object> { ["query"] = query, ["params"] = videos ? VideosFilter : SongsFilter },
            "No se ha podido buscar en YouTube. Inténtalo otra vez dentro de un rato.",
            "YouTube ha cambiado su forma de responder y no se puede leer la búsqueda.");
        var results = new List<OnlineTrack>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in FindAll(page.RootElement, "musicResponsiveListItemRenderer"))
        {
            if (ReadTrack(item, out _) is { } track && seen.Add(track.Id))
            {
                results.Add(track);
                if (results.Count == MaxSearchResults)
                    break;
            }
        }
        return results;
    }

    /// <summary>
    /// Datos de un vídeo suelto a partir de su id (para un enlace pegado): título y artista sacados
    /// del título del vídeo y de su canal, con el servicio público oEmbed de YouTube.
    /// </summary>
    /// <exception cref="ListImportException">No existe, es privado o no hay conexión.</exception>
    public static async Task<OnlineTrack> LoadVideoAsync(string videoId)
    {
        string url = "https://www.youtube.com/oembed?format=json&url=" +
                     Uri.EscapeDataString("https://www.youtube.com/watch?v=" + videoId);
        try
        {
            using var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new ListImportException("Ese vídeo no existe, es privado o su autor no deja verlo fuera de YouTube.");
            using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string title = StringOf(data.RootElement, "title")
                           ?? throw new ListImportException("No se ha podido leer ese vídeo.");
            string channel = CleanChannel(StringOf(data.RootElement, "author_name") ?? "");
            // Como en los vídeos subidos por cualquiera: "Artista - Canción" manda sobre el nombre del canal.
            var (clean, artists) = CleanVideoTitle(title, channel.Length > 0 ? channel : SongInfo.UnknownArtist,
                                                   uploadedByUser: true);
            return new OnlineTrack(OnlineSource.YouTubeMusic, videoId, clean, artists, TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ListImportException("No se puede conectar con YouTube. Comprueba la conexión a Internet.");
        }
        catch (JsonException)
        {
            throw new ListImportException("YouTube ha cambiado su forma de responder y no se puede leer el vídeo.");
        }
    }

    /// <summary>Imagen pequeña para la lista de resultados: la portada del álbum o la del vídeo.</summary>
    public static string ThumbnailUrl(OnlineTrack track) =>
        track.CoverUrl is { } cover ? ThumbSizePattern.Replace(cover, "=w120-h120")
                                    : $"https://i.ytimg.com/vi/{track.Id}/mqdefault.jpg";

    /// <summary>Nombre del canal como artista: sin " - Topic" (canales automáticos) ni "VEVO".</summary>
    private static string CleanChannel(string channel)
    {
        string name = channel.Trim();
        if (name.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase))
            name = name[..^" - Topic".Length];
        if (name.EndsWith("VEVO", StringComparison.Ordinal) && name.Length > 4)
            name = name[..^4];
        return name.Trim();
    }

    /// <summary>Descarga la lista o el álbum (hasta <see cref="MaxTracks"/> canciones).</summary>
    /// <exception cref="ListImportException">No se puede leer (sin conexión, privada o inexistente).</exception>
    public static async Task<OnlineList> LoadAsync(string type, string id)
    {
        string? name = null;
        if (type == "album")
        {
            // La página del álbum dice cuál es su lista de canciones (OLAK5uy_…), que se lee como cualquier otra.
            using var album = await BrowseAsync("browseId", id, "No se encuentra ese álbum en YouTube Music.");
            name = HeaderTitle(album.RootElement);
            id = FindAll(album.RootElement, "playlistId")
                     .Where(value => value.ValueKind == JsonValueKind.String)
                     .Select(value => value.GetString()!)
                     .FirstOrDefault(value => value.StartsWith("OLAK5uy_", StringComparison.Ordinal))
                 ?? throw new ListImportException("No se encuentra ese álbum en YouTube Music.");
        }

        if (id is "LM" or "LL" or "WL")
            throw new ListImportException(
                "Esa lista es privada de tu cuenta («Me gusta» o «Ver más tarde») y Launcherito no entra en tu cuenta. " +
                "Copia sus canciones a una lista pública o no listada y pega su enlace.");
        if (id.StartsWith("RD", StringComparison.Ordinal) && !id.StartsWith("RDCLAK", StringComparison.Ordinal))
            throw new ListImportException(
                "Los mix de YouTube Music cambian cada vez que se abren y no se pueden añadir. " +
                "Guarda sus canciones en una lista y pega el enlace de esa lista.");

        const string NotFound = "No se encuentra la lista. Comprueba el enlace y que la lista sea pública o no listada.";
        var reader = new TrackReader();
        string? token;
        using (var page = await BrowseAsync("browseId", "VL" + id, NotFound))
        {
            name ??= HeaderTitle(page.RootElement);
            var shelf = FindAll(page.RootElement, "musicPlaylistShelfRenderer").FirstOrDefault();
            if (shelf.ValueKind != JsonValueKind.Object || !shelf.TryGetProperty("contents", out var contents))
                throw new ListImportException(NotFound);
            token = reader.Read(contents);
        }
        // Las siguientes, de 100 en 100.
        while (token is not null && reader.Tracks.Count < MaxTracks)
        {
            using var page = await BrowseAsync("continuation", token, NotFound);
            var items = FindAll(page.RootElement, "continuationItems").FirstOrDefault();
            token = items.ValueKind == JsonValueKind.Array ? reader.Read(items) : null;
        }

        // Un álbum leído por su lista (OLAK5uy_…) no trae cabecera: se usa el álbum de sus canciones.
        name ??= reader.Albums.Count == 1 ? reader.Albums.First() : "Lista de YouTube Music";
        return new OnlineList(OnlineSource.YouTubeMusic, name, reader.Tracks);
    }

    /// <summary>
    /// Portadas que se prueban, en orden: la cuadrada que da la lista (544 px) y, si es un vídeo, su
    /// imagen grande o, si no la tiene, la mediana. El mosaico las recorta al cuadrado.
    /// </summary>
    internal static IEnumerable<string> CoverUrls(OnlineTrack track)
    {
        if (track.CoverUrl is { } url)
            yield return url;
        yield return $"https://i.ytimg.com/vi/{track.Id}/hq720.jpg";
        yield return $"https://i.ytimg.com/vi/{track.Id}/mqdefault.jpg";
    }

    /// <summary>Va leyendo las canciones de las páginas de una lista, sin repetir vídeos.</summary>
    private sealed class TrackReader
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public List<OnlineTrack> Tracks { get; } = new();
        public HashSet<string> Albums { get; } = new(StringComparer.Ordinal);

        /// <summary>Lee las canciones de una página y devuelve la marca para pedir la siguiente (null si no hay más).</summary>
        public string? Read(JsonElement items)
        {
            string? token = null;
            foreach (var entry in items.EnumerateArray())
            {
                if (entry.TryGetProperty("musicResponsiveListItemRenderer", out var item))
                {
                    if (Tracks.Count < MaxTracks && ReadTrack(item, out string? album) is { } track && _seen.Add(track.Id))
                    {
                        Tracks.Add(track);
                        if (album is not null)
                            Albums.Add(album);
                    }
                }
                else if (entry.TryGetProperty("continuationItemRenderer", out var more))
                {
                    token = FindAll(more, "token").Select(StringOf).FirstOrDefault(value => value is not null);
                }
            }
            return token;
        }
    }

    /// <summary>Una fila de la lista; null si no se puede escuchar (vídeo borrado o privado).</summary>
    private static OnlineTrack? ReadTrack(JsonElement item, out string? album)
    {
        album = null;
        var columns = item.TryGetProperty("flexColumns", out var flex) && flex.ValueKind == JsonValueKind.Array
            ? flex.EnumerateArray().Select(Runs).ToList()
            : [];
        if (columns.Count == 0)
            return null;

        string? videoId = item.TryGetProperty("playlistItemData", out var data) ? StringOf(data, "videoId") : null;
        videoId ??= columns[0].SelectMany(run => FindAll(run, "videoId")).Select(StringOf).FirstOrDefault(id => id is not null);
        string title = string.Concat(columns[0].Select(run => StringOf(run, "text"))).Trim();
        if (videoId is null || title.Length == 0)
            return null;

        // Los artistas son los textos que llevan a su página; los separadores (" & ", ", ") no.
        // Lo que va tras " • " (visualizaciones, año) no es del artista.
        var artistRuns = columns.Count > 1 ? columns[1].TakeWhile(run => StringOf(run, "text") != " • ").ToList() : [];
        var linked = artistRuns.Where(run => run.TryGetProperty("navigationEndpoint", out _))
                               .Select(run => StringOf(run, "text")).OfType<string>().ToList();
        string artists = linked.Count > 0 ? string.Join(", ", linked)
            : string.Concat(artistRuns.Select(run => StringOf(run, "text"))).Trim();
        if (artists.Length == 0)
            artists = SongInfo.UnknownArtist;

        if (columns.Count > 2 && string.Concat(columns[2].Select(run => StringOf(run, "text"))).Trim() is { Length: > 0 } albumName)
            album = albumName;

        // "Canción" de YouTube Music (con su portada cuadrada) o vídeo: videoclip oficial (OMV) o subido por alguien (UGC).
        string videoType = FindAll(item, "musicVideoType").Select(StringOf).FirstOrDefault(type => type is not null) ?? "";
        if (videoType != "MUSIC_VIDEO_TYPE_ATV")
            (title, artists) = CleanVideoTitle(title, artists, uploadedByUser: videoType == "MUSIC_VIDEO_TYPE_UGC");

        string? thumbnail = item.TryGetProperty("thumbnail", out var thumb)
            ? FindAll(thumb, "url").Select(StringOf).LastOrDefault(url => url is not null)
            : null;
        // La portada cuadrada de Google se pide a 544 px; la de un vídeo (i.ytimg.com) se busca con su id.
        string? cover = thumbnail is not null && !thumbnail.Contains("ytimg.com", StringComparison.OrdinalIgnoreCase)
            ? ThumbSizePattern.Replace(thumbnail, "=w544-h544")
            : null;

        return new OnlineTrack(OnlineSource.YouTubeMusic, videoId, title, artists, ReadDuration(item, columns), cover);
    }

    /// <summary>
    /// Título de un vídeo como título de canción: "Artista - Canción (Official Video)" → "Canción".
    /// En los subidos por cualquiera el canal no es el artista: se toma el de delante del guion.
    /// </summary>
    private static (string Title, string Artists) CleanVideoTitle(string title, string artists, bool uploadedByUser)
    {
        var dash = DashPattern.Match(title);
        if (dash.Success)
        {
            string left = dash.Groups["left"].Value.Trim();
            string mainArtist = OnlineSongs.Simplify(artists.Split(',')[0]);
            string simpleLeft = OnlineSongs.Simplify(left);
            if (uploadedByUser)
                (artists, title) = (left, dash.Groups["right"].Value);
            else if (mainArtist.Length > 0 && simpleLeft.Contains(mainArtist, StringComparison.Ordinal))
                title = dash.Groups["right"].Value;
        }
        string clean = VideoTagPattern.Replace(title, "").Trim();
        return (clean.Length > 0 ? clean : title.Trim(), artists);
    }

    /// <summary>
    /// Duración ("3:04" o "1:02:03"): en una lista va en la columna de la derecha; en una búsqueda, al
    /// final de la segunda columna ("Artista • Álbum • 3:04"). Cero si no viene.
    /// </summary>
    private static TimeSpan ReadDuration(JsonElement item, List<IEnumerable<JsonElement>> columns)
    {
        string? text = item.TryGetProperty("fixedColumns", out var fixedColumns)
            ? Runs(fixedColumns).Select(run => StringOf(run, "text")).FirstOrDefault(t => t is not null)
            : null;
        text ??= columns.Count > 1
            ? columns[1].Select(run => StringOf(run, "text")?.Trim())
                        .LastOrDefault(t => t is not null && DurationPattern.IsMatch(t))
            : null;
        if (text is null)
            return TimeSpan.Zero;
        long seconds = 0;
        foreach (string part in text.Split(':'))
        {
            if (!int.TryParse(part, out int value))
                return TimeSpan.Zero;
            seconds = seconds * 60 + value;
        }
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Título de la cabecera de la página (nombre de la lista o del álbum), o null.</summary>
    private static string? HeaderTitle(JsonElement root)
    {
        foreach (string header in new[] { "musicResponsiveHeaderRenderer", "musicDetailHeaderRenderer", "musicImmersiveHeaderRenderer" })
        {
            foreach (var found in FindAll(root, header))
            {
                if (found.ValueKind == JsonValueKind.Object && found.TryGetProperty("title", out var title) &&
                    string.Concat(Runs(title).Select(run => StringOf(run, "text"))).Trim() is { Length: > 0 } text)
                    return text;
            }
        }
        return null;
    }

    /// <summary>Pide una página a YouTube Music.</summary>
    private static Task<JsonDocument> BrowseAsync(string field, string value, string notFound) =>
        PostAsync(BrowseUrl, new Dictionary<string, object> { [field] = value }, notFound,
                  "YouTube Music ha cambiado su forma de responder y no se puede leer la lista.");

    /// <summary>Petición al servicio de la web de YouTube Music (browse o search) con los datos de su cliente.</summary>
    private static async Task<JsonDocument> PostAsync(string url, Dictionary<string, object> fields, string failed,
                                                      string unreadable)
    {
        var body = new Dictionary<string, object>
        {
            ["context"] = new { client = new { clientName = "WEB_REMIX", clientVersion = ClientVersion, hl = "es", gl = "ES" } },
        };
        foreach (var (name, value) in fields)
            body[name] = value;
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
                throw new ListImportException(failed);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ListImportException("No se puede conectar con YouTube Music. Comprueba la conexión a Internet.");
        }
        catch (JsonException)
        {
            throw new ListImportException(unreadable);
        }
    }

    /// <summary>Partes ("runs") de un texto de YouTube Music: {"runs":[{"text":"…"},…]}, también dentro de una columna.</summary>
    private static IEnumerable<JsonElement> Runs(JsonElement element) =>
        FindAll(element, "runs").Where(runs => runs.ValueKind == JsonValueKind.Array)
                                .Take(1).SelectMany(runs => runs.EnumerateArray());

    /// <summary>
    /// Todos los valores de la propiedad <paramref name="name"/> a cualquier profundidad, en el orden
    /// del documento. No entra dentro de los que encuentra. Así no depende de la ruta exacta, que
    /// YouTube cambia a menudo.
    /// </summary>
    private static IEnumerable<JsonElement> FindAll(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(name))
                    yield return property.Value;
                else
                    foreach (var found in FindAll(property.Value, name))
                        yield return found;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                foreach (var found in FindAll(child, name))
                    yield return found;
        }
    }

    private static string? StringOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() : null;

    private static string? StringOf(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? StringOf(value) : null;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        client.DefaultRequestHeaders.Add("Origin", "https://music.youtube.com");
        client.DefaultRequestHeaders.Referrer = new Uri("https://music.youtube.com/");
        return client;
    }
}
