using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Launcherito;

public partial class MainWindow : Window
{
    private const string PlayIcon = "";
    private const string PauseIcon = "";
    private const int CoverDecodeSize = 640;
    private const double MinControlsWidth = 330;   // ancho que necesitan los botones sin reducirse

    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Playlist _playlist = new();   // el modo aleatorio empieza activado (Playlist.Shuffle = true)
    private bool _isPlaying;
    private bool _isSeeking;
    private int _shownSecond;   // segundo que muestra CurrentTimeText, para no reescribirlo sin necesidad
    private readonly Dictionary<string, MosaicTile> _tiles = new(StringComparer.OrdinalIgnoreCase);
    private MosaicTile? _activeTile;
    private bool _mosaicView;
    private readonly TileAnimator _animator;

    private enum Section { Songs, Artists, Genres }
    private Section _section = Section.Songs;
    private readonly MusicCatalog _catalog = new();
    private readonly Dictionary<string, SongMeta> _meta = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _pending = new();             // canciones por clasificar con Deezer
    private bool _classifying;
    private readonly Dictionary<string, CategoryCard> _artistCards = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly Dictionary<string, CategoryCard> _genreCards = new(StringComparer.CurrentCultureIgnoreCase);
    private string? _openCategory;                                // artista o género abierto, o null en la lista
    private readonly DispatcherTimer _categoryRefresh = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private const string Unclassified = "Sin clasificar";

    public MainWindow()
    {
        InitializeComponent();
        // La versión sale de <Version> en el .csproj, así el título siempre coincide con el .exe.
        Title = $"Launcherito {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        _animator = new TileAnimator(MosaicView, Mosaic);

        _player.MediaOpened += Player_MediaOpened;
        _player.MediaEnded += Player_MediaEnded;
        _player.MediaFailed += Player_MediaFailed;
        _timer.Tick += Timer_Tick;
        // handledEventsToo: al pulsar en la pista, el Slider marca el clic como gestionado
        // (IsMoveToPointEnabled) y un manejador normal declarado en XAML no llegaría a ejecutarse.
        SeekSlider.AddHandler(PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(SeekSlider_PreviewMouseLeftButtonDown), handledEventsToo: true);
        UpdateShuffleButton();
        _categoryRefresh.Tick += (_, _) =>
        {
            _categoryRefresh.Stop();
            RefreshCategories();
        };
        SetMosaicView(false);

        // Permite abrir canciones pasadas como argumento (p. ej. "Abrir con → Launcherito").
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
            Loaded += (_, _) => AddSongs(args.Skip(1));
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona una o varias canciones",
            Filter = "Archivos MP3 (*.mp3)|*.mp3",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == true)
            AddSongs(dialog.FileNames);
    }

    private void AddSongs(IEnumerable<string> paths)
    {
        var valid = new List<string>();
        int rejected = 0;
        foreach (var path in paths)
        {
            if (File.Exists(path) &&
                string.Equals(Path.GetExtension(path), ".mp3", StringComparison.OrdinalIgnoreCase))
                valid.Add(path);
            else
                rejected++;
        }

        if (rejected > 0)
        {
            MessageBox.Show(this, $"Solo se admiten archivos .mp3. Se han ignorado {rejected} archivo(s).",
                "Launcherito", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        int before = _playlist.Count;
        bool wasEmpty = before == 0;
        _playlist.Add(valid);
        SyncMosaic();
        _ = ClassifyAsync(valid);

        // Al pasar de una canción a varias se cambia sola a la vista mosaico.
        if (before <= 1 && _playlist.Count > 1 && !_mosaicView)
            SetMosaicView(true);

        // Si ya estaba sonando algo, las nuevas canciones se añaden a la lista sin interrumpirla.
        if (wasEmpty && _playlist.Current is { } first)
            PlaySong(first);
        else
            UpdatePosition();
    }

    private void PlaySong(string path)
    {
        Stop();
        ShowMetadata(path);
        UpdatePosition();
        SetActiveTile(path);

        SeekSlider.Value = 0;
        _shownSecond = 0;
        CurrentTimeText.Text = "0:00";
        TotalTimeText.Text = "0:00";

        _player.Open(new Uri(path));
        if (_openCategory is not null)
            ScheduleCategoryRefresh();   // para resaltar la canción que suena en el detalle
        LoadButton.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;
        Play();
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (_playlist.Previous() is { } song)
            PlaySong(song);
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_playlist.Next() is { } song)
            PlaySong(song);
    }

    private void ShuffleButton_Click(object sender, RoutedEventArgs e)
    {
        _playlist.Shuffle = !_playlist.Shuffle;
        UpdateShuffleButton();
        UpdatePosition();
    }

    private void UpdateShuffleButton()
    {
        ShuffleButton.Foreground = _playlist.Shuffle
            ? (Brush)FindResource("Accent")
            : (Brush)FindResource("Muted");
        ShuffleButton.ToolTip = _playlist.Shuffle
            ? "Aleatorio: activado"
            : "Aleatorio: desactivado (orden alfabético)";
    }

    private void UpdatePosition() =>
        PositionText.Text = _playlist.Count == 0 ? "" : $"{_playlist.Position + 1} / {_playlist.Count}";

    private void ShowMetadata(string path)
    {
        var info = SongInfo.Read(path, CoverDecodeSize);
        TitleText.Text = info.Title;
        ArtistText.Text = info.Artist;
        BarTitleText.Text = info.Title;
        BarArtistText.Text = info.Artist;
        CoverBrush.ImageSource = info.Cover;
        CoverPlaceholder.Visibility = info.Cover is null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─────────────────────── Pestañas: artistas y géneros ───────────────────────

    private sealed class SongMeta
    {
        public required string Title { get; init; }
        public required string Artist { get; init; }
        public GenreInfo? Genre { get; set; }
        public bool Classified { get; set; }
        public string GenreName => Genre?.Name ?? Unclassified;
    }

    private void SongsTab_Click(object sender, RoutedEventArgs e) => SetSection(Section.Songs);
    private void ArtistsTab_Click(object sender, RoutedEventArgs e) => SetSection(Section.Artists);
    private void GenresTab_Click(object sender, RoutedEventArgs e) => SetSection(Section.Genres);

    private void SetSection(Section section)
    {
        _section = section;
        _openCategory = null;
        ApplyView();
        RefreshCategories();
        CategoryScroll.ScrollToTop();
    }

    /// <summary>Lee título y artista de las canciones nuevas y las clasifica por género con Deezer.</summary>
    private async Task ClassifyAsync(IReadOnlyList<string> paths)
    {
        var fresh = paths.Distinct(StringComparer.OrdinalIgnoreCase).Where(p => !_meta.ContainsKey(p)).ToList();
        if (fresh.Count == 0)
            return;

        // Solo etiquetas, sin carátula (maxCoverSize 0), y fuera del hilo de la interfaz.
        var tags = await Task.Run(() => fresh.Select(path => (path, info: SongInfo.Read(path, 0))).ToList());
        foreach (var (path, info) in tags)
        {
            _meta[path] = new SongMeta { Title = info.Title, Artist = info.Artist };
            _pending.Enqueue(path);
        }
        ScheduleCategoryRefresh();

        if (_classifying)
            return;   // el bucle que ya está en marcha se encargará también de estas
        _classifying = true;
        int done = 0;
        try
        {
            while (_pending.Count > 0)
            {
                CatalogStatusText.Text = $"Clasificando con Deezer… {done}/{done + _pending.Count}";
                string path = _pending.Peek();
                if (_meta.TryGetValue(path, out var meta))
                {
                    meta.Genre = await _catalog.GetGenreAsync(meta.Artist, meta.Title);
                    meta.Classified = true;
                    ScheduleCategoryRefresh();
                }
                _pending.Dequeue();
                if (++done % 25 == 0)
                    _catalog.Save();
            }
            CatalogStatusText.Text = "";
        }
        catch (CatalogUnavailableException)
        {
            // Las que faltan se quedan en la cola y se reintentan al añadir más canciones.
            CatalogStatusText.Text = $"Sin conexión con Deezer: faltan {_pending.Count} por clasificar";
        }
        finally
        {
            _classifying = false;
            _catalog.Save();
        }
    }

    private void ScheduleCategoryRefresh()
    {
        // Se agrupan los cambios: como mucho un redibujado cada 0,7 s mientras se clasifica.
        if (_section != Section.Songs && !_categoryRefresh.IsEnabled)
            _categoryRefresh.Start();
    }

    private void RefreshCategories()
    {
        if (_section == Section.Songs)
            return;
        if (_openCategory is not null)
        {
            ShowCategoryDetail(_openCategory);
            return;
        }

        bool artists = _section == Section.Artists;
        CategoryHeader.Visibility = Visibility.Collapsed;
        SongListPanel.Visibility = Visibility.Collapsed;
        CardsPanel.Visibility = Visibility.Visible;

        var groups = artists
            ? _meta.Values.GroupBy(m => m.Artist, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            : _meta.Values.Where(m => m.Classified).GroupBy(m => m.GenreName, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(g => g.Key == Unclassified).ThenByDescending(g => g.Count()).ThenBy(g => g.Key);

        CardsPanel.Children.Clear();
        foreach (var group in groups)
        {
            var card = GetCard(group.Key, artists, artists ? null : group.First().Genre?.Picture);
            card.SetCount(group.Count());
            CardsPanel.Children.Add(card);

            // Deezer no tiene foto para los subgéneros ("Pop latino", "Flamenco"): se usa la del
            // artista con más canciones de ese género.
            if (!artists && !card.HasPicture && !card.FallbackRequested && group.Key != Unclassified)
            {
                card.FallbackRequested = true;
                string topArtist = group.GroupBy(m => m.Artist).OrderByDescending(g => g.Count()).First().Key;
                _ = LoadFallbackPictureAsync(card, topArtist);
            }
        }
    }

    private CategoryCard GetCard(string key, bool artist, string? picture)
    {
        var cards = artist ? _artistCards : _genreCards;
        if (!cards.TryGetValue(key, out var card))
        {
            card = new CategoryCard(key, round: artist) { Style = (Style)FindResource("CardButton") };
            card.Click += (_, _) =>
            {
                _openCategory = card.Key;
                CategoryScroll.ScrollToTop();
                RefreshCategories();
            };
            cards.Add(key, card);
            if (artist)
                _ = LoadArtistPictureAsync(card);
        }
        card.SetPicture(picture);
        return card;
    }

    private async Task LoadArtistPictureAsync(CategoryCard card)
    {
        if (card.Key != SongInfo.UnknownArtist)
            card.SetPicture(await _catalog.GetArtistPictureAsync(card.Key));
    }

    private async Task LoadFallbackPictureAsync(CategoryCard card, string artist)
    {
        if (artist != SongInfo.UnknownArtist && !card.HasPicture)
            card.SetPicture(await _catalog.GetArtistPictureAsync(artist));
    }

    /// <summary>Canciones de un artista o de un género; al pulsar una, suena.</summary>
    private void ShowCategoryDetail(string key)
    {
        bool artists = _section == Section.Artists;
        var songs = _meta
            .Where(kv => string.Equals(artists ? kv.Value.Artist : kv.Value.GenreName, key,
                StringComparison.CurrentCultureIgnoreCase) && (artists || kv.Value.Classified))
            .OrderBy(kv => kv.Value.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        CardsPanel.Visibility = Visibility.Collapsed;
        SongListPanel.Visibility = Visibility.Visible;
        CategoryHeader.Visibility = Visibility.Visible;
        CategoryTitleText.Text = key;
        CategorySubtitleText.Text = (artists ? "Artista · " : "Género · ") +
            (songs.Count == 1 ? "1 canción" : $"{songs.Count} canciones");
        CategoryHeaderPictureHost.Child = (artists ? _artistCards : _genreCards).TryGetValue(key, out var card)
            ? card.CreateThumbnail(72)
            : null;

        var accent = (Brush)FindResource("Accent");
        var muted = (Brush)FindResource("Muted");
        SongListPanel.Children.Clear();
        foreach (var (path, meta) in songs)
        {
            bool playing = string.Equals(path, _playlist.Current, StringComparison.OrdinalIgnoreCase);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new TextBlock
            {
                Text = playing ? "\uE995" : "\uE768",   // altavoz / reproducir
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                Foreground = playing ? accent : muted,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var title = new TextBlock
            {
                Text = meta.Title,
                Foreground = playing ? accent : Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var detail = new TextBlock
            {
                Text = artists ? meta.GenreName : meta.Artist,
                Foreground = muted,
                Margin = new Thickness(16, 0, 0, 0),
            };
            Grid.SetColumn(title, 1);
            Grid.SetColumn(detail, 2);
            row.Children.Add(icon);
            row.Children.Add(title);
            row.Children.Add(detail);

            var button = new Button { Style = (Style)FindResource("SongRowButton"), Content = row, Tag = path };
            button.Click += SongRow_Click;
            SongListPanel.Children.Add(button);
        }
    }

    private void SongRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path } && _playlist.JumpTo(path) is { } song)
            PlaySong(song);
    }

    private void CategoryBack_Click(object sender, RoutedEventArgs e)
    {
        _openCategory = null;
        RefreshCategories();
    }

    // ───────────────────────────── Vista mosaico ─────────────────────────────

    private void ViewToggleButton_Click(object sender, RoutedEventArgs e) => SetMosaicView(!_mosaicView);

    private void SetMosaicView(bool mosaic)
    {
        _mosaicView = mosaic;
        ApplyView();
    }

    /// <summary>El mosaico solo se ve en la pestaña Canciones con la vista mosaico elegida.</summary>
    private bool MosaicVisible => _section == Section.Songs && _mosaicView;

    /// <summary>Muestra la vista que toca según la pestaña y la vista elegidas.</summary>
    private void ApplyView()
    {
        bool songs = _section == Section.Songs;
        MosaicView.Visibility = MosaicVisible ? Visibility.Visible : Visibility.Collapsed;
        SingleView.Visibility = songs && !_mosaicView ? Visibility.Visible : Visibility.Collapsed;
        CategoryView.Visibility = songs ? Visibility.Collapsed : Visibility.Visible;
        ViewToggleButton.Visibility = songs ? Visibility.Visible : Visibility.Collapsed;
        // El botón muestra la vista a la que se cambia al pulsarlo.
        ViewToggleIcon.Text = _mosaicView ? "\uE8D6" : "\uE8A9";
        ViewToggleText.Text = _mosaicView ? "Vista original" : "Vista mosaico";
        SongsTab.Tag = songs ? "Active" : null;
        ArtistsTab.Tag = _section == Section.Artists ? "Active" : null;
        GenresTab.Tag = _section == Section.Genres ? "Active" : null;

        MoveControls();
        if (MosaicVisible)
        {
            ScrollToActiveTile();
        }
        else
        {
            // En la vista original no se ven las portadas del mosaico: se libera su memoria.
            foreach (var tile in _tiles.Values)
                tile.ReleaseCover();
        }
    }

    /// <summary>Crea o quita teselas para que el mosaico coincida con la lista de reproducción.</summary>
    private void SyncMosaic()
    {
        var songs = _playlist.Songs;
        var present = new HashSet<string>(songs, StringComparer.OrdinalIgnoreCase);
        var oldSlots = _animator.CaptureSlots();   // para que las que se mueven no salten de sitio
        var added = new List<MosaicTile>();
        foreach (var gone in _tiles.Keys.Where(path => !present.Contains(path)).ToList())
        {
            var removed = _tiles[gone];
            removed.Selected -= Tile_Selected;
            _tiles.Remove(gone);
            if (removed == _activeTile)
            {
                // Devuelve los controles a la vista original antes de descartar la tesela.
                removed.SetActive(false);
                removed.SizeChanged -= ActiveTile_SizeChanged;
                _activeTile = null;
                MoveControls();
            }
        }

        Mosaic.Children.Clear();
        for (int i = 0; i < songs.Count; i++)
        {
            if (!_tiles.TryGetValue(songs[i], out var tile))
            {
                tile = new MosaicTile(songs[i]);
                tile.Selected += Tile_Selected;
                _tiles.Add(songs[i], tile);
                added.Add(tile);
            }
            MosaicPanel.SetSpan(tile, SpanFor(i, tile));
            Mosaic.Children.Add(tile);
        }

        SongCountText.Text = songs.Count == 1 ? "1 canción" : $"{songs.Count} canciones";
        Dispatcher.BeginInvoke(UpdateVisibleCovers, DispatcherPriority.Loaded);
        // Entrada de las nuevas y desplazamiento de las demás en cuanto el mosaico se recoloque, antes
        // de pintarlo: con BeginInvoke se llegaría a ver un fotograma con las teselas ya en su sitio.
        EventHandler? onLayout = null;
        onLayout = (_, _) =>
        {
            Mosaic.LayoutUpdated -= onLayout;
            _animator.Animate(added, oldSlots, _activeTile, _isSeeking);
        };
        Mosaic.LayoutUpdated += onLayout;
    }

    /// <summary>La canción que suena ocupa 2x2; del resto, una de cada seis también es grande.</summary>
    private static int SpanFor(int index, MosaicTile tile) => tile.IsActive || index % 6 == 0 ? 2 : 1;

    private void SetActiveTile(string path)
    {
        if (_activeTile is { } old && old.SongPath != path)
        {
            old.SetActive(false);
            old.SizeChanged -= ActiveTile_SizeChanged;
            MosaicPanel.SetSpan(old, SpanFor(Mosaic.Children.IndexOf(old), old));
        }

        if (!_tiles.TryGetValue(path, out var tile))
            return;
        _activeTile = tile;
        if (!tile.IsActive)
        {
            tile.SetActive(true);
            tile.SizeChanged += ActiveTile_SizeChanged;
            MosaicPanel.SetSpan(tile, 2);
        }

        MoveControls();
        if (MosaicVisible)
            ScrollToActiveTile();
    }

    /// <summary>
    /// Coloca el único juego de controles (barra, tiempos y botones) en la vista original
    /// o dentro de la portada activa del mosaico.
    /// </summary>
    private void MoveControls()
    {
        Decorator target = _section != Section.Songs ? BarControlsHost
            : MosaicVisible && _activeTile?.ControlsHost is { } host ? host
            : OriginalControlsHost;
        if (ControlsPanel.Parent == target)
            return;

        if (ControlsPanel.Parent is Decorator current)
            current.Child = null;
        target.Child = ControlsPanel;
        UpdateControlsWidth();
    }

    private void ActiveTile_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateControlsWidth();

    private void UpdateControlsWidth()
    {
        // Dentro de la portada los controles ocupan todo su ancho; si la portada es más estrecha que
        // los botones, el Viewbox de la tesela los reduce en lugar de recortarlos.
        ControlsPanel.Width = _activeTile is not null && ControlsPanel.Parent == _activeTile.ControlsHost
            ? Math.Max(MinControlsWidth, _activeTile.ActualWidth - 32)
            : double.NaN;
    }

    private void ScrollToActiveTile()
    {
        // Tras recolocar el mosaico, para que la posición de la tesela ya sea la nueva.
        Dispatcher.BeginInvoke(() =>
        {
            _activeTile?.BringIntoView();
            UpdateVisibleCovers();
        }, DispatcherPriority.Loaded);
    }

    private void MosaicView_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateVisibleCovers();

    /// <summary>Carga las portadas visibles (y una pantalla por encima y por debajo) y suelta el resto.</summary>
    private void UpdateVisibleCovers()
    {
        if (!MosaicVisible)
            return;

        double margin = MosaicView.ViewportHeight;
        double top = MosaicView.VerticalOffset - margin;
        double bottom = MosaicView.VerticalOffset + MosaicView.ViewportHeight + margin;

        foreach (MosaicTile tile in Mosaic.Children)
        {
            var slot = LayoutInformation.GetLayoutSlot(tile);
            if (slot.Bottom >= top && slot.Top <= bottom)
                tile.EnsureCover();
            else
                tile.ReleaseCover();
        }
    }

    private void Tile_Selected(object? sender, EventArgs e)
    {
        if (sender is MosaicTile tile && _playlist.JumpTo(tile.SongPath) is { } song)
            PlaySong(song);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
            Pause();
        else
            Play();
    }

    private void Play()
    {
        _player.Play();
        _isPlaying = true;
        PlayPauseButton.Content = PauseIcon;
        _timer.Start();
    }

    private void Pause()
    {
        _player.Pause();
        _isPlaying = false;
        PlayPauseButton.Content = PlayIcon;
        _timer.Stop();
    }

    private void Stop()
    {
        _player.Stop();
        _player.Close();
        _isPlaying = false;
        PlayPauseButton.Content = PlayIcon;
        _timer.Stop();
    }

    private void Player_MediaOpened(object? sender, EventArgs e)
    {
        if (!_player.NaturalDuration.HasTimeSpan)
            return;

        var duration = _player.NaturalDuration.TimeSpan;
        SeekSlider.Maximum = Math.Max(1, duration.TotalSeconds);
        TotalTimeText.Text = Format(duration);
    }

    private void Player_MediaFailed(object? sender, ExceptionEventArgs e)
    {
        // Libera el archivo y detiene el temporizador, que si no seguiría activo sin nada que reproducir.
        Stop();
        MessageBox.Show(this, $"No se pudo reproducir «{TitleText.Text}» y se ha quitado de la lista.\n{e.ErrorException.Message}",
            "Launcherito", MessageBoxButton.OK, MessageBoxImage.Error);

        if (_playlist.Current is { } failed)
            _meta.Remove(failed);
        var next = _playlist.RemoveCurrent();
        SyncMosaic();
        ScheduleCategoryRefresh();
        if (next is not null)
        {
            PlaySong(next);
        }
        else
        {
            PlayerPanel.Visibility = Visibility.Collapsed;
            LoadButton.Visibility = Visibility.Visible;
        }
    }

    private void Player_MediaEnded(object? sender, EventArgs e)
    {
        // Con varias canciones pasa a la siguiente (al acabar la lista vuelve a empezar).
        if (_playlist.Count > 1 && _playlist.Next() is { } next)
        {
            PlaySong(next);
            return;
        }

        Pause();
        _player.Position = TimeSpan.Zero;
        UpdateProgress();
    }

    private void Timer_Tick(object? sender, EventArgs e) => UpdateProgress();

    protected override void OnStateChanged(EventArgs e)
    {
        // Con la ventana minimizada no hace falta refrescar la barra de progreso.
        if (WindowState == WindowState.Minimized)
            _timer.Stop();
        else if (_isPlaying)
        {
            UpdateProgress();
            _timer.Start();
        }
        base.OnStateChanged(e);
    }

    private void UpdateProgress()
    {
        if (!_isSeeking)
            SeekSlider.Value = _player.Position.TotalSeconds;
    }

    private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isSeeking = true;
        // Una portada animada movería la barra bajo el ratón: se deja quieta antes de arrastrar.
        _animator.Finish(_activeTile);

        // Al pulsar en la pista (fuera del círculo) el Slider ya ha saltado a ese punto; además se
        // empieza a arrastrar el círculo para poder seguir moviéndolo sin soltar el botón.
        if (SeekSlider.Template.FindName("PART_Track", SeekSlider) is Track { Thumb: { IsMouseOver: false } thumb })
        {
            // Coloca el círculo en su nueva posición antes de arrastrarlo; si no, el primer
            // movimiento se calcularía desde la posición antigua y la barra daría un salto.
            SeekSlider.UpdateLayout();
            thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
            {
                RoutedEvent = MouseLeftButtonDownEvent,
                Source = thumb,
            });
        }
    }

    private void SeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndSeek();

    // Por si se pierde el ratón a mitad de arrastre (p. ej. Alt+Tab): sin esto la barra se quedaría congelada.
    private void SeekSlider_LostMouseCapture(object sender, MouseEventArgs e) => EndSeek();

    private void EndSeek()
    {
        // Al soltar llegan tanto el MouseUp como el LostMouseCapture: solo se salta una vez.
        if (!_isSeeking)
            return;
        _isSeeking = false;

        // El reproductor solo salta al soltar, nunca durante el arrastre.
        _player.Position = TimeSpan.FromSeconds(SeekSlider.Value);

        // Reinicia el temporizador para que el siguiente tick llegue cuando el salto ya está hecho
        // y la barra no rebote un instante a la posición antigua.
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // El valor cambia con cada tick y con cada píxel arrastrado; el texto solo se rehace
        // (creando una cadena nueva) cuando cambia el segundo que se muestra.
        int second = (int)e.NewValue;
        if (second == _shownSecond)
            return;
        _shownSecond = second;
        CurrentTimeText.Text = Format(TimeSpan.FromSeconds(second));
    }

    private static string Format(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    protected override void OnClosed(EventArgs e)
    {
        // MediaPlayer y DispatcherTimer no implementan IDisposable (no admiten using):
        // se liberan explícitamente parándolos, cerrando el audio y soltando los eventos.
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _player.MediaOpened -= Player_MediaOpened;
        _player.MediaEnded -= Player_MediaEnded;
        _player.MediaFailed -= Player_MediaFailed;
        _player.Close();
        _categoryRefresh.Stop();
        _catalog.Save();
        base.OnClosed(e);
    }
}
