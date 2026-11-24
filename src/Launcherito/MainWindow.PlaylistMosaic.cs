using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Launcherito;

/// <summary>
/// Una playlist en vista mosaico: las portadas de sus canciones, como en el mosaico de siempre. Con
/// «Editar» se mueven y se agrandan (con el mismo editor), y lo que se hace se guarda en la playlist:
/// el orden de las portadas pasa a ser el de la playlist y cada una recuerda su tamaño.
/// </summary>
public partial class MainWindow
{
    private TileAnimator? _listAnimator;
    private MosaicEditor? _listEditor;
    private readonly Dictionary<string, MosaicTile> _listTiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _listMosaicId;   // playlist cuyas portadas están ahora en PlaylistMosaic

    /// <summary>El editor y las animaciones del mosaico de playlists se crean la primera vez que hacen falta.</summary>
    private void EnsureListMosaic()
    {
        if (_listEditor is not null)
            return;
        _listAnimator = new TileAnimator(CategoryScroll, PlaylistMosaic);
        _listEditor = new MosaicEditor(CategoryScroll, PlaylistMosaic, _listAnimator);
        _listEditor.Changed += ListEditor_Changed;
    }

    /// <summary>
    /// Pone las portadas de la playlist en su orden y con sus tamaños. Las que ya estaban se quedan
    /// (con su carátula cargada); solo se rehace el mosaico si entran o salen canciones, así no se
    /// deshace lo que se está colocando en modo edición.
    /// </summary>
    private void ShowListMosaic(UserPlaylist list)
    {
        EnsureListMosaic();
        if (_listMosaicId != list.Id)
            ClearListMosaic();
        _listMosaicId = list.Id;
        PlaylistMosaic.Visibility = Visibility.Visible;

        var keys = PrepareKeys(list.Songs);   // las que se pueden poner, en el orden de la playlist
        bool same = keys.Count == _listTiles.Count && keys.All(_listTiles.ContainsKey);
        if (same)
        {
            Dispatcher.BeginInvoke(UpdateListCovers, DispatcherPriority.Loaded);
            return;
        }

        var present = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        var oldSlots = _listAnimator!.CaptureSlots();   // para que las que se mueven no salten de sitio
        foreach (var gone in _listTiles.Keys.Where(key => !present.Contains(key)).ToList())
        {
            _listTiles[gone].Selected -= ListTile_Selected;
            _listTiles.Remove(gone);
        }

        var added = new List<MosaicTile>();
        PlaylistMosaic.Children.Clear();
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (!_listTiles.TryGetValue(key, out var tile))
            {
                tile = new MosaicTile(key) { IsEditable = _listEditor!.IsEditing };
                tile.Selected += ListTile_Selected;
                AttachSongMenu(tile, key);   // clic derecho: Favoritos y playlists
                _listTiles.Add(key, tile);
                added.Add(tile);
            }
            // Sin tamaño elegido, una de cada seis es grande (como en el mosaico de siempre).
            tile.BaseSpan = list.Spans.TryGetValue(key, out int span) ? span : (i % 6 == 0 ? 2 : 1);
            MosaicPanel.SetSpan(tile, tile.EffectiveSpan);
            MosaicPanel.SetOrder(tile, i);
            PlaylistMosaic.Children.Add(tile);
        }

        Dispatcher.BeginInvoke(UpdateListCovers, DispatcherPriority.Loaded);
        // Entrada de las nuevas y desplazamiento de las demás en cuanto el mosaico se recoloque.
        EventHandler? onLayout = null;
        onLayout = (_, _) =>
        {
            PlaylistMosaic.LayoutUpdated -= onLayout;
            _listAnimator.Animate(added, oldSlots, null, false);
        };
        PlaylistMosaic.LayoutUpdated += onLayout;
    }

    /// <summary>Quita las portadas de la playlist (al volver a las tarjetas, cambiar de pestaña o a la vista lista).</summary>
    private void ClearListMosaic()
    {
        if (_listEditor is not null)
            SetListEditing(false);
        foreach (var tile in _listTiles.Values)
        {
            tile.Selected -= ListTile_Selected;
            tile.ReleaseCover();
        }
        _listTiles.Clear();
        PlaylistMosaic.Children.Clear();
        PlaylistMosaic.Visibility = Visibility.Collapsed;
        _listMosaicId = null;
    }

    /// <summary>Pulsar una portada: suena la playlist empezando por ella.</summary>
    private void ListTile_Selected(object? sender, EventArgs e)
    {
        if (sender is MosaicTile tile && _listMosaicId is { } id)
            PlayFromList(id, tile.SongPath);
    }

    /// <summary>
    /// Se ha movido o agrandado una portada: el orden del mosaico pasa a ser el de la playlist (el
    /// que se sigue sin aleatorio) y se guarda el tamaño de cada una.
    /// </summary>
    private void ListEditor_Changed(object? sender, EventArgs e)
    {
        if (_listMosaicId is not { } id || _listEditor is null)
            return;
        var tiles = _listEditor.Ordered();
        _userLists.SaveLayout(id, tiles.Select(tile => tile.SongPath).ToList(),
                              tiles.ToDictionary(tile => tile.SongPath, tile => tile.BaseSpan, StringComparer.OrdinalIgnoreCase));
        if (_sessionListId == id && _userLists.Get(id) is { } list)
        {
            _playlist.SetCustomOrder(list.Songs);   // si está sonando, el nuevo orden vale ya
            UpdatePosition();
        }
        UpdateListCovers();
    }

    private void ListViewToggle_Click(object sender, RoutedEventArgs e)
    {
        if (OpenList is not { } list)
            return;
        _userLists.SetListView(list.Id, !list.ListView);
        CategoryScroll.ScrollToTop();
        RefreshPlaylists();
    }

    private void ListEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_listEditor is not null)
            SetListEditing(!_listEditor.IsEditing);
    }

    /// <summary>Modo edición del mosaico de la playlist: el botón pasa a «Dejar edición» en verde y cada portada lleva su tirador.</summary>
    private void SetListEditing(bool editing)
    {
        if (_listEditor is null)
            return;
        _listEditor.IsEditing = editing;
        foreach (var tile in _listTiles.Values)
            tile.IsEditable = editing;
        ListEditButton.Background = editing ? (Brush)FindResource("EditGreen") : EditIdle;
        ListEditIcon.Text = editing ? "" : "";   // hecho / lápiz
        ListEditText.Text = editing ? "Dejar edición" : "Editar";
        ListEditButton.ToolTip = editing
            ? "Terminar de colocar las portadas"
            : "Mover las portadas y cambiar su tamaño (moverlas cambia el orden de la playlist)";
    }

    private void CategoryScroll_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateListCovers();

    /// <summary>Carga las portadas de la playlist que se ven (y una pantalla por encima y por debajo) y suelta el resto.</summary>
    private void UpdateListCovers()
    {
        if (_listTiles.Count == 0 || !PlaylistMosaic.IsVisible)
            return;
        // Dónde empieza el mosaico dentro de lo que se ve (debajo puede haber otras cosas en el panel).
        double panelTop = PlaylistMosaic.TranslatePoint(new Point(0, 0), CategoryScroll).Y;
        double viewport = CategoryScroll.ViewportHeight;
        foreach (MosaicTile tile in PlaylistMosaic.Children)
        {
            var slot = LayoutInformation.GetLayoutSlot(tile);
            double top = panelTop + slot.Top;
            if (top + slot.Height >= -viewport && top <= viewport * 2)
                tile.EnsureCover();
            else
                tile.ReleaseCover();
        }
    }
}
