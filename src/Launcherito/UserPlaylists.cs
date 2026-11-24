using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcherito;

/// <summary>
/// Playlist hecha por el usuario: un nombre y sus canciones en el orden en que se han añadido.
/// Cada canción es su clave de «Tus canciones» (ruta del .mp3 o "spotify:track:…" / "ytmusic:…").
/// </summary>
public sealed class UserPlaylist
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public List<string> Songs { get; init; } = new();

    /// <summary>Se ve como lista sencilla en lugar de como mosaico de portadas (lo que se eligió la última vez).</summary>
    public bool ListView { get; set; }

    /// <summary>Tamaño elegido para cada portada en su mosaico (celdas de lado); las que no estén, el automático.</summary>
    public Dictionary<string, int> Spans { get; init; } = new();

    /// <summary>La lista fija de Favoritos: no se puede renombrar ni borrar.</summary>
    [JsonIgnore]
    public bool IsFavorites => Id == UserPlaylists.FavoritesId;

    public bool Contains(string key) => Songs.Contains(key, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Favoritos y las playlists del usuario, guardadas en %LOCALAPPDATA%\Launcherito\playlists.json.
/// Cada cambio se escribe al momento. Quitar una canción de una playlist no la borra de «Tus canciones».
/// </summary>
public sealed class UserPlaylists
{
    public const string FavoritesId = "favoritos";
    public const string FavoritesName = "Favoritos";
    public const int MaxNameLength = 60;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcherito", "playlists.json");

    private readonly List<UserPlaylist> _lists;

    public UserPlaylists()
    {
        _lists = Load();
        // Favoritos siempre existe y va la primera.
        var favorites = _lists.FirstOrDefault(list => list.IsFavorites)
                        ?? new UserPlaylist { Id = FavoritesId };
        favorites.Name = FavoritesName;
        _lists.Remove(favorites);
        _lists.Insert(0, favorites);
    }

    public UserPlaylist Favorites => _lists[0];

    /// <summary>Favoritos y después las demás, en el orden en que se crearon.</summary>
    public IReadOnlyList<UserPlaylist> All => _lists;

    public UserPlaylist? Get(string id) => _lists.FirstOrDefault(list => list.Id == id);

    public bool IsFavorite(string key) => Favorites.Contains(key);

    /// <summary>
    /// Comprueba un nombre para una playlist nueva o renombrada. Devuelve el motivo si no vale, o null.
    /// </summary>
    public string? CheckName(string name, string? renamingId = null)
    {
        if (name.Length == 0)
            return "Escribe un nombre para la playlist.";
        if (name.Length > MaxNameLength)
            return $"El nombre es demasiado largo (como mucho {MaxNameLength} letras).";
        if (_lists.Any(list => list.Id != renamingId &&
                               string.Equals(list.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            return $"Ya hay una playlist que se llama «{name}».";
        return null;
    }

    public UserPlaylist Create(string name)
    {
        var list = new UserPlaylist { Id = Guid.NewGuid().ToString("N"), Name = name };
        _lists.Add(list);
        Save();
        return list;
    }

    public void Rename(string id, string name)
    {
        if (Get(id) is { IsFavorites: false } list && list.Name != name)
        {
            list.Name = name;
            Save();
        }
    }

    public void Delete(string id)
    {
        if (_lists.RemoveAll(list => list.Id == id && !list.IsFavorites) > 0)
            Save();
    }

    /// <summary>Añade al final las canciones que aún no están en la playlist. Devuelve cuántas se han añadido.</summary>
    public int Add(string id, IEnumerable<string> keys)
    {
        if (Get(id) is not { } list)
            return 0;
        int added = 0;
        foreach (var key in keys)
        {
            if (!list.Contains(key))
            {
                list.Songs.Add(key);
                added++;
            }
        }
        if (added > 0)
            Save();
        return added;
    }

    /// <summary>Quita canciones de una playlist. Devuelve cuántas se han quitado.</summary>
    public int Remove(string id, IEnumerable<string> keys)
    {
        if (Get(id) is not { } list)
            return 0;
        var gone = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        int removed = list.Songs.RemoveAll(gone.Contains);
        if (removed > 0)
            Save();
        return removed;
    }

    /// <summary>Recuerda si la playlist se ve como lista o como mosaico.</summary>
    public void SetListView(string id, bool listView)
    {
        if (Get(id) is { } list && list.ListView != listView)
        {
            list.ListView = listView;
            Save();
        }
    }

    /// <summary>
    /// Guarda cómo ha quedado su mosaico: el orden de las portadas pasa a ser el de la playlist (las
    /// que no se veían, p. ej. un .mp3 que ya no está, siguen detrás) y se apunta el tamaño de cada una.
    /// </summary>
    public void SaveLayout(string id, IReadOnlyList<string> order, IReadOnlyDictionary<string, int> spans)
    {
        if (Get(id) is not { } list)
            return;
        var shown = new HashSet<string>(order, StringComparer.OrdinalIgnoreCase);
        var songs = order.Where(list.Contains).Concat(list.Songs.Where(key => !shown.Contains(key))).ToList();
        list.Songs.Clear();
        list.Songs.AddRange(songs);
        foreach (var (key, span) in spans)
            list.Spans[key] = span;
        Save();
    }

    /// <summary>Pone o quita una canción de Favoritos. Devuelve si queda como favorita.</summary>
    public bool ToggleFavorite(string key)
    {
        if (IsFavorite(key))
        {
            Remove(FavoritesId, [key]);
            return false;
        }
        Add(FavoritesId, [key]);
        return true;
    }

    /// <summary>Quita de todas las playlists canciones que se han borrado de «Tus canciones».</summary>
    public void Forget(IEnumerable<string> keys)
    {
        var gone = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        int removed = 0;
        foreach (var list in _lists)
        {
            removed += list.Songs.RemoveAll(gone.Contains);
            foreach (var key in list.Spans.Keys.Where(gone.Contains).ToList())
                list.Spans.Remove(key);
        }
        if (removed > 0)
            Save();
    }

    /// <summary>
    /// Cambia una canción por otra en todas las playlists, en el mismo sitio (p. ej. una de Spotify
    /// por el .mp3 que ya se tiene). Si la nueva ya estaba en esa playlist, solo se quita la vieja.
    /// </summary>
    public void Replace(string oldKey, string newKey)
    {
        bool changed = false;
        foreach (var list in _lists)
        {
            int at = list.Songs.FindIndex(key => string.Equals(key, oldKey, StringComparison.OrdinalIgnoreCase));
            if (at < 0)
                continue;
            if (list.Contains(newKey))
                list.Songs.RemoveAt(at);
            else
                list.Songs[at] = newKey;
            // Su portada conserva el tamaño elegido.
            if (list.Spans.Remove(oldKey, out int span))
                list.Spans.TryAdd(newKey, span);
            changed = true;
        }
        if (changed)
            Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            // Primero a un temporal, para no dejar el archivo a medias si algo falla.
            string temp = FilePath + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, _lists, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si no se puede guardar ahora, se vuelve a intentar con el siguiente cambio.
        }
    }

    private static List<UserPlaylist> Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            var lists = JsonSerializer.Deserialize<List<UserPlaylist>>(stream) ?? new();
            // Un archivo editado a mano podría traer ids repetidos o canciones dos veces en la misma lista.
            lists = lists.Where(list => !string.IsNullOrEmpty(list.Id)).DistinctBy(list => list.Id).ToList();
            foreach (var list in lists)
            {
                var unique = list.Songs.Where(key => !string.IsNullOrEmpty(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                list.Songs.Clear();
                list.Songs.AddRange(unique);
            }
            return lists;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();   // primera vez (aún no hay archivo) o archivo dañado
        }
    }
}
