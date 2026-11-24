using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launcherito;

/// <summary>
/// Lee listas públicas y álbumes de Spotify sin cuenta ni clave. La API oficial exige desde febrero
/// de 2026 que el dueño de la app tenga Premium, así que se usa la página que Spotify ofrece para
/// insertar listas en otras webs (trae título y artistas de cada canción, hasta 100 canciones) y su
/// servicio oEmbed para las portadas. Spotify no da el audio completo: si no se tiene el .mp3, la
/// canción suena con su vídeo de YouTube (<see cref="YouTubeLinks"/>). Las canciones leídas se
/// guardan en <see cref="OnlineSongs"/>.
/// </summary>
public static class SpotifyLibrary
{
    public const string KeyPrefix = "spotify:track:";

    private static readonly HttpClient Http = CreateClient();

    // open.spotify.com/playlist/ID, open.spotify.com/intl-es/album/ID?si=…, spotify:playlist:ID
    private static readonly Regex LinkPattern = new(
        @"(?:open\.spotify\.com/(?:intl-[a-z-]+/)?|spotify:)(?<type>playlist|album)[/:](?<id>[A-Za-z0-9]{22})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DataPattern = new(
        "<script id=\"__NEXT_DATA__\" type=\"application/json\">(?<json>.*?)</script>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static bool IsSpotify(string key) => key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    /// <summary>Saca el tipo ("playlist" o "album") y el id de un enlace de Spotify, o null si no lo es.</summary>
    public static ListLink? ParseLink(string? text)
    {
        var match = text is null ? null : LinkPattern.Match(text);
        return match is { Success: true }
            ? new ListLink(OnlineSource.Spotify, match.Groups["type"].Value.ToLowerInvariant(), match.Groups["id"].Value)
            : null;
    }

    /// <summary>Descarga la lista o el álbum.</summary>
    /// <exception cref="ListImportException">No se puede leer (sin conexión, privada o inexistente).</exception>
    public static async Task<OnlineList> LoadAsync(string type, string id)
    {
        string html;
        try
        {
            html = await Http.GetStringAsync($"https://open.spotify.com/embed/{type}/{id}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ListImportException("No se puede conectar con Spotify. Comprueba la conexión a Internet.");
        }

        var data = DataPattern.Match(html);
        if (!data.Success)
            throw new ListImportException("Spotify ha cambiado su página y no se puede leer la lista.");

        try
        {
            using var json = JsonDocument.Parse(data.Groups["json"].Value);
            if (!json.RootElement.TryGetProperty("props", out var props) ||
                !props.TryGetProperty("pageProps", out var page) ||
                !page.TryGetProperty("state", out var state) ||
                !state.TryGetProperty("data", out var stateData) ||
                !stateData.TryGetProperty("entity", out var entity) ||
                !entity.TryGetProperty("trackList", out var list) || list.ValueKind != JsonValueKind.Array)
                throw new ListImportException(type == "album"
                    ? "No se encuentra ese álbum en Spotify."
                    : "No se encuentra la lista. Comprueba el enlace y que la lista sea pública.");

            var tracks = new List<OnlineTrack>();
            foreach (var item in list.EnumerateArray())
            {
                string? uri = Text(item, "uri");
                if (uri is null || !uri.StartsWith(KeyPrefix, StringComparison.Ordinal))
                    continue;   // episodios de pódcast u otros elementos que no son canciones
                tracks.Add(new OnlineTrack(
                    OnlineSource.Spotify,
                    uri[KeyPrefix.Length..],
                    Text(item, "title") ?? "Sin título",
                    Text(item, "subtitle") is { Length: > 0 } artists ? artists : SongInfo.UnknownArtist,
                    TimeSpan.FromMilliseconds(item.TryGetProperty("duration", out var ms) && ms.TryGetInt64(out long d) ? d : 0)));
            }
            return new OnlineList(OnlineSource.Spotify, Text(entity, "name") ?? "Lista de Spotify", tracks,
                                  LargestCover(entity));
        }
        catch (JsonException)
        {
            throw new ListImportException("Spotify ha cambiado su página y no se puede leer la lista.");
        }
    }

    /// <summary>
    /// Dirección de la portada (640 px) de una canción. Se pregunta a Spotify solo cuando se recorre,
    /// es decir, si la portada no está ya guardada en disco. Bloquea el hilo.
    /// </summary>
    internal static IEnumerable<string> CoverUrls(OnlineTrack track)
    {
        // oEmbed es el servicio público de Spotify para mostrar una canción en otra web; da la
        // portada a 300 px. Cambiando el código de tamaño de la URL se obtiene la de 640 px.
        using var oembed = JsonDocument.Parse(GetString(
            $"https://open.spotify.com/oembed?url={Uri.EscapeDataString("https://open.spotify.com/track/" + track.Id)}"));
        if (Text(oembed.RootElement, "thumbnail_url") is { } url)
            yield return url.Replace("ab67616d00001e02", "ab67616d0000b273");
    }

    private static string GetString(string url)
    {
        using var response = Http.Send(new HttpRequestMessage(HttpMethod.Get, url));
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(response.Content.ReadAsStream());
        return reader.ReadToEnd();
    }

    /// <summary>La portada más grande de la lista ("coverArt": {"sources": [{url, width}, …]}), o null.</summary>
    private static string? LargestCover(JsonElement entity)
    {
        if (!entity.TryGetProperty("coverArt", out var art) || !art.TryGetProperty("sources", out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
            return null;
        return sources.EnumerateArray()
            .Where(source => Text(source, "url") is not null)
            // El ancho puede venir vacío (null): entonces cuenta como 0.
            .OrderByDescending(source => source.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number &&
                                         w.TryGetInt32(out int width) ? width : 0)
            .Select(source => Text(source, "url"))
            .FirstOrDefault();
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Launcherito");
        return client;
    }
}
