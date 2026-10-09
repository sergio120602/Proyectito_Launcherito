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

    public int Count => _songs.Count;

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
            _songs.Sort((a, b) => NameComparer.Compare(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b)));
            RebuildOrder(current);
        }
        return added;
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
