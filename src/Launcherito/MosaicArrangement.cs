using System.IO;
using System.Text.Json;

namespace Launcherito;

/// <summary>
/// Orden y tamaño de las portadas que el usuario ha elegido en el mosaico, guardados en disco para
/// que se mantengan al cerrar el programa. Mientras no se toque nada, el mosaico va en orden
/// alfabético con los tamaños automáticos.
/// </summary>
public sealed class MosaicArrangement
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcherito", "mosaico.json");

    private List<string> _order;
    private readonly Dictionary<string, int> _spans = new(StringComparer.OrdinalIgnoreCase);

    public MosaicArrangement()
    {
        var data = Load();
        _order = data.Order;
        foreach (var (path, span) in data.Spans)
            _spans[path] = span;
    }

    /// <summary>
    /// Ordena las canciones, que llegan en orden alfabético: primero las ya colocadas, en el orden
    /// elegido; las nuevas, al final y entre ellas por orden alfabético.
    /// </summary>
    public List<string> Arrange(List<string> songs)
    {
        if (_order.Count == 0)
            return songs;
        var position = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _order.Count; i++)
            position.TryAdd(_order[i], i);
        return songs.OrderBy(song => position.TryGetValue(song, out int at) ? at : int.MaxValue).ToList();
    }

    /// <summary>Tamaño elegido para la canción (en celdas de lado), o null si no se ha guardado ninguno.</summary>
    public int? SpanOf(string path) => _spans.TryGetValue(path, out int span) ? span : null;

    /// <summary>Guarda el orden y el tamaño de todas las portadas tal como están ahora.</summary>
    public void Remember(IReadOnlyList<MosaicTile> tiles)
    {
        var shown = new HashSet<string>(tiles.Select(tile => tile.SongPath), StringComparer.OrdinalIgnoreCase);
        // Las que ahora no están (una lista de Spotify quitada, p. ej.) conservan su orden detrás.
        _order = tiles.Select(tile => tile.SongPath).Concat(_order.Where(path => !shown.Contains(path))).ToList();
        foreach (var tile in tiles)
            _spans[tile.SongPath] = tile.BaseSpan;
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
                JsonSerializer.Serialize(stream, new Data { Order = _order, Spans = new(_spans) });
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si no se puede guardar, el orden se mantiene mientras el programa siga abierto.
        }
    }

    private static Data Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize<Data>(stream) ?? new Data();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Data();
        }
    }

    private sealed class Data
    {
        public List<string> Order { get; set; } = new();               // rutas, en el orden del mosaico
        public Dictionary<string, int> Spans { get; set; } = new();    // ruta → celdas de lado
    }
}
