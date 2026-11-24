using System.IO;
using System.Text.Json;

namespace Launcherito;

/// <summary>Una de las playlists del momento de Spotify, tal como se leyó la última vez.</summary>
public sealed class TrendingList
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public string? CoverUrl { get; set; }

    /// <summary>Sus canciones, en el orden de Spotify (en un Top 50, su puesto).</summary>
    public List<SavedSong> Tracks { get; set; } = new();
}

/// <summary>
/// «Del momento»: las playlists más escuchadas de Spotify (Top 50, Éxitos…), que se leen cada día a
/// las 7:00. La información (canciones, artistas, portadas) es la de Spotify; la música suena con el
/// vídeo de YouTube de cada canción, como las demás de Spotify. Se guardan en
/// %LOCALAPPDATA%\Launcherito\del-momento.json, así se ven aunque no haya conexión.
/// <para>
/// Las actualiza la tarea programada de Windows «Launcherito - Playlists del momento», que abre
/// Launcherito.exe con <see cref="RefreshArgument"/> (sin ventana), y también la propia aplicación
/// si al abrirla o mientras está abierta ya ha pasado la hora y aún no se han leído.
/// </para>
/// </summary>
public sealed class TrendingPlaylists
{
    /// <summary>Argumento con el que la tarea programada abre Launcherito solo para actualizarlas.</summary>
    public const string RefreshArgument = "--actualizar-del-momento";

    /// <summary>Hora del día a la que se renuevan.</summary>
    public const int RefreshHour = 7;

    /// <summary>
    /// Las playlists que se siguen (id de Spotify y nombre por si aún no se han leído). Son listas
    /// de la propia Spotify que se renuevan solas: los Top 50 cada día, las demás cada semana.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Name)> Sources =
    [
        ("37i9dQZEVXbNFJfN1Vw8d9", "Top 50 - España"),
        ("37i9dQZEVXbMDoHDwVN2tF", "Top 50 - Global"),
        ("37i9dQZF1DXaxEKcoCdWHD", "Éxitos España"),
        ("37i9dQZF1DXcBWIGoYBM5M", "Today's Top Hits"),
        ("37i9dQZF1DX10zKzsJ2jva", "Viva Latino"),
        ("37i9dQZF1DWY7IeIP1cdjF", "Baila Reggaeton"),
        ("37i9dQZF1DWUa8ZRTfalHk", "Pop Rising"),
        ("37i9dQZF1DX0XUsuxWHRQd", "RapCaviar"),
    ];

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcherito");
    public static readonly string FilePath = Path.Combine(Folder, "del-momento.json");
    private static readonly string LogPath = Path.Combine(Folder, "del-momento.log");

    /// <summary>Cuándo se leyeron por última vez (hora local), o null si nunca.</summary>
    public DateTime? Updated { get; set; }

    public List<TrendingList> Lists { get; set; } = new();

    public TrendingList? Get(string id) => Lists.FirstOrDefault(list => list.Id == id);

    /// <summary>La última vez que tocaba renovarlas: hoy a las 7:00, o ayer si aún no son las 7.</summary>
    public static DateTime LastRefreshTime(DateTime now)
    {
        var today = now.Date.AddHours(RefreshHour);
        return now >= today ? today : today.AddDays(-1);
    }

    /// <summary>Ya ha pasado la hora de renovarlas y no se ha hecho (o nunca se han leído).</summary>
    public bool IsStale(DateTime now) => Updated is not { } updated || updated < LastRefreshTime(now);

    /// <summary>Lo guardado en disco, o vacío si no hay nada (o está dañado).</summary>
    public static TrendingPlaylists Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize<TrendingPlaylists>(stream) ?? new TrendingPlaylists();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new TrendingPlaylists();
        }
    }

    /// <summary>
    /// Vuelve a leer todas de Spotify y lo guarda. Si alguna no se puede leer se queda la de la vez
    /// anterior; si no se puede leer ninguna (sin conexión), no se toca nada y se lanza la excepción.
    /// </summary>
    /// <exception cref="ListImportException">No se ha podido leer ninguna.</exception>
    public static async Task<TrendingPlaylists> RefreshAsync(TrendingPlaylists previous)
    {
        var lists = new List<TrendingList>();
        int read = 0;
        string? lastError = null;
        foreach (var (id, name) in Sources)
        {
            try
            {
                var list = await SpotifyLibrary.LoadAsync("playlist", id);
                if (list.Tracks.Count == 0)
                    throw new ListImportException("La lista ha llegado vacía.");
                lists.Add(new TrendingList
                {
                    Id = id,
                    Name = list.Name,
                    CoverUrl = list.CoverUrl,
                    Tracks = list.Tracks.Select(SavedSong.FromTrack).ToList(),
                });
                read++;
            }
            catch (Exception ex)
            {
                // Cualquier fallo con una lista (sin conexión, Spotify ha cambiado algo…) no debe
                // impedir las demás ni cerrar el programa: esa se queda como estaba.
                lastError = ex is ListImportException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
                if (previous.Get(id) is { } old)
                    lists.Add(old);   // mejor la de ayer que nada
                else
                    lists.Add(new TrendingList { Id = id, Name = name });
            }
        }

        if (read == 0)
        {
            Log($"No se ha podido leer ninguna playlist: {lastError}");
            throw new ListImportException(lastError ?? "No se ha podido leer ninguna playlist de Spotify.");
        }

        var fresh = new TrendingPlaylists { Updated = DateTime.Now, Lists = lists };
        fresh.Save();
        Log(read == Sources.Count
            ? $"Actualizadas las {read} playlists."
            : $"Actualizadas {read} de {Sources.Count} playlists (las demás se quedan como estaban): {lastError}");
        return fresh;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            // Primero a un temporal, para no dejar el archivo a medias (la aplicación puede estar leyéndolo).
            string temp = FilePath + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log($"No se ha podido guardar: {ex.Message}");
        }
    }

    /// <summary>Una línea en del-momento.log, para saber qué pasó con la tarea de las 7:00.</summary>
    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            // Se guardan solo las últimas líneas para que el archivo no crezca sin fin.
            var lines = File.Exists(LogPath) ? File.ReadAllLines(LogPath).TakeLast(199).ToList() : new List<string>();
            lines.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}");
            File.WriteAllLines(LogPath, lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sin registro: no es importante.
        }
    }
}
