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

    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _isPlaying;
    private bool _isSeeking;

    public MainWindow()
    {
        InitializeComponent();

        _player.MediaOpened += Player_MediaOpened;
        _player.MediaEnded += Player_MediaEnded;
        _player.MediaFailed += (_, e) =>
            MessageBox.Show(this, $"No se pudo reproducir el archivo.\n{e.ErrorException.Message}",
                "Launcherito", MessageBoxButton.OK, MessageBoxImage.Error);
        _timer.Tick += (_, _) => UpdateProgress();
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona una canción",
            Filter = "Archivos MP3 (*.mp3)|*.mp3",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        if (!string.Equals(Path.GetExtension(dialog.FileName), ".mp3", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Solo se admiten archivos .mp3.", "Launcherito",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoadSong(dialog.FileName);
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
        BitmapImage? cover = null;

        try
        {
            using var file = TagLib.File.Create(path);
            if (!string.IsNullOrWhiteSpace(file.Tag.Title))
                title = file.Tag.Title;
            if (!string.IsNullOrWhiteSpace(file.Tag.FirstPerformer))
                artist = file.Tag.FirstPerformer;
            if (file.Tag.Pictures.Length > 0)
                cover = LoadImage(file.Tag.Pictures[0].Data.Data);
        }
        catch (Exception)
        {
            // Etiquetas ilegibles: se usa el nombre del archivo y la carátula por defecto.
        }

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
            image.CacheOption = BitmapCacheOption.OnLoad;
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

    private void Player_MediaEnded(object? sender, EventArgs e)
    {
        Pause();
        _player.Position = TimeSpan.Zero;
        UpdateProgress();
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
        _timer.Stop();
        _player.Close();
        base.OnClosed(e);
    }
}
