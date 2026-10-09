using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Launcherito;

/// <summary>Género asignado a una canción, con la foto que lo representa.</summary>
public sealed record GenreInfo(string Name, string? Picture);

/// <summary>
/// Clasifica canciones con la API pública de Deezer (no necesita clave): busca la canción, mira
/// el género de su álbum y obtiene fotos de artistas y géneros. Todo se guarda en una caché en
/// disco, así cada canción solo se consulta una vez aunque se cierre el programa.
/// </summary>
public sealed class MusicCatalog
{
    private const string Api = "https://api.deezer.com";
    // Deezer admite 50 peticiones cada 5 s; con 120 ms entre peticiones se queda por debajo.
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(120);

    private static readonly HttpClient Http = CreateClient();
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcherito", "catalogo.json");

    private readonly SemaphoreSlim _gate = new(1);
    private DateTime _lastRequest = DateTime.MinValue;
    private readonly CacheData _cache;
    private bool _dirty;

    public MusicCatalog()
    {
        _cache = LoadCache();
    }

    /// <summary>Busca la canción en Deezer y devuelve el género de su álbum, o null si no se encuentra.</summary>
    /// <exception cref="CatalogUnavailableException">No hay conexión con Deezer.</exception>
    public async Task<GenreInfo?> GetGenreAsync(string artist, string title)
    {
        string key = $"{Normalize(artist)}|{Normalize(title)}";
        if (!_cache.Tracks.TryGetValue(key, out long? albumId))
        {
            albumId = await SearchAlbumIdAsync(artist, CleanTitle(title));
            _cache.Tracks[key] = albumId;
            _dirty = true;
        }
        if (albumId is not { } album)
            return null;

        if (!_cache.Albums.TryGetValue(album, out long? genreId))
        {
            genreId = await GetAlbumGenreIdAsync(album);
            _cache.Albums[album] = genreId;
            _dirty = true;
        }
        return genreId is { } id ? await GetGenreInfoAsync(id) : null;
    }

    /// <summary>URL de la foto de un artista, o null si Deezer no lo conoce.</summary>
    public async Task<string?> GetArtistPictureAsync(string artist)
    {
        string key = Normalize(artist);
        if (_cache.ArtistPictures.TryGetValue(key, out var picture))
            return picture;

        using var json = await GetJsonAsync($"{Api}/search/artist?limit=1&q={Uri.EscapeDataString(artist)}");
        if (json is null)
            return null;
        picture = FirstData(json.RootElement) is { } found && found.TryGetProperty("picture_big", out var url)
            ? url.GetString()
            : null;
        _cache.ArtistPictures[key] = picture;
        _dirty = true;
        return picture;
    }

    /// <summary>Guarda la caché en disco si ha cambiado.</summary>
    public void Save()
    {
        if (!_dirty)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            // Se escribe primero en un archivo temporal para no dejar la caché a medias si algo falla.
            string temp = CachePath + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, _cache);
            File.Move(temp, CachePath, overwrite: true);
            _dirty = false;
        }
        catch (IOException)
        {
            // Si no se puede guardar, se volverá a consultar la próxima vez.
        }
    }

    private async Task<long?> SearchAlbumIdAsync(string artist, string title)
    {
        // Primero una búsqueda exacta por artista y título; si no da nada, una búsqueda libre.
        foreach (var query in new[] { $"artist:\"{artist}\" track:\"{title}\"", $"{artist} {title}" })
        {
            using var json = await GetJsonAsync($"{Api}/search?limit=1&q={Uri.EscapeDataString(query)}")
                ?? throw new CatalogUnavailableException();
            if (FirstData(json.RootElement) is { } track &&
                track.TryGetProperty("album", out var album) && album.TryGetProperty("id", out var id))
                return id.GetInt64();
        }
        return null;
    }

    private async Task<long?> GetAlbumGenreIdAsync(long albumId)
    {
        using var json = await GetJsonAsync($"{Api}/album/{albumId}")
            ?? throw new CatalogUnavailableException();

        // Un álbum puede tener varios géneros ("Pop", "Pop latino", "Latino"); "Pop" es el que Deezer
        // pone a casi todo, así que se prefiere el primero que sea más concreto.
        long? chosen = null;
        if (json.RootElement.TryGetProperty("genres", out var genres) && genres.TryGetProperty("data", out var list))
        {
            foreach (var genre in list.EnumerateArray())
            {
                long id = genre.GetProperty("id").GetInt64();
                chosen ??= id;
                if (id != PopGenreId)
                {
                    chosen = id;
                    break;
                }
            }
        }
        if (chosen is null && json.RootElement.TryGetProperty("genre_id", out var main) && main.GetInt64() > 0)
            chosen = main.GetInt64();
        return chosen;
    }

    private const long PopGenreId = 132;

    private async Task<GenreInfo?> GetGenreInfoAsync(long genreId)
    {
        if (_cache.Genres.TryGetValue(genreId, out var cached))
            return cached;

        using var json = await GetJsonAsync($"{Api}/genre/{genreId}")
            ?? throw new CatalogUnavailableException();
        if (!json.RootElement.TryGetProperty("name", out var name))
            return null;
        var info = new GenreInfo(
            name.GetString() ?? "Desconocido",
            json.RootElement.TryGetProperty("picture_big", out var picture) ? picture.GetString() : null);
        _cache.Genres[genreId] = info;
        _dirty = true;
        return info;
    }

    /// <summary>Hace una petición respetando el límite de Deezer. Devuelve null si falla la red.</summary>
    private async Task<JsonDocument?> GetJsonAsync(string url)
    {
        await _gate.WaitAsync();
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var wait = _lastRequest + MinInterval - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait);
                _lastRequest = DateTime.UtcNow;

                // using: la respuesta HTTP (y su conexión) se liberan al salir, igual que un try-with-resources.
                using var response = await Http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;
                await using var stream = await response.Content.ReadAsStreamAsync();
                var json = await JsonDocument.ParseAsync(stream);

                // Si se supera el límite, Deezer responde {"error":{"code":4,...}}: se espera y se reintenta.
                if (json.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("code", out var code) && code.GetInt32() == 4)
                {
                    json.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    continue;
                }
                return json;
            }
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static JsonElement? FirstData(JsonElement root) =>
        root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
            ? data[0]
            : null;

    /// <summary>Quita añadidos como "- Remastered 2005" o "(feat. X)" que estorban en la búsqueda.</summary>
    private static string CleanTitle(string title)
    {
        int dash = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
            title = title[..dash];
        int paren = title.IndexOfAny(['(', '[']);
        if (paren > 0)
            title = title[..paren];
        return title.Trim();
    }

    private static string Normalize(string text) => text.Trim().ToLowerInvariant();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es");   // nombres de géneros en español
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Launcherito");
        return client;
    }

    private static CacheData LoadCache()
    {
        try
        {
            using var stream = File.OpenRead(CachePath);
            return JsonSerializer.Deserialize<CacheData>(stream) ?? new CacheData();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new CacheData();
        }
    }

    private sealed class CacheData
    {
        public Dictionary<string, long?> Tracks { get; set; } = new();       // "artista|título" → id de álbum
        public Dictionary<long, long?> Albums { get; set; } = new();         // id de álbum → id de género
        public Dictionary<long, GenreInfo> Genres { get; set; } = new();     // id de género → nombre y foto
        public Dictionary<string, string?> ArtistPictures { get; set; } = new();
    }
}

/// <summary>Deezer no responde (sin conexión, caído o bloqueado). Lo ya consultado no se pierde.</summary>
public sealed class CatalogUnavailableException() : Exception("No se puede conectar con Deezer.");
