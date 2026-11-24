using System.IO;

namespace Launcherito;

/// <summary>
/// Lista de reproducción. Guarda las canciones en orden alfabético y mantiene aparte
/// el orden en que se reproducen, que puede ser alfabético o aleatorio.
/// </summary>
public sealed class Playlist
{
    private static readonly StringComparer NameComparer = StringComparer.CurrentCultureIgnoreCase;

    private readonly List<string> _songs = new();                       // rutas en orden alfabético
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();
    private List<int> _order = new();                                   // índices de _songs en orden de reproducción
    private int _position = -1;                                         // posición actual dentro de _order
    private bool _shuffle = true;                                       // aleatorio activado por defecto
    private Dictionary<string, int>? _rank;                             // orden propio (el de una playlist del usuario)

    public int Count => _songs.Count;

    /// <summary>Sin aleatorio se sigue el orden de una playlist del usuario en lugar del alfabético.</summary>
    public bool HasCustomOrder => _rank is not null;

    /// <summary>Todas las canciones en orden alfabético (el orden del mosaico).</summary>
    public IReadOnlyList<string> Songs => _songs;

    /// <summary>Posición (empezando en 0) de la canción actual en el orden de reproducción.</summary>
    public int Position => _position;

    public string? Current => _position < 0 ? null : _songs[_order[_position]];

    /// <summary>
    /// true: las canciones se reproducen desordenadas. false: se reproducen en orden alfabético.
    /// Al cambiarlo, la canción actual sigue sonando y solo cambia cuál va después.
    /// </summary>
    public bool Shuffle
    {
        get => _shuffle;
        set
        {
            if (_shuffle == value)
                return;
            _shuffle = value;
            RebuildOrder();
        }
    }

    /// <summary>
    /// Orden en que suenan sin aleatorio: el de <paramref name="keys"/> (las que no estén en ella van
    /// al final, en orden alfabético). null vuelve al orden alfabético. El mosaico no cambia.
    /// </summary>
    public void SetCustomOrder(IReadOnlyList<string>? keys)
    {
        _rank = null;
        if (keys is not null)
        {
            _rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < keys.Count; i++)
                _rank.TryAdd(keys[i], i);
        }
        if (!_shuffle)
            RebuildOrder();
    }

    /// <summary>Vacía la lista (y olvida el orden propio).</summary>
    public void Clear()
    {
        _songs.Clear();
        _known.Clear();
        _order.Clear();
        _position = -1;
        _rank = null;
    }

    /// <summary>Añade canciones ignorando las repetidas. Devuelve cuántas se han añadido.</summary>
    public int Add(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (var path in paths)
        {
            if (_known.Add(path))
            {
                _songs.Add(path);
                added++;
            }
        }

        if (added > 0)
        {
            string? current = Current;
            _songs.Sort(CompareTitles);
            RebuildOrder(current);
        }
        return added;
    }

    /// <summary>Quita canciones de la lista, salvo la que está sonando. Devuelve cuántas se han quitado.</summary>
    public int Remove(IEnumerable<string> paths)
    {
        string? current = Current;
        var gone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!string.Equals(path, current, StringComparison.OrdinalIgnoreCase) && _known.Remove(path))
                gone.Add(path);
        }

        if (gone.Count > 0)
        {
            _songs.RemoveAll(gone.Contains);
            RebuildOrder(current);
        }
        return gone.Count;
    }

    /// <summary>Hace que la canción indicada sea la actual sin cambiar el orden de reproducción.</summary>
    public string? JumpTo(string path)
    {
        int index = _songs.IndexOf(path);
        if (index >= 0)
            _position = _order.IndexOf(index);
        return Current;
    }

    public string? Next() => Move(+1);

    public string? Previous() => Move(-1);

    /// <summary>Quita la canción actual (p. ej. si no se puede reproducir) y pasa a la siguiente.</summary>
    public string? RemoveCurrent()
    {
        if (_position < 0)
            return null;

        int removed = _order[_position];
        _known.Remove(_songs[removed]);
        _songs.RemoveAt(removed);
        _order.RemoveAt(_position);
        for (int i = 0; i < _order.Count; i++)
        {
            if (_order[i] > removed)
                _order[i]--;
        }

        if (_order.Count == 0)
            _position = -1;
        else if (_position >= _order.Count)
            _position = 0;
        return Current;
    }

    /// <summary>Orden alfabético por título, el mismo que el del mosaico.</summary>
    public static int CompareTitles(string a, string b) =>
        NameComparer.Compare(SongInfo.FallbackTitle(a), SongInfo.FallbackTitle(b));

    private string? Move(int step)
    {
        if (_order.Count == 0)
            return null;
        // Al llegar al final vuelve al principio (y al revés).
        _position = (_position + step + _order.Count) % _order.Count;
        return Current;
    }

    private void RebuildOrder() => RebuildOrder(Current);

    private void RebuildOrder(string? current)
    {
        _order = Enumerable.Range(0, _songs.Count).ToList();
        int currentIndex = current is null ? -1 : _songs.IndexOf(current);

        // Orden de la playlist; OrderBy es estable, así que las que no están en ella siguen en orden alfabético.
        if (!_shuffle && _rank is { } rank)
            _order = _order.OrderBy(i => rank.TryGetValue(_songs[i], out int at) ? at : int.MaxValue).ToList();

        if (_shuffle)
        {
            // Fisher-Yates
            for (int i = _order.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
            // La canción que está sonando pasa a ser la primera del nuevo orden aleatorio.
            if (currentIndex >= 0)
            {
                _order.Remove(currentIndex);
                _order.Insert(0, currentIndex);
            }
        }

        _position = currentIndex >= 0 ? _order.IndexOf(currentIndex) : (_order.Count > 0 ? 0 : -1);
    }
}
