using System.IO;
using System.Text.Json;

namespace Launcherito;

/// <summary>
/// Canción guardada. <see cref="Key"/> es la ruta del .mp3 o la clave de Spotify ("spotify:track:…").
/// De las de Spotify se guardan todos sus datos, porque sin ellos no se pueden volver a mostrar.
/// </summary>
public sealed class SavedSong
{
    public required string Key { get; init; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public long DurationMs { get; init; }   // solo Spotify

    public bool IsSpotify => SpotifyLibrary.IsSpotify(Key);

    public static SavedSong FromTrack(SpotifyTrack track) => new()
    {
        Key = track.Key,
        Title = track.Title,
        Artist = track.Artists,
        DurationMs = (long)track.Duration.TotalMilliseconds,
    };

    public SpotifyTrack ToTrack() =>
        new(Key[SpotifyLibrary.KeyPrefix.Length..], Title, Artist, TimeSpan.FromMilliseconds(DurationMs));
}

/// <summary>
/// Las canciones que se han cargado alguna vez («Tus canciones»), guardadas en
/// %LOCALAPPDATA%\Launcherito\canciones.json para que sigan ahí al volver a abrir el programa.
/// Borrar una solo la quita de aquí: el archivo .mp3 no se toca.
/// </summary>
public sealed class SongLibrary
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcherito", "canciones.json");

    private readonly Dictionary<string, SavedSong> _songs = new(StringComparer.OrdinalIgnoreCase);
    private bool _dirty;

    public SongLibrary()
    {
        foreach (var song in Load())
            _songs.TryAdd(song.Key, song);
    }

    public int Count => _songs.Count;

    /// <summary>Todas, por orden alfabético de título.</summary>
    public IReadOnlyList<SavedSong> Songs =>
        _songs.Values.OrderBy(song => song.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

    public bool Contains(string key) => _songs.ContainsKey(key);

    /// <summary>Guarda las canciones nuevas (las que ya estaban se dejan como están).</summary>
    public void Add(IEnumerable<SavedSong> songs)
    {
        foreach (var song in songs)
            _dirty |= _songs.TryAdd(song.Key, song);
        Save();
    }

    /// <summary>Apunta el título y el artista de un .mp3 en cuanto se leen sus etiquetas. Se escribe con <see cref="Save"/>.</summary>
    public void Describe(string key, string title, string artist)
    {
        if (_songs.TryGetValue(key, out var song) && (song.Title != title || song.Artist != artist))
        {
            song.Title = title;
            song.Artist = artist;
            _dirty = true;
        }
    }

    public void Remove(IEnumerable<string> keys)
    {
        foreach (var key in keys)
            _dirty |= _songs.Remove(key);
        Save();
    }

    /// <summary>Escribe el archivo si algo ha cambiado.</summary>
    public void Save()
    {
        if (!_dirty)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            // Primero a un temporal, para no dejar el archivo a medias si algo falla.
            string temp = FilePath + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, _songs.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.Move(temp, FilePath, overwrite: true);
            _dirty = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si no se puede guardar ahora, se reintenta con el siguiente cambio.
        }
    }

    private static List<SavedSong> Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize<List<SavedSong>>(stream) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();   // primera vez (aún no hay archivo) o archivo dañado
        }
    }
}
