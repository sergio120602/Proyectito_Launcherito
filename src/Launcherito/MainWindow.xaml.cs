using System.IO;
using System.Windows;
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
    private bool _isPlaying;
    private bool _isSeeking;

    public MainWindow()
    {
        InitializeComponent();

        _player.MediaOpened += Player_MediaOpened;
        _player.MediaEnded += Player_MediaEnded;
        _player.MediaFailed += Player_MediaFailed;
        _timer.Tick += Timer_Tick;

        // Permite abrir una canción pasada como argumento (p. ej. "Abrir con → Launcherito").
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
            Loaded += (_, _) => TryLoadSong(args[1]);
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona una canción",
            Filter = "Archivos MP3 (*.mp3)|*.mp3",
        };
        if (dialog.ShowDialog(this) == true)
            TryLoadSong(dialog.FileName);
    }

    private void TryLoadSong(string path)
    {
        if (!File.Exists(path) ||
            !string.Equals(Path.GetExtension(path), ".mp3", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Solo se admiten archivos .mp3.", "Launcherito",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoadSong(path);
    }

    private void LoadSong(string path)
    {
        Stop();
        ShowMetadata(path);

        SeekSlider.Value = 0;
        CurrentTimeText.Text = "0:00";
        TotalTimeText.Text = "0:00";

        _player.Open(new Uri(path));
        LoadButton.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;
        Play();
    }

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
        PlayerPanel.Visibility = Visibility.Collapsed;
        LoadButton.Visibility = Visibility.Visible;
        MessageBox.Show(this, $"No se pudo reproducir el archivo.\n{e.ErrorException.Message}",
            "Launcherito", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void Player_MediaEnded(object? sender, EventArgs e)
    {
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

    private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _isSeeking = true;

    private void SeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _player.Position = TimeSpan.FromSeconds(SeekSlider.Value);
        _isSeeking = false;
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        CurrentTimeText.Text = Format(TimeSpan.FromSeconds(e.NewValue));

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
