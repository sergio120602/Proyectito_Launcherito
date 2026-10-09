using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Launcherito;

public partial class MainWindow : Window
{
    private const string PlayIcon = "";
    private const string PauseIcon = "";
    private const int CoverDecodeSize = 640;

    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Playlist _playlist = new();   // el modo aleatorio empieza activado (Playlist.Shuffle = true)
    private bool _isPlaying;
    private bool _isSeeking;
    private int _shownSecond;   // segundo que muestra CurrentTimeText, para no reescribirlo sin necesidad

    public MainWindow()
    {
        InitializeComponent();

        _player.MediaOpened += Player_MediaOpened;
        _player.MediaEnded += Player_MediaEnded;
        _player.MediaFailed += Player_MediaFailed;
        _timer.Tick += Timer_Tick;
        // handledEventsToo: al pulsar en la pista, el Slider marca el clic como gestionado
        // (IsMoveToPointEnabled) y un manejador normal declarado en XAML no llegaría a ejecutarse.
        SeekSlider.AddHandler(PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(SeekSlider_PreviewMouseLeftButtonDown), handledEventsToo: true);
        UpdateShuffleButton();

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

        bool wasEmpty = _playlist.Count == 0;
        _playlist.Add(valid);

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
        string title = Path.GetFileNameWithoutExtension(path);
        string artist = "Artista desconocido";
        byte[]? coverData = null;

        try
        {
            // El bloque using equivale al try-with-resources de Java: el archivo se cierra al salir
            // del bloque, antes de decodificar la carátula. ReadStyle.None evita analizar todo el
            // audio para calcular su duración, que aquí ya obtiene el reproductor.
            using (var file = TagLib.File.Create(path, TagLib.ReadStyle.None))
            {
                if (!string.IsNullOrWhiteSpace(file.Tag.Title))
                    title = file.Tag.Title;
                if (!string.IsNullOrWhiteSpace(file.Tag.FirstPerformer))
                    artist = file.Tag.FirstPerformer;
                if (file.Tag.Pictures.Length > 0)
                    coverData = file.Tag.Pictures[0].Data.Data;
            }
        }
        catch (Exception)
        {
            // Etiquetas ilegibles: se usa el nombre del archivo y la carátula por defecto.
        }

        var cover = coverData is null ? null : LoadImage(coverData);

        TitleText.Text = title;
        ArtistText.Text = artist;
        CoverBrush.ImageSource = cover;
        CoverPlaceholder.Visibility = cover is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BitmapImage? LoadImage(byte[] data)
    {
        try
        {
            var image = new BitmapImage();
            using var stream = new MemoryStream(data);
            image.BeginInit();
            // OnLoad copia los píxeles al crearla, así el stream puede liberarse al salir del método.
            image.CacheOption = BitmapCacheOption.OnLoad;
            // Limita la resolución decodificada: una carátula de 3000x3000 ocuparía ~36 MB en memoria.
            // DelayCreation lee solo la cabecera para conocer el tamaño sin decodificar la imagen.
            int width = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).PixelWidth;
            if (width > CoverDecodeSize)
                image.DecodePixelWidth = CoverDecodeSize;
            stream.Position = 0;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
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

        if (_playlist.RemoveCurrent() is { } next)
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
