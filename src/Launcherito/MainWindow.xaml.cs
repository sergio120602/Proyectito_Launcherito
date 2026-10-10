using System.ComponentModel;
using System.IO;
using Microsoft.Web.WebView2.Core;
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
    // Fondo del botón Editar fuera del modo edición (el mismo que el resto de píldoras).
    private static readonly Brush EditIdle = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1C));

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
    private readonly MosaicEditor _editor;
    private readonly MosaicArrangement _arrangement = new();
    // Modo edición del mosaico: con él activo las portadas se mueven y cambian de tamaño. Todo lo
    // demás (botones, cursor, tiradores, el editor) se pone según esta variable en SetEditing.
    private bool _isEditing;

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
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    private readonly Dictionary<string, string> _localSongs = new();   // clave artista|título → .mp3 cargado
    private readonly SongLibrary _library = new();                     // «Tus canciones»: todas las que se han cargado
    private readonly DispatcherTimer _spotifyStatusClear = new() { Interval = TimeSpan.FromSeconds(12) };

    // Vídeo de YouTube de la canción que suena, manejado con los controles del programa: a la vista
    // en la vista original y, para las de Spotify, oculto en las demás. Mientras _audioFromVideo es
    // true el sonido sale del vídeo y el .mp3 está cerrado.
    private YouTubeVideo? _songVideo;
    private Decorator? _songVideoHost;   // SongVideoHost (a la vista) o HiddenVideoHost
    private string? _videoFailedFor;     // .mp3 sin vídeo: suena el archivo y no se reintenta
    private int _skippedInARow;          // canciones de Spotify sin vídeo saltadas seguidas
    private bool _songVideoPlaying;      // el vídeo actual ya ha empezado a sonar
    private bool _songVideoStarted;      // ya se ha cargado la página (luego solo se cambian los vídeos)
    private int _songVideoVersion;       // invalida búsquedas de vídeo de una canción que ya no suena
    private bool _audioFromVideo;
    private double _videoTime;           // segundo actual y duración del vídeo, según avisa la página
    private double _videoDuration;
    private TimeSpan? _pendingSeek;      // posición que se aplica al .mp3 en cuanto se abre

    private int _metadataVersion;     // invalida lecturas de etiquetas de una canción que ya no suena
    private string? _coverShownFor;   // canción cuya carátula grande está cargada en la vista original

    public MainWindow()
    {
        InitializeComponent();
        // La versión sale de <Version> en el .csproj, así el título siempre coincide con el .exe.
        Title = $"Launcherito {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        _animator = new TileAnimator(MosaicView, Mosaic);
        _editor = new MosaicEditor(MosaicView, Mosaic, _animator);
        _editor.Changed += Editor_Changed;

        _player.MediaOpened += Player_MediaOpened;
        _player.MediaEnded += Player_MediaEnded;
        _player.MediaFailed += Player_MediaFailed;
        _timer.Tick += Timer_Tick;
        // handledEventsToo: al pulsar en la pista, el Slider marca el clic como gestionado
        // (IsMoveToPointEnabled) y un manejador normal declarado en XAML no llegaría a ejecutarse.
        SeekSlider.AddHandler(PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(SeekSlider_PreviewMouseLeftButtonDown), handledEventsToo: true);
        UpdateShuffleButton();
        _spotifyStatusClear.Tick += (_, _) =>
        {
            _spotifyStatusClear.Stop();
            SpotifyStatusText.Text = "";
        };
        _categoryRefresh.Tick += (_, _) =>
        {
            _categoryRefresh.Stop();
            RefreshCategories();
        };
        SetMosaicView(false);
        UpdateLibraryButtons();

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

    // ───────────────────────────── Listas de Spotify ─────────────────────────────

    private void SpotifyButton_Click(object sender, RoutedEventArgs e)
    {
        // Si se acaba de copiar un enlace de Spotify, ya aparece pegado.
        try
        {
            if (SpotifyLibrary.ParseLink(Clipboard.GetText()) is not null)
                SpotifyLinkBox.Text = Clipboard.GetText().Trim();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Portapapeles ocupado por otro programa: se pega a mano.
        }
        SpotifyDialogStatus.Text = "";
        SpotifyOverlay.Visibility = Visibility.Visible;
        SpotifyLinkBox.Focus();
        SpotifyLinkBox.SelectAll();
    }

    private void SpotifyLinkBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _ = AddSpotifyListAsync();
        else if (e.Key == Key.Escape)
            SpotifyCancel_Click(sender, e);
    }

    private void SpotifyAdd_Click(object sender, RoutedEventArgs e) => _ = AddSpotifyListAsync();

    private void SpotifyCancel_Click(object sender, RoutedEventArgs e)
    {
        if (SpotifyAddButton.IsEnabled)   // mientras se lee la lista no se cierra
            SpotifyOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Lee la lista de Spotify y añade sus canciones al mosaico. Las que ya están en un .mp3 cargado no
    /// se repiten (suena el .mp3 entero); las demás son accesos directos a su vídeo de YouTube.
    /// </summary>
    private async Task AddSpotifyListAsync()
    {
        if (!SpotifyAddButton.IsEnabled)
            return;
        if (SpotifyLibrary.ParseLink(SpotifyLinkBox.Text) is not { } link)
        {
            SpotifyDialogStatus.Text = "Ese enlace no es de una lista ni de un álbum de Spotify.";
            return;
        }

        SpotifyList list;
        SpotifyAddButton.IsEnabled = false;
        SpotifyDialogStatus.Text = "Leyendo la lista en Spotify…";
        try
        {
            list = await SpotifyLibrary.LoadAsync(link.Type, link.Id);
        }
        catch (SpotifyImportException ex)
        {
            SpotifyDialogStatus.Text = ex.Message;
            return;
        }
        finally
        {
            SpotifyAddButton.IsEnabled = true;
        }

        var keys = new List<string>();
        var saved = new List<SavedSong>();
        int owned = 0;
        foreach (var track in list.Tracks)
        {
            if (_localSongs.ContainsKey(SpotifyLibrary.MatchKey(track.Artists, track.Title)))
                owned++;
            else
            {
                keys.Add(track.Key);
                saved.Add(SavedSong.FromTrack(track));
            }
        }
        _library.Add(saved);
        int added = AddToPlaylist(keys);

        var summary = new List<string> { $"«{list.Name}»: {Songs(added)} añadida{(added == 1 ? "" : "s")}" };
        if (owned > 0)
            summary.Add($"{owned} ya la{(owned == 1 ? "" : "s")} tenías en .mp3");
        if (keys.Count > added)
            summary.Add($"{keys.Count - added} ya estaba{(keys.Count - added == 1 ? "" : "n")}");
        string text = list.Tracks.Count == 0 ? $"«{list.Name}» no tiene canciones." : string.Join("  ·  ", summary);

        // Si no se ha añadido nada el panel sigue abierto con la explicación; si no, se cierra.
        if (added == 0)
        {
            SpotifyDialogStatus.Text = text;
            return;
        }
        SpotifyOverlay.Visibility = Visibility.Collapsed;
        SpotifyLinkBox.Text = "";
        // En la barra superior cabe poco: el resumen corto y el detalle al pasar el ratón.
        SpotifyStatusText.Text = $"Spotify: +{Songs(added)}";
        SpotifyStatusText.ToolTip = text;
        _spotifyStatusClear.Stop();
        _spotifyStatusClear.Start();
    }

    private static string Songs(int count) => count == 1 ? "1 canción" : $"{count} canciones";

    /// <summary>
    /// Quita las canciones de Spotify que ya están en un .mp3 cargado: suena el archivo, que está
    /// entero. La que está sonando se deja hasta que se cambie de canción.
    /// </summary>
    private void RemoveSpotifyDuplicates()
    {
        string? current = _playlist.Current;
        var duplicates = _playlist.Songs
            .Where(key => key != current && SpotifyLibrary.TryGet(key, out var track) &&
                          _localSongs.ContainsKey(SpotifyLibrary.MatchKey(track.Artists, track.Title)))
            .ToList();
        if (_playlist.Remove(duplicates) == 0)
            return;
        foreach (var key in duplicates)
            _meta.Remove(key);
        _library.Remove(duplicates);   // tampoco hace falta guardarla: ya está guardado el .mp3
        SyncMosaic();
        UpdatePosition();
        ScheduleCategoryRefresh();
    }

    /// <summary>Pulsar una canción: suena (las de Spotify, con su vídeo de YouTube).</summary>
    private void SelectSong(string path)
    {
        if (_playlist.JumpTo(path) is { } song)
            PlaySong(song);
    }

    private void ShowSpotifyStatus(string text)
    {
        SpotifyStatusText.Text = text;
        SpotifyStatusText.ToolTip = null;
        _spotifyStatusClear.Stop();
        _spotifyStatusClear.Start();
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

        // Se guardan en «Tus canciones»; el título y el artista se apuntan al leer sus etiquetas.
        _library.Add(valid.Select(path => new SavedSong { Key = path, Title = SongInfo.FallbackTitle(path) }));
        AddToPlaylist(valid);
    }

    // ─────────────────────── Tus canciones y Borrar canciones ───────────────────────

    /// <summary>Activa los botones según lo guardado; en la barra superior, Tus canciones solo si falta alguna por cargar.</summary>
    private void UpdateLibraryButtons()
    {
        int saved = _library.Count;
        LibraryButton.IsEnabled = saved > 0;
        DeleteSongsButton.IsEnabled = saved > 0;
        LibraryCountText.Text = saved > 0 ? $"· {saved}" : "";
        LibraryButton.ToolTip = saved > 0
            ? $"Cargar {Songs(saved)} guardada{(saved == 1 ? "" : "s")}"
            : "Aún no hay canciones guardadas: se guardan solas al cargarlas";
        DeleteSongsButton.ToolTip = saved > 0 ? null : "No hay canciones guardadas";

        int notLoaded = UnloadedSongs().Count;
        TopLibraryButton.Content = $"♫  +{notLoaded}";
        TopLibraryButton.ToolTip = $"Tus canciones: cargar {(notLoaded == 1 ? "la que falta" : $"las {notLoaded} que faltan")}";
        TopLibraryButton.Visibility = notLoaded > 0 ? Visibility.Visible : Visibility.Collapsed;
        TopDeleteButton.Visibility = saved > 0 ? Visibility.Visible : Visibility.Collapsed;
        // En el menú principal, si hay algo cargado, se puede volver al reproductor.
        BackToPlayerButton.Visibility = _playlist.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        BackToPlayerText.Text = _isPlaying ? $"Volver al reproductor  ·  {TitleText.Text}" : "Volver al reproductor";
    }

    /// <summary>Guardadas que no están en el mosaico y se pueden cargar (los .mp3, solo si siguen en su sitio).</summary>
    private List<SavedSong> UnloadedSongs() =>
        _library.Songs.Where(song => !_tiles.ContainsKey(song.Key) && (song.IsSpotify || File.Exists(song.Key))).ToList();

    /// <summary>Carga todas las canciones guardadas que aún no están en el mosaico.</summary>
    private void LibraryButton_Click(object sender, RoutedEventArgs e)
    {
        var songs = new List<string>();
        foreach (var song in UnloadedSongs())
        {
            if (song.IsSpotify)
                SpotifyLibrary.Register(song.ToTrack());
            songs.Add(song.Key);
        }
        if (songs.Count > 0)
            AddToPlaylist(songs);
        ShowPlayer();   // aunque ya estuvieran todas cargadas

        int missing = _library.Songs.Count(song => !song.IsSpotify && !File.Exists(song.Key));
        if (missing > 0)
        {
            MessageBox.Show(this,
                $"{(missing == 1 ? "1 canción guardada ya no está" : $"{missing} canciones guardadas ya no están")} en su sitio " +
                "(¿se ha movido o borrado el archivo?). Puedes quitarlas con «Borrar canciones».",
                "Launcherito", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void DeleteSongsButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteList.Children.Clear();
        var muted = (Brush)FindResource("Muted");
        foreach (var song in _library.Songs)
        {
            bool missing = !song.IsSpotify && !File.Exists(song.Key);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
            title.Inlines.Add(song.Title);
            if (song.Artist.Length > 0)
                title.Inlines.Add(new System.Windows.Documents.Run("  ·  " + song.Artist) { Foreground = muted });
            // Marca a la derecha: de dónde viene, o que el archivo ya no está.
            var tag = new TextBlock
            {
                Text = missing ? "no se encuentra" : song.IsSpotify ? "Spotify" : ".mp3",
                FontSize = 12,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = missing ? (Brush)FindResource("DangerRed")
                    : song.IsSpotify ? (Brush)FindResource("SpotifyGreen") : muted,
            };
            Grid.SetColumn(tag, 1);
            row.Children.Add(title);
            row.Children.Add(tag);

            var check = new CheckBox
            {
                Style = (Style)FindResource("SongCheck"),
                Content = row,
                Tag = song.Key,
                ToolTip = song.IsSpotify ? null : song.Key,   // la ruta del .mp3
            };
            check.Checked += DeleteCheck_Changed;
            check.Unchecked += DeleteCheck_Changed;
            DeleteList.Children.Add(check);
        }
        UpdateDeleteButtons();
        DeleteOverlay.Visibility = Visibility.Visible;
        DeleteOverlay.Focus();
    }

    private IEnumerable<CheckBox> DeleteChecks => DeleteList.Children.OfType<CheckBox>();

    private void DeleteCheck_Changed(object sender, RoutedEventArgs e) => UpdateDeleteButtons();

    private void UpdateDeleteButtons()
    {
        int marked = DeleteChecks.Count(check => check.IsChecked == true);
        bool all = marked > 0 && marked == DeleteList.Children.Count;
        ConfirmDeleteButton.Content = marked == 0 ? "Borrar" : $"Borrar {Songs(marked)}";
        ConfirmDeleteButton.IsEnabled = marked > 0;
        SelectAllButton.Content = all ? "Quitar la selección" : "Seleccionar todas";
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        bool select = DeleteChecks.Any(check => check.IsChecked != true);
        foreach (var check in DeleteChecks)
            check.IsChecked = select;
    }

    private void DeleteCancel_Click(object sender, RoutedEventArgs e) => CloseDeletePanel();

    private void DeleteOverlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            CloseDeletePanel();
    }

    private void CloseDeletePanel()
    {
        DeleteOverlay.Visibility = Visibility.Collapsed;
        DeleteList.Children.Clear();   // las filas se rehacen cada vez que se abre
    }

    private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        var keys = DeleteChecks.Where(check => check.IsChecked == true).Select(check => (string)check.Tag).ToList();
        if (keys.Count == 0)
            return;
        string question = keys.Count == _library.Count
            ? "¿Borrar todas tus canciones guardadas?"
            : $"¿Borrar {Songs(keys.Count)} de «Tus canciones»?";
        if (MessageBox.Show(this, question + "\nLos archivos .mp3 de tu PC no se borran.", "Borrar canciones",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _library.Remove(keys);
        CloseDeletePanel();
        RemoveFromSession(keys);
        UpdateLibraryButtons();
    }

    /// <summary>
    /// Quita del mosaico y de la lista las canciones borradas. Si sonaba una de ellas, pasa a la
    /// siguiente que quede; si no queda ninguna, se vuelve a la pantalla inicial.
    /// </summary>
    private void RemoveFromSession(IReadOnlyList<string> keys)
    {
        var gone = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        bool currentGone = _playlist.Current is { } current && gone.Contains(current);
        bool wasPlaying = _isPlaying;
        if (currentGone)
            Stop();

        foreach (var key in gone)
            _meta.Remove(key);
        foreach (var match in _localSongs.Where(kv => gone.Contains(kv.Value)).Select(kv => kv.Key).ToList())
            _localSongs.Remove(match);
        _playlist.Remove(gone);         // todas menos la que suena…
        if (currentGone)
            _playlist.RemoveCurrent();  // …que se quita aparte
        SyncMosaic();
        ScheduleCategoryRefresh();
        UpdatePosition();

        if (_playlist.Count == 0)
        {
            DisposeSongVideo();
            SetSongTexts("", "");
            PlayerPanel.Visibility = Visibility.Collapsed;
            StartPanel.Visibility = Visibility.Visible;
        }
        else if (currentGone)
        {
            if (_playlist.Current is { } next)
            {
                PlaySong(next);
                if (!wasPlaying)
                    Pause();
            }
        }
    }

    /// <summary>Añade canciones (.mp3 o de Spotify) a la lista y al mosaico. Devuelve cuántas son nuevas.</summary>
    private int AddToPlaylist(IReadOnlyList<string> songs)
    {
        int before = _playlist.Count;
        bool wasEmpty = _playlist.Count == 0;
        int added = _playlist.Add(songs);
        SyncMosaic();
        _ = ClassifyAsync(songs);

        // Al pasar de una canción a varias se cambia sola a la vista mosaico.
        if (before <= 1 && _playlist.Count > 1 && !_mosaicView)
            SetMosaicView(true);

        // Si ya estaba sonando algo, las nuevas canciones se añaden a la lista sin interrumpirla.
        if (wasEmpty && _playlist.Current is { } first)
            PlaySong(first);
        else
            UpdatePosition();
        ShowPlayer();   // también si se han añadido desde el menú principal
        return added;
    }

    private void PlaySong(string path)
    {
        Stop();
        _ = ShowMetadataAsync(path);
        UpdatePosition();
        SetActiveTile(path);

        SeekSlider.Value = 0;
        _shownSecond = 0;
        CurrentTimeText.Text = "0:00";
        TotalTimeText.Text = "0:00";

        // El reproductor se muestra antes: el vídeo necesita estar a la vista (con tamaño) para arrancar.
        StartPanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;

        // Suena el vídeo de YouTube en la vista original y siempre en las de Spotify; si no, el .mp3.
        _videoFailedFor = null;
        if (WantedVideoHost(path) is { } host)
            StartSongVideo(path, 0, host);
        else
            _player.Open(new Uri(path));
        if (_openCategory is not null)
            ScheduleCategoryRefresh();   // para resaltar la canción que suena en el detalle
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

    /// <summary>
    /// Pone al momento el título y el artista que ya se conocen y luego lee las etiquetas fuera del
    /// hilo de la interfaz, para que la ventana no se congele al cambiar de canción. La carátula
    /// grande solo se decodifica si se ve la vista original.
    /// </summary>
    private async Task ShowMetadataAsync(string path)
    {
        int version = ++_metadataVersion;
        bool withCover = SingleViewVisible;

        if (_meta.TryGetValue(path, out var known))
            SetSongTexts(known.Title, known.Artist);
        else
            SetSongTexts(SongInfo.FallbackTitle(path), "");
        if (_coverShownFor != path)
            ShowCover(null, null);

        var info = await Task.Run(() => SongInfo.Read(path, withCover ? CoverDecodeSize : 0));
        if (version != _metadataVersion)
            return;   // mientras se leía ya se ha pasado a otra canción
        SetSongTexts(info.Title, info.Artist);
        if (withCover)
            ShowCover(info.Cover, path);
    }

    private void SetSongTexts(string title, string artist)
    {
        TitleText.Text = title;
        ArtistText.Text = artist;
        BarTitleText.Text = title;
        BarArtistText.Text = artist;
    }

    private void ShowCover(ImageSource? cover, string? path)
    {
        CoverBrush.ImageSource = cover;
        CoverPlaceholder.Visibility = cover is null ? Visibility.Visible : Visibility.Collapsed;
        _coverShownFor = path;
    }

    // ─────────────────────── Vídeo de YouTube de la canción que suena ───────────────────────
    // En la vista original se ve el vídeo de la canción que suena en lugar de la carátula, y el
    // sonido sale de él. Las canciones de Spotify no tienen .mp3: suenan siempre de su vídeo, y fuera
    // de la vista original el vídeo va en un hueco invisible (HiddenVideoHost) y solo se oye.

    private enum VideoProblem { Offline, NotEmbeddable, NoWebView }

    /// <summary>
    /// Dónde va el vídeo de la canción: a la vista en la vista original; en el hueco invisible si es
    /// de Spotify; en ningún sitio si es un .mp3 fuera de la vista original (suena el archivo).
    /// </summary>
    private Decorator? WantedVideoHost(string path) =>
        SingleViewVisible ? SongVideoHost : SpotifyLibrary.IsSpotify(path) ? HiddenVideoHost : null;

    /// <summary>
    /// Pone el vídeo de YouTube de la canción en <paramref name="host"/>, empezando en el segundo
    /// <paramref name="start"/>. El sonido sale del vídeo y lo manejan los controles del programa.
    /// Mientras se busca se ve la carátula.
    /// </summary>
    private void StartSongVideo(string path, double start, Decorator host)
    {
        // El navegador se reutiliza de una canción a otra (y se cambia de sitio si hace falta).
        MoveSongVideo(host);
        int version = ++_songVideoVersion;
        _audioFromVideo = true;
        _songVideoPlaying = false;
        _videoTime = start;
        _videoDuration = 0;
        ShowSongVideo(false);
        SetVideoStatus(host == SongVideoHost ? "Buscando el vídeo en YouTube…" : null);

        if (_songVideo is null)
        {
            var video = new YouTubeVideo(controlled: true);
            video.Playing += (_, _) =>
            {
                if (_songVideo != video || !_audioFromVideo)
                    return;
                _skippedInARow = 0;
                _songVideoPlaying = true;
                if (_songVideoHost == SongVideoHost)
                    ShowSongVideo(true);
            };
            video.Ended += (_, _) =>
            {
                if (_songVideo == video && _audioFromVideo)
                    SongEnded();
            };
            video.Progress += (_, progress) =>
            {
                if (_songVideo != video || !_audioFromVideo)
                    return;
                _videoTime = progress.Time;
                if (progress.Duration > 0 && Math.Abs(progress.Duration - _videoDuration) > 0.5)
                {
                    _videoDuration = progress.Duration;
                    SeekSlider.Maximum = Math.Max(1, progress.Duration);
                    TotalTimeText.Text = Format(TimeSpan.FromSeconds(progress.Duration));
                }
            };
            video.Unavailable += (_, _) =>
            {
                if (_songVideo == video && _audioFromVideo)
                    VideoFailed(VideoProblem.NotEmbeddable);
            };
            _songVideo = video;
            _songVideoHost = host;
            _songVideoStarted = false;
            host.Child = video.View;
        }
        if (_songVideoStarted)
            _songVideo.Hold();   // el vídeo anterior no vuelve a sonar mientras se busca el nuevo
        _ = LoadSongVideoAsync(path, start, version);
    }

    private async Task LoadSongVideoAsync(string path, double start, int version)
    {
        var (title, artist) = SpotifyLibrary.TryGet(path, out var track) ? (track.Title, track.MainArtist)
            : _meta.TryGetValue(path, out var meta) ? (meta.Title, meta.Artist)
            : await Task.Run(() => SongInfo.Read(path, 0) is var info ? (info.Title, info.Artist) : default);
        var ids = await YouTubeLinks.FindVideosAsync(path, artist, title);
        if (version != _songVideoVersion || _songVideo is not { } video)
            return;   // mientras se buscaba se ha cambiado de canción o de vista
        if (ids.Count == 0)
        {
            VideoFailed(VideoProblem.Offline);
            return;
        }

        try
        {
            if (_songVideoStarted)
            {
                video.Load(ids, start);
            }
            else
            {
                _songVideoStarted = true;
                await video.StartAsync(ids, start);
            }
        }
        catch (WebView2RuntimeNotFoundException)
        {
            if (version == _songVideoVersion)
                VideoFailed(VideoProblem.NoWebView);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // Se ha cerrado el vídeo mientras arrancaba el navegador interno.
        }
    }

    /// <summary>
    /// La canción no tiene vídeo que se pueda ver. Si es un .mp3, suena el archivo. Si es de Spotify
    /// no hay nada más que poner: sin conexión se para; si es solo esa, se pasa a la siguiente.
    /// </summary>
    private void VideoFailed(VideoProblem problem)
    {
        if (_playlist.Current is not { } path)
            return;
        if (!SpotifyLibrary.IsSpotify(path))
        {
            _videoFailedFor = path;   // no se vuelve a intentar al cambiar de vista
            SwitchToMp3(problem switch
            {
                VideoProblem.Offline => "Sin conexión con YouTube: suena el .mp3",
                VideoProblem.NoWebView => "Este Windows no puede mostrar vídeos: suena el .mp3",
                _ => "Esta canción no tiene vídeo que se pueda ver aquí: suena el .mp3",
            });
            if (problem == VideoProblem.NoWebView)
                DisposeSongVideo();
            return;
        }

        SetVideoStatus(null);
        string title = SongInfo.FallbackTitle(path);
        if (problem != VideoProblem.NotEmbeddable)
        {
            ShowSpotifyStatus(problem == VideoProblem.Offline
                ? "Sin conexión con YouTube: las canciones de Spotify no pueden sonar"
                : "Este Windows no puede mostrar vídeos: las canciones de Spotify no pueden sonar");
            Pause();
            return;
        }
        // Se salta, pero sin dar vueltas a la lista si ninguna tiene vídeo.
        if (++_skippedInARow < _playlist.Count && _playlist.Next() is { } next)
        {
            ShowSpotifyStatus($"«{title}» no tiene vídeo que se pueda ver aquí: pasa a la siguiente");
            PlaySong(next);
        }
        else
        {
            _skippedInARow = 0;
            ShowSpotifyStatus($"«{title}» no tiene vídeo que se pueda ver aquí");
            Pause();
        }
    }

    /// <summary>
    /// Al cambiar de vista el sonido pasa a donde toca (el vídeo a la vista, el vídeo oculto o el
    /// .mp3) y sigue por el mismo segundo.
    /// </summary>
    private void UpdateSongSource()
    {
        if (_playlist.Current is not { } path)
            return;
        var wanted = WantedVideoHost(path);
        if (_audioFromVideo ? _songVideoHost == wanted : wanted is null || _videoFailedFor == path)
        {
            if (wanted is null && !_audioFromVideo)
                DisposeSongVideo();   // suena el .mp3: el navegador no hace falta
            return;
        }

        // De la vista original al hueco oculto o al revés: el mismo vídeo cambia de sitio sin cortarse.
        if (_audioFromVideo && wanted is not null)
        {
            MoveSongVideo(wanted);
            ShowSongVideo(wanted == SongVideoHost && _songVideoPlaying);
            if (wanted == SongVideoHost && !_songVideoPlaying)
                SetVideoStatus("Buscando el vídeo en YouTube…");
            return;
        }

        double at = _audioFromVideo ? _videoTime : _pendingSeek?.TotalSeconds ?? _player.Position.TotalSeconds;
        bool playing = _isPlaying;
        if (_audioFromVideo)
        {
            _audioFromVideo = false;
            DisposeSongVideo();
        }
        else
        {
            _player.Stop();
            _player.Close();
            _pendingSeek = null;
        }

        if (wanted is null)
        {
            _pendingSeek = TimeSpan.FromSeconds(at);
            _player.Open(new Uri(path));
            if (playing)
                _player.Play();
        }
        else
        {
            StartSongVideo(path, at, wanted);
            if (playing)
                _songVideo!.Play();
            else
                _songVideo!.Pause();
        }
    }

    /// <summary>El sonido de un .mp3 vuelve al archivo, por donde iba el vídeo (el vídeo se queda parado).</summary>
    private void SwitchToMp3(string? reason)
    {
        if (!_audioFromVideo || _playlist.Current is not { } path)
            return;
        if (reason is not null)
            ShowSpotifyStatus(reason);
        _songVideoVersion++;
        _audioFromVideo = false;
        _songVideo?.Pause();
        ShowSongVideo(false);
        SetVideoStatus(null);

        _pendingSeek = TimeSpan.FromSeconds(_videoTime);
        _player.Open(new Uri(path));
        if (_isPlaying)
            _player.Play();
    }

    /// <summary>Muestra el vídeo (marco 16:9) o la carátula (marco cuadrado) en la vista original.</summary>
    private void ShowSongVideo(bool visible)
    {
        SongVideoHost.Opacity = visible ? 1 : 0;
        CoverFrame.Width = visible ? 560 : 400;
        CoverFrame.Height = visible ? 315 : 400;
        if (visible)
            SetVideoStatus(null);
    }

    private void SetVideoStatus(string? text)
    {
        SongVideoStatusText.Text = text ?? "";
        SongVideoStatus.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Pasa el navegador del vídeo (si lo hay) a otro hueco sin cerrarlo.</summary>
    private void MoveSongVideo(Decorator host)
    {
        if (_songVideo is not { } video || _songVideoHost == host)
            return;
        if (_songVideoHost is { } old)
            old.Child = null;
        _songVideoHost = host;
        host.Child = video.View;
        if (host != SongVideoHost)
        {
            ShowSongVideo(false);
            SetVideoStatus(null);
        }
    }

    /// <summary>Libera el navegador del vídeo. Quien lo llama decide antes de dónde sale el sonido.</summary>
    private void DisposeSongVideo()
    {
        if (_songVideo is not { } video)
            return;
        _songVideoVersion++;
        _songVideo = null;
        _songVideoStarted = false;
        if (_songVideoHost is { } host)
            host.Child = null;
        _songVideoHost = null;
        video.Dispose();
        ShowSongVideo(false);
        SetVideoStatus(null);
    }

    // Esquinas redondeadas también para el vídeo.
    private void SongVideoHost_SizeChanged(object sender, SizeChangedEventArgs e) =>
        SongVideoHost.Clip = new RectangleGeometry(new Rect(e.NewSize), 16, 16);

    // ─────────────────────────── Menú principal y buscador ───────────────────────────

    /// <summary>Vuelve a la pantalla inicial sin parar la música; desde allí se vuelve con «Volver al reproductor».</summary>
    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        SetEditing(false);
        SearchPopup.IsOpen = false;
        PlayerPanel.Visibility = Visibility.Collapsed;
        StartPanel.Visibility = Visibility.Visible;
        UpdateLibraryButtons();
    }

    private void BackToPlayerButton_Click(object sender, RoutedEventArgs e) => ShowPlayer();

    /// <summary>Muestra el reproductor si hay algo cargado.</summary>
    private void ShowPlayer()
    {
        if (_playlist.Count == 0)
            return;
        StartPanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;
    }

    // Ctrl+F lleva al buscador.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && PlayerPanel.IsVisible)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateSearch();

    private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (SearchBox.Text.Length > 0)
            UpdateSearch();
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Enter elige la primera.
            if (SearchResults.Children.OfType<Button>().FirstOrDefault() is { Tag: string key })
                PickSearchResult(key);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Text = "";
            e.Handled = true;
        }
    }

    /// <summary>
    /// Busca por el nombre de la canción (y por el artista) sin distinguir mayúsculas ni tildes, entre
    /// las cargadas y las guardadas en «Tus canciones». Primero las que empiezan por lo escrito.
    /// </summary>
    private void UpdateSearch()
    {
        string query = SearchBox.Text.Trim();
        SearchResults.Children.Clear();
        if (query.Length == 0)
        {
            SearchPopup.IsOpen = false;
            return;
        }

        var compare = System.Globalization.CultureInfo.CurrentCulture.CompareInfo;
        const System.Globalization.CompareOptions Loose =
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace;
        var matches = SearchCandidates()
            .Select(song =>
            {
                int at = compare.IndexOf(song.Title, query, Loose, out int length);
                return (song.Key, song.Title, song.Artist, At: at, Length: length,
                        InArtist: compare.IndexOf(song.Artist, query, Loose) >= 0);
            })
            .Where(match => match.At >= 0 || match.InArtist)
            .OrderBy(match => match.At == 0 ? 0 : match.At > 0 ? 1 : 2)
            .ThenBy(match => match.Title, StringComparer.CurrentCultureIgnoreCase)
            // Un .mp3 guardado que ya no está en el PC no se puede poner.
            .Where(match => _tiles.ContainsKey(match.Key) || SpotifyLibrary.IsSpotify(match.Key) || File.Exists(match.Key))
            .Take(MaxSearchResults)
            .ToList();

        if (matches.Count == 0)
        {
            SearchResults.Children.Add(new TextBlock
            {
                Text = $"Ninguna canción se llama «{query}»",
                Foreground = (Brush)FindResource("Muted"),
                Margin = new Thickness(14, 10, 14, 10),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        foreach (var match in matches)
        {
            var row = CreateSongRow(match.Key, match.Title, match.Artist,
                match.At >= 0 ? (match.At, match.Length) : null);
            row.Click -= SongRow_Click;
            row.Click += (_, _) => PickSearchResult(match.Key);
            SearchResults.Children.Add(row);
        }
        SearchPopup.IsOpen = true;
    }

    private const int MaxSearchResults = 50;

    /// <summary>Las canciones del mosaico y, además, las guardadas que aún no están cargadas.</summary>
    private IEnumerable<(string Key, string Title, string Artist)> SearchCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _tiles.Keys)
        {
            seen.Add(key);
            if (_meta.TryGetValue(key, out var meta))
                yield return (key, meta.Title, meta.Artist);
            else if (SpotifyLibrary.TryGet(key, out var track))
                yield return (key, track.Title, track.Artists);
            else
                yield return (key, SongInfo.FallbackTitle(key), "");
        }
        foreach (var song in _library.Songs)
        {
            if (seen.Add(song.Key))
                yield return (song.Key, song.Title, song.Artist);
        }
    }

    /// <summary>Pone la canción elegida en el buscador; si era una guardada sin cargar, la carga antes.</summary>
    private void PickSearchResult(string key)
    {
        SearchBox.Text = "";   // cierra la lista
        Keyboard.ClearFocus();
        if (!_tiles.ContainsKey(key))
        {
            if (SpotifyLibrary.IsSpotify(key) && _library.Songs.FirstOrDefault(song => song.Key == key) is { } saved)
                SpotifyLibrary.Register(saved.ToTrack());
            AddToPlaylist([key]);
            if (_playlist.Current == key && _isPlaying)
                return;   // era la primera de la lista y ya ha empezado a sonar
        }
        SelectSong(key);
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
        // Las fotos de las tarjetas que dejan de verse se sueltan; se recargan al volver a su pestaña.
        if (section != Section.Artists)
            foreach (var card in _artistCards.Values)
                card.ReleasePicture();
        if (section != Section.Genres)
            foreach (var card in _genreCards.Values)
                card.ReleasePicture();
        CategoryHeaderPictureHost.Child = null;
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
        bool newLocal = false;
        foreach (var (path, info) in tags)
        {
            if (SpotifyLibrary.TryGet(path, out var track))
            {
                // Sin la marca de YouTube y solo el primer artista, para agrupar y buscar en Deezer.
                _meta[path] = new SongMeta { Title = track.Title, Artist = track.MainArtist };
            }
            else
            {
                _meta[path] = new SongMeta { Title = info.Title, Artist = info.Artist };
                _localSongs[SpotifyLibrary.MatchKey(info.Artist, info.Title)] = path;
                _library.Describe(path, info.Title, info.Artist);
                newLocal = true;
            }
            _pending.Enqueue(path);
        }
        _library.Save();
        if (newLocal)
            RemoveSpotifyDuplicates();
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
        card.RestorePicture();
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

        SongListPanel.Children.Clear();
        foreach (var (path, meta) in songs)
            SongListPanel.Children.Add(CreateSongRow(path, meta.Title, artists ? meta.GenreName : meta.Artist));
    }

    /// <summary>
    /// Fila de una canción (detalle de artista o género, y resultados del buscador): icono, título y
    /// un dato a la derecha. La que suena sale resaltada. Si se indica, una parte del título va en
    /// morado (lo que coincide con la búsqueda). Al pulsarla, suena o se ve su vídeo.
    /// </summary>
    private Button CreateSongRow(string path, string titleText, string detailText, (int Start, int Length)? highlight = null)
    {
        var accent = (Brush)FindResource("Accent");
        var muted = (Brush)FindResource("Muted");
        bool playing = string.Equals(path, _playlist.Current, StringComparison.OrdinalIgnoreCase);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        bool youTube = SpotifyLibrary.IsSpotify(path);
        var icon = new TextBlock
        {
            Text = playing ? "" : youTube ? "" : "",   // altavoz / vídeo / reproducir
            FontFamily = IconFont,
            Foreground = playing ? accent : muted,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var title = new TextBlock
        {
            Foreground = playing ? accent : Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (highlight is { } part)
        {
            title.Inlines.Add(titleText[..part.Start]);
            title.Inlines.Add(new System.Windows.Documents.Run(titleText.Substring(part.Start, part.Length))
            {
                Foreground = accent,
                FontWeight = FontWeights.SemiBold,
            });
            title.Inlines.Add(titleText[(part.Start + part.Length)..]);
        }
        else
        {
            title.Text = titleText;
        }
        var detail = new TextBlock
        {
            Text = detailText,
            Foreground = muted,
            Margin = new Thickness(16, 0, 0, 0),
        };
        Grid.SetColumn(title, 1);
        Grid.SetColumn(detail, 2);
        row.Children.Add(icon);
        row.Children.Add(title);
        row.Children.Add(detail);

        var button = new Button
        {
            Style = (Style)FindResource("SongRowButton"),
            Content = row,
            Tag = path,
            ToolTip = youTube ? "Ver su vídeo de YouTube" : null,
        };
        button.Click += SongRow_Click;
        return button;
    }

    private void SongRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            SelectSong(path);
    }

    private void CategoryBack_Click(object sender, RoutedEventArgs e)
    {
        _openCategory = null;
        RefreshCategories();
    }

    // ───────────────────────────── Vista mosaico ─────────────────────────────

    private void ViewToggleButton_Click(object sender, RoutedEventArgs e) => SetMosaicView(!_mosaicView);

    private void EditButton_Click(object sender, RoutedEventArgs e) => SetEditing(!_isEditing);

    private void StopEditButton_Click(object sender, RoutedEventArgs e) => SetEditing(false);

    /// <summary>
    /// Entra o sale del modo edición. Dentro, Editar se queda en morado, aparece Dejar edición en
    /// verde y cada portada muestra su tirador; fuera, todo vuelve a ser como siempre.
    /// </summary>
    private void SetEditing(bool editing)
    {
        _isEditing = editing;
        _editor.IsEditing = editing;
        foreach (var tile in _tiles.Values)
            tile.IsEditable = editing;
        EditButton.Background = editing ? (Brush)FindResource("Accent") : EditIdle;
        StopEditButton.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetMosaicView(bool mosaic)
    {
        _mosaicView = mosaic;
        ApplyView();
    }

    /// <summary>El mosaico solo se ve en la pestaña Canciones con la vista mosaico elegida.</summary>
    private bool MosaicVisible => _section == Section.Songs && _mosaicView;

    /// <summary>La vista original (carátula grande) solo se ve en Canciones sin la vista mosaico.</summary>
    private bool SingleViewVisible => _section == Section.Songs && !_mosaicView;

    /// <summary>Muestra la vista que toca según la pestaña y la vista elegidas.</summary>
    private void ApplyView()
    {
        bool songs = _section == Section.Songs;
        MosaicView.Visibility = MosaicVisible ? Visibility.Visible : Visibility.Collapsed;
        SingleView.Visibility = SingleViewVisible ? Visibility.Visible : Visibility.Collapsed;
        CategoryView.Visibility = songs ? Visibility.Collapsed : Visibility.Visible;
        ViewToggleButton.Visibility = songs ? Visibility.Visible : Visibility.Collapsed;
        // Solo se edita el mosaico: al salir de él se deja la edición.
        EditButton.Visibility = MosaicVisible ? Visibility.Visible : Visibility.Collapsed;
        if (!MosaicVisible && _isEditing)
            SetEditing(false);
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

        // En la vista original suena el vídeo de la canción; fuera de ella, el .mp3 por donde iba
        // (las de Spotify, su vídeo oculto).
        UpdateSongSource();

        // La carátula grande (640 px, ~1,6 MB) solo ocupa memoria mientras se ve la vista original.
        if (!SingleViewVisible)
            ShowCover(null, null);
        else if (_playlist.Current is { } current && _coverShownFor != current)
            _ = ShowMetadataAsync(current);
    }

    /// <summary>
    /// Crea o quita teselas para que el mosaico coincida con la lista de reproducción y los accesos
    /// directos de Spotify: en orden alfabético o en el que haya elegido el usuario arrastrándolas.
    /// </summary>
    private void SyncMosaic()
    {
        var songs = _playlist.Songs.ToList();   // ya en orden alfabético
        songs = _arrangement.Arrange(songs);
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
                tile.IsEditable = _isEditing;
                _tiles.Add(songs[i], tile);
                added.Add(tile);
            }
            // Sin tamaño elegido, una de cada seis es grande.
            tile.BaseSpan = _arrangement.SpanOf(songs[i]) ?? (i % 6 == 0 ? 2 : 1);
            MosaicPanel.SetSpan(tile, tile.EffectiveSpan);
            MosaicPanel.SetOrder(tile, i);
            Mosaic.Children.Add(tile);
        }

        SongCountText.Text = songs.Count == 1 ? "1 canción" : $"{songs.Count} canciones";
        UpdateLibraryButtons();
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

    /// <summary>Se ha movido o cambiado de tamaño una portada: se guarda cómo ha quedado el mosaico.</summary>
    private void Editor_Changed(object? sender, EventArgs e)
    {
        _arrangement.Remember(_editor.Ordered());
        UpdateVisibleCovers();
    }

    private void SetActiveTile(string path)
    {
        if (_activeTile is { } old && old.SongPath != path)
        {
            old.SetActive(false);
            old.SizeChanged -= ActiveTile_SizeChanged;
            MosaicPanel.SetSpan(old, old.EffectiveSpan);
        }

        if (!_tiles.TryGetValue(path, out var tile))
            return;
        _activeTile = tile;
        if (!tile.IsActive)
        {
            tile.SetActive(true);
            tile.SizeChanged += ActiveTile_SizeChanged;
            MosaicPanel.SetSpan(tile, tile.EffectiveSpan);
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
        if (sender is MosaicTile tile)
            SelectSong(tile.SongPath);
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
        if (_audioFromVideo)
            _songVideo?.Play();
        else
            _player.Play();
        _isPlaying = true;
        PlayPauseButton.Content = PauseIcon;
        _timer.Start();
    }

    private void Pause()
    {
        if (_audioFromVideo)
            _songVideo?.Pause();
        else
            _player.Pause();
        _isPlaying = false;
        PlayPauseButton.Content = PlayIcon;
        _timer.Stop();
    }

    private void Stop()
    {
        _player.Stop();
        _player.Close();
        _pendingSeek = null;
        // El vídeo se queda (se reutiliza para la siguiente canción), pero callado.
        _songVideoVersion++;
        _audioFromVideo = false;
        _songVideo?.Pause();
        _isPlaying = false;
        PlayPauseButton.Content = PlayIcon;
        _timer.Stop();
    }

    private void Player_MediaOpened(object? sender, EventArgs e)
    {
        if (!_player.NaturalDuration.HasTimeSpan)
            return;

        _skippedInARow = 0;
        var duration = _player.NaturalDuration.TimeSpan;
        SeekSlider.Maximum = Math.Max(1, duration.TotalSeconds);
        TotalTimeText.Text = Format(duration);
        // Al pasar del vídeo al .mp3 se sigue por donde iba.
        if (_pendingSeek is { } position)
        {
            _player.Position = position < duration ? position : TimeSpan.Zero;
            _pendingSeek = null;
            UpdateProgress();
        }
    }

    private void Player_MediaFailed(object? sender, ExceptionEventArgs e)
    {
        // Libera el archivo y detiene el temporizador, que si no seguiría activo sin nada que reproducir.
        Stop();
        MessageBox.Show(this, $"No se pudo reproducir «{TitleText.Text}» y se ha quitado de la lista.\n{e.ErrorException.Message}",
            "Launcherito", MessageBoxButton.OK, MessageBoxImage.Error);

        if (_playlist.Current is { } failed)
        {
            _meta.Remove(failed);
            foreach (var key in _localSongs.Where(kv => kv.Value == failed).Select(kv => kv.Key).ToList())
                _localSongs.Remove(key);
        }
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
            StartPanel.Visibility = Visibility.Visible;
        }
    }

    private void Player_MediaEnded(object? sender, EventArgs e) => SongEnded();

    /// <summary>Ha terminado la canción (el .mp3 o su vídeo).</summary>
    private void SongEnded()
    {
        // Con varias canciones pasa a la siguiente (al acabar la lista vuelve a empezar).
        if (_playlist.Count > 1 && _playlist.Next() is { } next)
        {
            PlaySong(next);
            return;
        }

        Pause();
        if (_audioFromVideo)
        {
            _songVideo?.Seek(0);
            _videoTime = 0;
        }
        else
        {
            _player.Position = TimeSpan.Zero;
        }
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
            SeekSlider.Value = _audioFromVideo ? _videoTime : _player.Position.TotalSeconds;
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
        if (_audioFromVideo)
        {
            _songVideo?.Seek(SeekSlider.Value);
            _videoTime = SeekSlider.Value;
        }
        else
        {
            _player.Position = TimeSpan.FromSeconds(SeekSlider.Value);
        }

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
        _spotifyStatusClear.Stop();
        _animator.Stop();
        DisposeSongVideo();
        _catalog.Save();
        _library.Save();
        base.OnClosed(e);
    }
}
