using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launcherito;

/// <summary>Canción de una lista de Spotify. Dentro de Launcherito se identifica por <see cref="Key"/>.</summary>
public sealed record SpotifyTrack(string Id, string Title, string Artists, TimeSpan Duration, string? PreviewUrl)
{
    public string Key => SpotifyLibrary.KeyPrefix + Id;

    /// <summary>Primer artista ("Drake, Future" → "Drake"), el que se usa para agrupar y para Deezer.</summary>
    public string MainArtist => Artists.Split(',')[0].Trim();
}

/// <summary>Lista o álbum de Spotify ya leído.</summary>
public sealed record SpotifyList(string Name, string Owner, IReadOnlyList<SpotifyTrack> Tracks);

/// <summary>
/// Lee listas públicas y álbumes de Spotify sin cuenta ni clave. La API oficial exige desde febrero
/// de 2026 que el dueño de la app tenga Premium, así que se usa la página que Spotify ofrece para
/// insertar listas en otras webs (trae título, artistas y un fragmento de 30 s de cada canción, hasta
/// 100 canciones) y su servicio oEmbed para las portadas. Spotify no da el audio completo: si no se
/// tiene el .mp3, suena el fragmento.
/// </summary>
public static class SpotifyLibrary
{
    public const string KeyPrefix = "spotify:track:";

    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, SpotifyTrack> Tracks = new();
    private static readonly string CoverFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcherito", "spotify");

    // open.spotify.com/playlist/ID, open.spotify.com/intl-es/album/ID?si=…, spotify:playlist:ID
    private static readonly Regex LinkPattern = new(
        @"(?:open\.spotify\.com/(?:intl-[a-z-]+/)?|spotify:)(?<type>playlist|album)[/:](?<id>[A-Za-z0-9]{22})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DataPattern = new(
        "<script id=\"__NEXT_DATA__\" type=\"application/json\">(?<json>.*?)</script>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static bool IsSpotify(string key) => key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    public static bool TryGet(string key, out SpotifyTrack track) => Tracks.TryGetValue(key, out track!);

    /// <summary>Saca el tipo ("playlist" o "album") y el id de un enlace de Spotify, o null si no lo es.</summary>
    public static (string Type, string Id)? ParseLink(string? text)
    {
        var match = text is null ? null : LinkPattern.Match(text);
        return match is { Success: true }
            ? (match.Groups["type"].Value.ToLowerInvariant(), match.Groups["id"].Value)
            : null;
    }

    /// <summary>Descarga la lista o el álbum y registra sus canciones.</summary>
    /// <exception cref="SpotifyImportException">No se puede leer (sin conexión, privada o inexistente).</exception>
    public static async Task<SpotifyList> LoadAsync(string type, string id)
    {
        string html;
        try
        {
            html = await Http.GetStringAsync($"https://open.spotify.com/embed/{type}/{id}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new SpotifyImportException("No se puede conectar con Spotify. Comprueba la conexión a Internet.");
        }

        var data = DataPattern.Match(html);
        if (!data.Success)
            throw new SpotifyImportException("Spotify ha cambiado su página y no se puede leer la lista.");

        try
        {
            using var json = JsonDocument.Parse(data.Groups["json"].Value);
            if (!json.RootElement.TryGetProperty("props", out var props) ||
                !props.TryGetProperty("pageProps", out var page) ||
                !page.TryGetProperty("state", out var state) ||
                !state.TryGetProperty("data", out var stateData) ||
                !stateData.TryGetProperty("entity", out var entity) ||
                !entity.TryGetProperty("trackList", out var list) || list.ValueKind != JsonValueKind.Array)
                throw new SpotifyImportException(type == "album"
                    ? "No se encuentra ese álbum en Spotify."
                    : "No se encuentra la lista. Comprueba el enlace y que la lista sea pública.");

            var tracks = new List<SpotifyTrack>();
            foreach (var item in list.EnumerateArray())
            {
                string? uri = Text(item, "uri");
                if (uri is null || !uri.StartsWith(KeyPrefix, StringComparison.Ordinal))
                    continue;   // episodios de pódcast u otros elementos que no son canciones
                var track = new SpotifyTrack(
                    uri[KeyPrefix.Length..],
                    Text(item, "title") ?? "Sin título",
                    Text(item, "subtitle") is { Length: > 0 } artists ? artists : SongInfo.UnknownArtist,
                    TimeSpan.FromMilliseconds(item.TryGetProperty("duration", out var ms) && ms.TryGetInt64(out long d) ? d : 0),
                    item.TryGetProperty("audioPreview", out var preview) && preview.ValueKind == JsonValueKind.Object
                        ? Text(preview, "url")
                        : null);
                Tracks[track.Key] = track;
                tracks.Add(track);
            }
            return new SpotifyList(Text(entity, "name") ?? "Lista de Spotify", Text(entity, "subtitle") ?? "", tracks);
        }
        catch (JsonException)
        {
            throw new SpotifyImportException("Spotify ha cambiado su página y no se puede leer la lista.");
        }
    }

    /// <summary>
    /// Bytes de la portada (640 px) de una canción, o null si no se puede descargar. Se guarda en disco,
    /// así solo se descarga una vez. Bloquea el hilo: llamarlo fuera del hilo de la interfaz.
    /// </summary>
    public static byte[]? GetCoverData(SpotifyTrack track)
    {
        string file = Path.Combine(CoverFolder, track.Id + ".jpg");
        try
        {
            if (File.Exists(file))
                return File.ReadAllBytes(file);

            // oEmbed es el servicio público de Spotify para mostrar una canción en otra web; da la
            // portada a 300 px. Cambiando el código de tamaño de la URL se obtiene la de 640 px.
            using var oembed = JsonDocument.Parse(GetString(
                $"https://open.spotify.com/oembed?url={Uri.EscapeDataString("https://open.spotify.com/track/" + track.Id)}"));
            if (Text(oembed.RootElement, "thumbnail_url") is not { } url)
                return null;
            url = url.Replace("ab67616d00001e02", "ab67616d0000b273");

            using var response = Http.Send(new HttpRequestMessage(HttpMethod.Get, url));
            response.EnsureSuccessStatusCode();
            using var memory = new MemoryStream();
            response.Content.ReadAsStream().CopyTo(memory);
            byte[] bytes = memory.ToArray();

            // Se escribe primero en un archivo temporal para no dejar una portada a medias.
            Directory.CreateDirectory(CoverFolder);
            string temp = file + ".tmp";
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, file, overwrite: true);
            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or IOException or UnauthorizedAccessException)
        {
            return null;   // sin portada esta vez; se reintentará la próxima vez que se necesite
        }
    }

    /// <summary>
    /// Clave para reconocer la misma canción en Spotify y en un .mp3 aunque se escriba distinto:
    /// sin mayúsculas, tildes, signos ni añadidos como "(feat. X)" o "- Remastered".
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

    private static string Simplify(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    private static string GetString(string url)
    {
        using var response = Http.Send(new HttpRequestMessage(HttpMethod.Get, url));
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(response.Content.ReadAsStream());
        return reader.ReadToEnd();
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

/// <summary>No se ha podido leer una lista de Spotify; el mensaje se muestra tal cual.</summary>
public sealed class SpotifyImportException(string message) : Exception(message);
