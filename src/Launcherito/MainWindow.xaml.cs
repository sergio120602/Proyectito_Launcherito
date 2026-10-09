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
        CoverBrush.ImageSource = info.Cover;
        CoverPlaceholder.Visibility = info.Cover is null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ───────────────────────────── Vista mosaico ─────────────────────────────

    private void ViewToggleButton_Click(object sender, RoutedEventArgs e) => SetMosaicView(!_mosaicView);

    private void SetMosaicView(bool mosaic)
    {
        _mosaicView = mosaic;
        MosaicView.Visibility = mosaic ? Visibility.Visible : Visibility.Collapsed;
        SingleView.Visibility = mosaic ? Visibility.Collapsed : Visibility.Visible;
        // El botón muestra la vista a la que se cambia al pulsarlo.
        ViewToggleIcon.Text = mosaic ? "" : "";
        ViewToggleText.Text = mosaic ? "Vista original" : "Vista mosaico";

        MoveControls();
        if (mosaic)
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
        if (_mosaicView)
            ScrollToActiveTile();
    }

    /// <summary>
    /// Coloca el único juego de controles (barra, tiempos y botones) en la vista original
    /// o dentro de la portada activa del mosaico.
    /// </summary>
    private void MoveControls()
    {
        Decorator target = _mosaicView && _activeTile?.ControlsHost is { } host ? host : OriginalControlsHost;
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
        ControlsPanel.Width = ControlsPanel.Parent == OriginalControlsHost || _activeTile is null
            ? double.NaN
            : Math.Max(MinControlsWidth, _activeTile.ActualWidth - 32);
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
        if (!_mosaicView)
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

        var next = _playlist.RemoveCurrent();
        SyncMosaic();
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
        base.OnClosed(e);
    }
}
