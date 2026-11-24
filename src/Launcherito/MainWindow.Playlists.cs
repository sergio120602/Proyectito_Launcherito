using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Launcherito;

/// <summary>
/// Favoritos y playlists del usuario: el corazón y el ＋ de los controles, el menú de clic derecho
/// de cada canción, la pestaña Playlists y escuchar solo las canciones de una playlist.
/// </summary>
public partial class MainWindow
{
    private const string HeartIcon = "";
    private const string HeartFillIcon = "";
    private const string ListIcon = "";     // lista con viñetas
    private const string AddIcon = "";
    private const string RemoveIcon = "";

    private static readonly Brush FavoritesFill = Frozen(new LinearGradientBrush(
        Color.FromRgb(0x4A, 0x16, 0x38), Color.FromRgb(0x1A, 0x0E, 0x18), 45));
    private static readonly Brush ListFill = Frozen(new LinearGradientBrush(
        Color.FromRgb(0x2E, 0x1F, 0x5C), Color.FromRgb(0x15, 0x13, 0x1F), 45));
    private static readonly Brush NewListFill = Frozen(new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12)));

    private readonly UserPlaylists _userLists = new();
    private string? _sessionListId;      // playlist que se está escuchando (solo sus canciones), o null
    private bool _loadingSession;        // mientras se cargan sus canciones, AddToPlaylist no la deja
    private string? _renamingListId;     // playlist que se renombra en el panel del nombre; null si es nueva
    private string? _songForNewList;     // canción que entra en la playlist que se crea desde su menú
    private string? _pickListId;         // playlist a la que se añaden canciones en el panel de casillas

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    // ─────────────────────────────── Favoritos ───────────────────────────────

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_playlist.Current is { } key)
            ToggleFavorite(key);
    }

    private void ToggleFavorite(string key)
    {
        KeepInLibrary(key);
        bool favorite = _userLists.ToggleFavorite(key);
        AfterListChanged(UserPlaylists.FavoritesId, [key], favorite);
        string title = DescribeSong(key).Title;
        ShowPlaylistStatus(favorite ? $"♥  «{title}» está en Favoritos" : $"«{title}» ya no está en Favoritos");
    }

    /// <summary>Corazón (lleno si la que suena es favorita) y botón ＋, que sin canción no hacen nada.</summary>
    private void UpdateFavoriteButton()
    {
        string? current = _playlist.Current;
        bool favorite = current is not null && _userLists.IsFavorite(current);
        FavoriteButton.Content = favorite ? HeartFillIcon : HeartIcon;
        FavoriteButton.Foreground = favorite ? (Brush)FindResource("FavoritePink") : Brushes.White;
        FavoriteButton.ToolTip = favorite ? "Quitar de Favoritos" : "Añadir a Favoritos";
        FavoriteButton.IsEnabled = current is not null;
        FavoriteButton.Opacity = current is null ? 0.4 : 1;
        AddToListButton.IsEnabled = current is not null;
        AddToListButton.Opacity = current is null ? 0.4 : 1;
    }

    /// <summary>Botón «Tus playlists» de la pantalla inicial: cuántas hay además de Favoritos.</summary>
    private void UpdateStartPlaylistsButton()
    {
        int lists = _userLists.All.Count - 1;
        int favorites = _userLists.Favorites.Songs.Count;
        PlaylistsCountText.Text = lists > 0 ? $"· {lists}" : "";
        StartPlaylistsButton.ToolTip =
            $"Favoritos ({Songs(favorites)}) y {(lists == 1 ? "1 playlist" : $"{lists} playlists")} hechas por ti";
    }

    // ─────────────────── Menú de una canción: Favoritos y playlists ───────────────────

    /// <summary>Clic derecho en una portada o en una fila: Favoritos, las playlists y «Nueva playlist…».</summary>
    private void AttachSongMenu(FrameworkElement owner, string key)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("DarkMenu") };
        owner.ContextMenu = menu;
        // Se rellena justo antes de abrirse, para que esté al día (favorita o no, playlists nuevas…).
        owner.ContextMenuOpening += (_, _) => FillSongMenu(menu, key);
    }

    /// <summary>El botón ＋ de los controles abre el mismo menú para la canción que suena.</summary>
    private void AddToListButton_Click(object sender, RoutedEventArgs e)
    {
        if (_playlist.Current is not { } key)
            return;
        var menu = new ContextMenu
        {
            Style = (Style)FindResource("DarkMenu"),
            PlacementTarget = AddToListButton,
            Placement = PlacementMode.Top,
        };
        FillSongMenu(menu, key);
        menu.IsOpen = true;
    }

    private void FillSongMenu(ContextMenu menu, string key)
    {
        menu.Items.Clear();
        menu.Items.Add(new MenuItem
        {
            Header = DescribeSong(key).Title,
            IsEnabled = false,
            FontWeight = FontWeights.SemiBold,
        });

        bool favorite = _userLists.IsFavorite(key);
        var favoriteItem = new MenuItem
        {
            Header = favorite ? "Quitar de Favoritos" : "Añadir a Favoritos",
            Icon = MenuGlyph(favorite ? HeartFillIcon : HeartIcon, (Brush)FindResource("FavoritePink")),
        };
        favoriteItem.Click += (_, _) => ToggleFavorite(key);
        menu.Items.Add(favoriteItem);
        menu.Items.Add(new Separator());

        // Cada playlist con una marca si ya la tiene: al pulsarla se pone o se quita.
        foreach (var list in _userLists.All.Where(list => !list.IsFavorites))
        {
            bool inList = list.Contains(key);
            var item = new MenuItem
            {
                Header = list.Name,
                IsChecked = inList,
                Icon = MenuGlyph(ListIcon, (Brush)FindResource("Muted")),
                ToolTip = inList ? "Ya está: pulsa para quitarla" : "Añadir a esta playlist",
            };
            string id = list.Id;
            item.Click += (_, _) => SetInList(id, key, add: !inList);
            menu.Items.Add(item);
        }

        var create = new MenuItem { Header = "Nueva playlist…", Icon = MenuGlyph(AddIcon, (Brush)FindResource("Accent")) };
        create.Click += (_, _) =>
        {
            _songForNewList = key;
            OpenNameDialog(null);
        };
        menu.Items.Add(create);
    }

    private static TextBlock MenuGlyph(string icon, Brush brush) => new()
    {
        Text = icon,
        FontFamily = IconFont,
        FontSize = 13,
        Foreground = brush,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Pone o quita una canción de una playlist (no de Favoritos, que va con su corazón).</summary>
    private void SetInList(string listId, string key, bool add)
    {
        if (_userLists.Get(listId) is not { } list)
            return;
        if (add)
            KeepInLibrary(key);
        int changed = add ? _userLists.Add(listId, [key]) : _userLists.Remove(listId, [key]);
        if (changed == 0)
            return;
        AfterListChanged(listId, [key], add);
        string title = DescribeSong(key).Title;
        ShowPlaylistStatus(add ? $"«{title}» añadida a «{list.Name}»" : $"«{title}» quitada de «{list.Name}»");
    }

    /// <summary>
    /// Tras poner o quitar canciones de una playlist. Si es la que se está escuchando, entran o salen
    /// también del mosaico (la que suena sigue sonando). Luego se redibuja lo que se ve.
    /// </summary>
    private void AfterListChanged(string listId, IReadOnlyList<string> keys, bool added)
    {
        if (_sessionListId == listId && _userLists.Get(listId) is { } list)
        {
            if (added)
            {
                _loadingSession = true;
                try
                {
                    _playlist.SetCustomOrder(list.Songs);
                    AddToPlaylist(PrepareKeys(keys));
                }
                finally
                {
                    _loadingSession = false;
                }
            }
            else
            {
                var gone = keys.Where(key => key != _playlist.Current).ToList();
                if (_playlist.Remove(gone) > 0)
                {
                    ForgetLoaded(gone);
                    SyncMosaic();
                    UpdatePosition();
                    ScheduleCategoryRefresh();
                }
            }
        }
        UpdateFavoriteButton();
        UpdateStartPlaylistsButton();
        if (_section == Section.Playlists)
            RefreshPlaylists();
    }

    // ─────────────────────── Escuchar una playlist ───────────────────────

    /// <summary>
    /// Deja en el mosaico y en la lista de reproducción solo las canciones de la playlist y empieza a
    /// sonar <paramref name="startWith"/> (o la primera; con el aleatorio, una al azar). Sin aleatorio
    /// suenan en el orden de la playlist.
    /// </summary>
    private void LoadPlaylistSession(UserPlaylist list, string? startWith)
    {
        var keys = PrepareKeys(list.Songs);
        if (keys.Count == 0)
        {
            ShowPlaylistStatus(list.Songs.Count == 0
                ? $"«{list.Name}» está vacía: añádele canciones primero"
                : $"Ninguna canción de «{list.Name}» se puede poner (¿se han movido los .mp3?)");
            return;
        }

        SetEditing(false);
        Stop();
        // Lo que se queda fuera deja de estar cargado (tampoco sale en Artistas ni en Géneros).
        var keep = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        ForgetLoaded(_playlist.Songs.Where(key => !keep.Contains(key)).ToList(), keepCurrent: false);
        _playlist.Clear();

        _sessionListId = list.Id;
        _loadingSession = true;
        try
        {
            _playlist.SetCustomOrder(list.Songs);
            AddToPlaylist(keys, startWith);
        }
        finally
        {
            _loadingSession = false;
        }
        UpdateSessionLabel();
        ScheduleCategoryRefresh();

        int missing = list.Songs.Count - keys.Count;
        ShowPlaylistStatus(missing == 0
            ? $"Escuchando «{list.Name}»: {Songs(keys.Count)}"
            : $"Escuchando «{list.Name}»: {Songs(keys.Count)} ({missing} ya no {(missing == 1 ? "está" : "están")} en su sitio)");
    }

    /// <summary>Se deja de escuchar solo la playlist: las canciones se quedan, pero vuelve el orden alfabético.</summary>
    private void LeaveSessionList()
    {
        _sessionListId = null;
        _sessionTrendingId = null;
        _playlist.SetCustomOrder(null);
        UpdateSessionLabel();
    }

    /// <summary>«Ver todas»: vuelven al mosaico todas las canciones guardadas, sin cortar la que suena.</summary>
    private void LeaveSession_Click(object sender, RoutedEventArgs e)
    {
        LeaveSessionList();
        LibraryButton_Click(sender, e);
        if (_section == Section.Playlists)
            RefreshPlaylists();
    }

    private void UpdateSessionLabel()
    {
        var list = _sessionListId is null ? null : _userLists.Get(_sessionListId);
        if (list is null)
            _sessionListId = null;
        // O una playlist del momento de Spotify.
        string? name = list?.Name ?? (_sessionTrendingId is { } id ? _trending.Get(id)?.Name : null);
        SessionPanel.Visibility = name is null ? Visibility.Collapsed : Visibility.Visible;
        SessionText.Text = name is null ? "" : $"♫  Escuchando «{name}»";
        UpdateShuffleButton();
    }

    /// <summary>Olvida lo leído de canciones que salen del mosaico (la que suena, solo si se indica).</summary>
    private void ForgetLoaded(IReadOnlyCollection<string> keys, bool keepCurrent = true)
    {
        var gone = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        if (keepCurrent && _playlist.Current is { } current)
            gone.Remove(current);
        foreach (var key in gone)
            _meta.Remove(key);
        foreach (var match in _localSongs.Where(kv => gone.Contains(kv.Value)).Select(kv => kv.Key).ToList())
            _localSongs.Remove(match);
    }

    /// <summary>
    /// Las canciones que se pueden poner, en el mismo orden: las de Spotify y YouTube Music se
    /// recuperan de «Tus canciones» y los .mp3 tienen que seguir en su sitio.
    /// </summary>
    private List<string> PrepareKeys(IEnumerable<string> keys)
    {
        var ready = new List<string>();
        foreach (var key in keys)
        {
            if (_tiles.ContainsKey(key))
            {
                ready.Add(key);
            }
            else if (OnlineSongs.IsOnline(key))
            {
                if (!OnlineSongs.TryGet(key, out _))
                {
                    if (!_library.TryGet(key, out var saved))
                        continue;
                    OnlineSongs.Register(saved.ToTrack());
                }
                ready.Add(key);
            }
            else if (File.Exists(key))
            {
                ready.Add(key);
            }
        }
        return ready;
    }

    private bool IsAvailable(string key) =>
        _tiles.ContainsKey(key) ||
        (OnlineSongs.IsOnline(key) ? OnlineSongs.TryGet(key, out _) || _library.Contains(key) : File.Exists(key));

    /// <summary>Título y artista que se conocen de una canción, esté cargada o no.</summary>
    private (string Title, string Artist) DescribeSong(string key)
    {
        if (_meta.TryGetValue(key, out var meta))
            return (meta.Title, meta.Artist);
        if (_library.TryGet(key, out var saved) && saved.Title.Length > 0)
            return (saved.Title, saved.Artist);
        if (OnlineSongs.TryGet(key, out var track))
            return (track.Title, track.Artists);
        return (SongInfo.FallbackTitle(key), "");
    }

    /// <summary>Aviso de playlists en la barra superior (en morado).</summary>
    private void ShowPlaylistStatus(string text)
    {
        ShowListStatus(text);
        ListStatusText.Foreground = (Brush)FindResource("AccentHover");
    }

    // ─────────────────────────── Pestaña Playlists ───────────────────────────

    private void PlaylistsTab_Click(object sender, RoutedEventArgs e) => SetSection(Section.Playlists);

    /// <summary>Desde la pantalla inicial se puede ir a las playlists aunque no haya nada cargado.</summary>
    private void StartPlaylistsButton_Click(object sender, RoutedEventArgs e)
    {
        SetSection(Section.Playlists);
        StartPanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;
    }

    /// <summary>Tarjetas de Favoritos y de cada playlist, o el detalle de la que esté abierta.</summary>
    private void RefreshPlaylists()
    {
        if (_openCategory is { } id)
        {
            if (_userLists.Get(id) is { } open)
            {
                ShowPlaylistDetail(open);
                return;
            }
            _openCategory = null;   // se ha borrado
        }

        ClearListMosaic();
        CategoryHeader.Visibility = Visibility.Collapsed;
        PlaylistActions.Visibility = Visibility.Collapsed;
        SongListPanel.Visibility = Visibility.Collapsed;
        CardsPanel.Visibility = Visibility.Visible;
        CategoryHeaderPictureHost.Child = null;

        CardsPanel.Children.Clear();
        foreach (var list in _userLists.All)
        {
            var card = new CategoryCard(list.Name, round: false,
                list.IsFavorites ? HeartFillIcon : ListIcon,
                (Brush)FindResource(list.IsFavorites ? "FavoritePink" : "AccentHover"),
                list.IsFavorites ? FavoritesFill : ListFill)
            {
                Style = (Style)FindResource("CardButton"),
            };
            card.SetDetail(Songs(list.Songs.Count) + (_sessionListId == list.Id ? "  ·  sonando" : ""));
            string listId = list.Id;
            card.Click += (_, _) =>
            {
                _openCategory = listId;
                CategoryScroll.ScrollToTop();
                RefreshPlaylists();
            };
            CardsPanel.Children.Add(card);
        }

        var create = new CategoryCard("Nueva playlist", round: false, AddIcon, (Brush)FindResource("Muted"), NewListFill)
        {
            Style = (Style)FindResource("CardButton"),
            ToolTip = "Crear una playlist",
        };
        create.SetDetail("Ponle nombre y añade canciones");
        create.Click += (_, _) => OpenNameDialog(null);
        CardsPanel.Children.Add(create);
    }

    /// <summary>Canciones de una playlist: al pulsar una suena la playlist desde ella; la X la quita.</summary>
    private void ShowPlaylistDetail(UserPlaylist list)
    {
        bool playing = _sessionListId == list.Id;
        CardsPanel.Visibility = Visibility.Collapsed;
        SongListPanel.Visibility = Visibility.Visible;
        CategoryHeader.Visibility = Visibility.Visible;
        PlaylistActions.Visibility = Visibility.Visible;
        CategoryTitleText.Text = list.Name;
        CategorySubtitleText.Text = (list.IsFavorites ? "Favoritos · " : "Playlist · ") + Songs(list.Songs.Count) +
                                    (playing ? "  ·  sonando ahora" : "");
        CategoryHeaderPictureHost.Child = PlaylistThumbnail(list, 72);
        RenameListButton.Visibility = list.IsFavorites ? Visibility.Collapsed : Visibility.Visible;
        DeleteListButton.Visibility = list.IsFavorites ? Visibility.Collapsed : Visibility.Visible;

        // El botón de vista muestra a la que se cambia al pulsarlo (como el de la vista original).
        ListViewToggleIcon.Text = list.ListView ? "" : "";   // mosaico / lista
        ListViewToggleText.Text = list.ListView ? "Vista mosaico" : "Vista lista";
        ListViewToggleButton.ToolTip = list.ListView
            ? "Ver las portadas de los álbumes, que se pueden mover y agrandar"
            : "Ver las canciones en una lista sencilla";

        // Vista mosaico (si hay alguna canción que se pueda poner; si no, la lista con su explicación).
        int playable = PrepareKeys(list.Songs).Count;
        bool mosaic = !list.ListView && playable > 0;
        ListEditButton.Visibility = mosaic ? Visibility.Visible : Visibility.Collapsed;
        SongListPanel.Children.Clear();
        if (mosaic)
        {
            SongListPanel.Visibility = Visibility.Collapsed;
            PlayListButton.IsEnabled = true;
            PlayListButton.Opacity = 1;
            ShowListMosaic(list);
            return;
        }
        ClearListMosaic();

        int available = 0;
        foreach (var key in list.Songs)
        {
            var (title, artist) = DescribeSong(key);
            var row = CreateSongRow(key, title, artist);
            row.Click -= SongRow_Click;
            row.Click += (_, _) => PlayFromList(list.Id, key);
            if (IsAvailable(key))
            {
                available++;
            }
            else
            {
                row.IsEnabled = false;
                row.Opacity = 0.45;
                ToolTipService.SetShowOnDisabled(row, true);
                row.ToolTip = "El archivo .mp3 ya no está en su sitio";
            }

            var remove = new Button
            {
                Style = (Style)FindResource("IconButton"),
                Width = 36,
                Height = 36,
                FontSize = 12,
                Content = RemoveIcon,
                Foreground = (Brush)FindResource("Muted"),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = list.IsFavorites ? "Quitar de Favoritos" : "Quitar de la playlist",
            };
            remove.Click += (_, _) =>
            {
                if (list.IsFavorites)
                    ToggleFavorite(key);
                else
                    SetInList(list.Id, key, add: false);
            };

            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(remove, 1);
            line.Children.Add(row);
            line.Children.Add(remove);
            SongListPanel.Children.Add(line);
        }

        PlayListButton.IsEnabled = available > 0;
        PlayListButton.Opacity = available > 0 ? 1 : 0.4;
        if (list.Songs.Count == 0)
        {
            SongListPanel.Children.Add(new TextBlock
            {
                Text = list.IsFavorites
                    ? "Aún no tienes favoritos. Mientras suena una canción, pulsa el corazón junto a los controles, " +
                      "o haz clic derecho en una portada y elige «Añadir a Favoritos»."
                    : "Esta playlist está vacía. Añade canciones con «Añadir canciones», o haz clic derecho en " +
                      "una portada o con el botón ＋ de los controles.",
                Foreground = (Brush)FindResource("Muted"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(14, 10, 14, 10),
            });
        }
    }

    private static Border PlaylistThumbnail(UserPlaylist list, double size) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(12),
        Background = list.IsFavorites ? FavoritesFill : ListFill,
        Child = new TextBlock
        {
            Text = list.IsFavorites ? HeartFillIcon : ListIcon,
            FontFamily = IconFont,
            FontSize = size * 0.42,
            Foreground = list.IsFavorites
                ? new SolidColorBrush(Color.FromRgb(0xF4, 0x72, 0xB6))
                : new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    /// <summary>Pulsar una canción en el detalle: si su playlist ya suena salta a ella; si no, la carga empezando por ella.</summary>
    private void PlayFromList(string listId, string key)
    {
        if (_userLists.Get(listId) is not { } list)
            return;
        if (_sessionListId == listId && _tiles.ContainsKey(key))
            SelectSong(key);
        else
            LoadPlaylistSession(list, key);
    }

    private UserPlaylist? OpenList => _openCategory is { } id ? _userLists.Get(id) : null;

    /// <summary>«Reproducir»: suena la playlist y se pasa al mosaico con sus canciones.</summary>
    private void PlayListButton_Click(object sender, RoutedEventArgs e)
    {
        if (OpenList is not { } list)
            return;
        LoadPlaylistSession(list, null);
        if (_sessionListId == list.Id)
            SetSection(Section.Songs);
    }

    private void RenameList_Click(object sender, RoutedEventArgs e)
    {
        if (OpenList is { IsFavorites: false } list)
            OpenNameDialog(list.Id);
    }

    private void DeleteList_Click(object sender, RoutedEventArgs e)
    {
        if (OpenList is not { IsFavorites: false } list)
            return;
        if (MessageBox.Show(this, $"¿Borrar la playlist «{list.Name}»?\nSus canciones siguen en «Tus canciones».",
                "Borrar playlist", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _userLists.Delete(list.Id);
        if (_sessionListId == list.Id)
            LeaveSessionList();   // lo que suena sigue sonando
        _openCategory = null;
        RefreshPlaylists();
        UpdateStartPlaylistsButton();
        ShowPlaylistStatus($"Playlist «{list.Name}» borrada");
    }

    // ─────────────────── Panel del nombre (nueva playlist o renombrar) ───────────────────

    private void OpenNameDialog(string? renamingId)
    {
        var list = renamingId is null ? null : _userLists.Get(renamingId);
        _renamingListId = list?.Id;
        NameDialogTitle.Text = list is null ? "Nueva playlist" : "Renombrar playlist";
        NameSaveButton.Content = list is null ? "Crear" : "Guardar";
        NameBox.Text = list?.Name ?? "";
        NameDialogStatus.Text = "";
        NameOverlay.Visibility = Visibility.Visible;
        // Tras mostrarse el panel (si no, el cuadro aún no puede recibir el foco).
        Dispatcher.BeginInvoke(() =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            SaveNameDialog();
        else if (e.Key == Key.Escape)
            CloseNameDialog();
    }

    private void NameSave_Click(object sender, RoutedEventArgs e) => SaveNameDialog();

    private void NameCancel_Click(object sender, RoutedEventArgs e) => CloseNameDialog();

    private void CloseNameDialog()
    {
        NameOverlay.Visibility = Visibility.Collapsed;
        _renamingListId = null;
        _songForNewList = null;
    }

    private void SaveNameDialog()
    {
        string name = NameBox.Text.Trim();
        if (_userLists.CheckName(name, _renamingListId) is { } problem)
        {
            NameDialogStatus.Text = problem;
            return;
        }

        UserPlaylist? created = null;
        if (_renamingListId is { } id)
        {
            _userLists.Rename(id, name);
            ShowPlaylistStatus($"Playlist renombrada: «{name}»");
        }
        else
        {
            created = _userLists.Create(name);
            if (_songForNewList is { } key)
            {
                _userLists.Add(created.Id, [key]);
                ShowPlaylistStatus($"«{DescribeSong(key).Title}» añadida a la nueva playlist «{name}»");
            }
            else
            {
                ShowPlaylistStatus($"Playlist «{name}» creada");
            }
            if (_section == Section.Playlists)
                _openCategory = created.Id;   // detrás del panel de canciones queda abierta la nueva
        }

        CloseNameDialog();
        UpdateSessionLabel();
        UpdateStartPlaylistsButton();
        if (_section == Section.Playlists)
            RefreshPlaylists();
        // Una playlist recién creada se llena al momento: se abre el panel para elegir sus canciones.
        if (created is not null)
            OpenPickDialog(created);
    }

    // ─────────────────── Panel Añadir canciones a una playlist ───────────────────

    private const int PickCoverSize = 88;      // píxeles con que se decodifica la carátula de cada fila
    private const int PickCoverWorkers = 4;    // carátulas que se leen a la vez

    /// <summary>Fila del panel: su casilla, el texto por el que se filtra y su carátula.</summary>
    private sealed class PickRow(CheckBox check, string key, string text, Border cover)
    {
        public CheckBox Check { get; } = check;
        public string Key { get; } = key;
        public string Text { get; } = text;
        public Border Cover { get; } = cover;
        public bool CoverRequested { get; set; }
    }

    private readonly List<PickRow> _pickRows = new();
    private int _pickVersion;   // invalida la carga de carátulas al cerrar o volver a abrir el panel

    private void AddSongsToList_Click(object sender, RoutedEventArgs e)
    {
        if (OpenList is { } list)
            OpenPickDialog(list);
    }

    private void OpenPickDialog(UserPlaylist list)
    {
        int version = ++_pickVersion;
        _pickListId = list.Id;
        PickTitle.Text = $"Añadir canciones a «{list.Name}»";
        PickList.Children.Clear();
        _pickRows.Clear();
        foreach (var song in _library.Songs)
        {
            // Las que ya tiene no salen, ni los .mp3 que ya no están en su sitio.
            if (list.Contains(song.Key) || (!song.IsOnline && !File.Exists(song.Key)))
                continue;
            // Para leer su portada, las de Spotify y YouTube Music tienen que estar registradas.
            if (song.IsOnline && !OnlineSongs.TryGet(song.Key, out _))
                OnlineSongs.Register(song.ToTrack());

            var cover = CreatePickCover();
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var details = CreateSavedSongContent(song, missing: false);
            details.Margin = new Thickness(12, 0, 0, 0);
            details.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(details, 1);
            content.Children.Add(cover);
            content.Children.Add(details);

            var check = new CheckBox
            {
                Style = (Style)FindResource("SongCheck"),
                Background = (Brush)FindResource("Accent"),
                Content = content,
                Tag = song.Key,
                ToolTip = song.IsOnline ? null : song.Key,
            };
            check.Checked += PickCheck_Changed;
            check.Unchecked += PickCheck_Changed;
            PickList.Children.Add(check);
            _pickRows.Add(new PickRow(check, song.Key, $"{song.Title} {song.Artist}", cover));
        }
        if (_pickRows.Count == 0)
        {
            PickList.Children.Add(new TextBlock
            {
                Text = _library.Count == 0
                    ? "Aún no tienes canciones guardadas: carga algún .mp3 o una lista de Spotify o YouTube Music."
                    : "Ya están en esta playlist todas tus canciones.",
                Foreground = (Brush)FindResource("Muted"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 8, 12, 8),
            });
        }

        PickFilterBox.Text = "";   // también actualiza los botones
        UpdatePickButtons();
        PickScroll.ScrollToTop();
        PickOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => PickFilterBox.Focus(), DispatcherPriority.Input);
        // Las carátulas, cuando el panel ya está colocado (así se sabe qué filas se ven).
        Dispatcher.BeginInvoke(() =>
        {
            for (int i = 0; i < PickCoverWorkers; i++)
                _ = LoadPickCoversAsync(version);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Hueco cuadrado de la carátula: un icono de nota hasta que se carga la del álbum.</summary>
    private static Border CreatePickCover() => new()
    {
        Width = 44,
        Height = 44,
        CornerRadius = new CornerRadius(6),
        Background = NewListFill,
        Child = new TextBlock
        {
            Text = "",   // nota musical
            FontFamily = IconFont,
            FontSize = 16,
            Foreground = PickCoverIconBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private static readonly Brush PickCoverIconBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));

    /// <summary>
    /// Va leyendo carátulas fuera del hilo de la interfaz, una tras otra: primero las de las filas que
    /// se ven (también después de desplazarse o filtrar) y luego las demás. Se para al cerrar el panel.
    /// </summary>
    private async Task LoadPickCoversAsync(int version)
    {
        while (version == _pickVersion && NextPickCover() is { } row)
        {
            row.CoverRequested = true;
            ImageSource? cover;
            try
            {
                cover = await Task.Run(() => SongInfo.Read(row.Key, PickCoverSize).Cover);
            }
            catch (Exception)
            {
                continue;   // sin carátula: se queda el icono
            }
            if (version != _pickVersion)
                return;
            if (cover is not null)
            {
                row.Cover.Child = null;
                row.Cover.Background = new ImageBrush(cover) { Stretch = Stretch.UniformToFill };
            }
        }
    }

    /// <summary>La siguiente fila sin carátula: una de las que se ven, o si no, la primera que quede.</summary>
    private PickRow? NextPickCover()
    {
        PickRow? firstShown = null;
        double viewport = PickScroll.ViewportHeight;
        foreach (var row in _pickRows)
        {
            if (row.CoverRequested || !row.Check.IsVisible)
                continue;
            double top = row.Check.TranslatePoint(new Point(0, 0), PickScroll).Y;
            if (top + row.Check.ActualHeight >= 0 && top <= viewport)
                return row;
            firstShown ??= row;
        }
        // Después de las que se ven, las que deja el filtro, y al final las ocultas por él.
        return firstShown ?? _pickRows.FirstOrDefault(row => !row.CoverRequested);
    }

    private IEnumerable<CheckBox> VisiblePickChecks =>
        _pickRows.Select(row => row.Check).Where(check => check.Visibility == Visibility.Visible);

    private void PickCheck_Changed(object sender, RoutedEventArgs e) => UpdatePickButtons();

    /// <summary>Filtra por título o artista sin distinguir mayúsculas ni tildes. Las marcadas siguen marcadas.</summary>
    private void PickFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = PickFilterBox.Text.Trim();
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        foreach (var row in _pickRows)
        {
            bool match = query.Length == 0 ||
                         compare.IndexOf(row.Text, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
            row.Check.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
        }
        UpdatePickButtons();
    }

    private void UpdatePickButtons()
    {
        int marked = _pickRows.Count(row => row.Check.IsChecked == true);
        var visible = VisiblePickChecks.ToList();
        bool allVisible = visible.Count > 0 && visible.All(check => check.IsChecked == true);
        bool filtered = PickFilterBox.Text.Trim().Length > 0;
        ConfirmPickButton.Content = marked == 0 ? "Añadir" : $"Añadir {Songs(marked)}";
        ConfirmPickButton.IsEnabled = marked > 0;
        ConfirmPickButton.Opacity = marked > 0 ? 1 : 0.4;
        PickAllButton.Content = allVisible ? "Quitar la selección"
            : filtered ? "Seleccionar las que se ven" : "Seleccionar todas";
        PickAllButton.Visibility = visible.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PickAllButton_Click(object sender, RoutedEventArgs e)
    {
        var visible = VisiblePickChecks.ToList();
        bool select = visible.Any(check => check.IsChecked != true);
        foreach (var check in visible)
            check.IsChecked = select;
    }

    private void PickOverlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            ClosePickDialog();
    }

    private void PickCancel_Click(object sender, RoutedEventArgs e) => ClosePickDialog();

    private void ClosePickDialog()
    {
        _pickVersion++;   // para la carga de carátulas
        PickOverlay.Visibility = Visibility.Collapsed;
        PickList.Children.Clear();   // las filas (y sus carátulas) se rehacen cada vez que se abre
        _pickRows.Clear();
        _pickListId = null;
    }

    private void ConfirmPick_Click(object sender, RoutedEventArgs e)
    {
        if (_pickListId is not { } id || _userLists.Get(id) is not { } list)
            return;
        var keys = _pickRows.Where(row => row.Check.IsChecked == true).Select(row => row.Key).ToList();
        ClosePickDialog();
        int added = _userLists.Add(id, keys);
        if (added == 0)
            return;
        AfterListChanged(id, keys, added: true);
        ShowPlaylistStatus($"{Songs(added)} añadida{(added == 1 ? "" : "s")} a «{list.Name}»");
    }
}
