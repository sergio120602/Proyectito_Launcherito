using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Launcherito;

/// <summary>
/// Buscar canciones en YouTube y quedárselas: la barra de la pantalla inicial, «＋ Desde YouTube»
/// arriba y la última fila del buscador abren un panel con los resultados (canciones o vídeos), o
/// leen el enlace de un vídeo. Las que se añaden se guardan en «Tus canciones» como las de YouTube
/// Music y suenan con su vídeo.
/// </summary>
public partial class MainWindow
{
    private const int YouTubeThumbSize = 56;
    private static readonly Brush YouTubeThumbFill = Frozen(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)));

    private bool _youTubeVideos;        // pestaña Vídeos (si no, Canciones)
    private int _youTubeSearchVersion;  // descarta resultados de una búsqueda anterior que llegan tarde

    // ─────────────────────────── Abrir el panel ───────────────────────────

    private void YouTubeSearchButton_Click(object sender, RoutedEventArgs e) => OpenYouTubeSearch(null);

    private void StartYouTubeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        string query = StartYouTubeBox.Text.Trim();
        StartYouTubeBox.Text = "";
        OpenYouTubeSearch(query);
    }

    /// <summary>Fila final del buscador de canciones: «Buscar «…» en YouTube».</summary>
    private Button CreateYouTubeSearchRow(string query)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = "",   // vídeo
            FontFamily = IconFont,
            Foreground = (Brush)FindResource("YouTubeMusicRed"),
            VerticalAlignment = VerticalAlignment.Center,
            Width = 32,
        });
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        text.Inlines.Add("Buscar «");
        text.Inlines.Add(new System.Windows.Documents.Run(query) { FontWeight = FontWeights.SemiBold });
        text.Inlines.Add("» en YouTube");
        content.Children.Add(text);

        var row = new Button { Style = (Style)FindResource("SongRowButton"), Content = content };
        row.Click += (_, _) =>
        {
            SearchBox.Text = "";   // cierra la lista
            Keyboard.ClearFocus();
            OpenYouTubeSearch(query);
        };
        return row;
    }

    /// <summary>Abre el panel; con texto, busca (o lee el enlace) al momento.</summary>
    private void OpenYouTubeSearch(string? query)
    {
        SearchPopup.IsOpen = false;
        YouTubeOverlay.Visibility = Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(query))
        {
            YouTubeQueryBox.Text = query.Trim();
            _ = RunYouTubeSearchAsync();
        }
        else if (YouTubeResults.Children.Count == 0)
        {
            YouTubeStatus.Text = "";
        }
        Dispatcher.BeginInvoke(() =>
        {
            YouTubeQueryBox.Focus();
            YouTubeQueryBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void YouTubeOverlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            CloseYouTubeSearch();
    }

    private void YouTubeClose_Click(object sender, RoutedEventArgs e) => CloseYouTubeSearch();

    /// <summary>Se cierra; la búsqueda se queda para cuando se vuelva a abrir.</summary>
    private void CloseYouTubeSearch()
    {
        _youTubeSearchVersion++;   // si aún se estaba buscando, se descarta
        YouTubeSearchGo.IsEnabled = true;
        YouTubeOverlay.Visibility = Visibility.Collapsed;
    }

    // ─────────────────────────────── Buscar ───────────────────────────────

    private void YouTubeQueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _ = RunYouTubeSearchAsync();
    }

    private void YouTubeSearchGo_Click(object sender, RoutedEventArgs e) => _ = RunYouTubeSearchAsync();

    private void YouTubeSongsTab_Click(object sender, RoutedEventArgs e) => SetYouTubeTab(videos: false);

    private void YouTubeVideosTab_Click(object sender, RoutedEventArgs e) => SetYouTubeTab(videos: true);

    /// <summary>Cambia entre canciones y vídeos y repite la búsqueda escrita.</summary>
    private void SetYouTubeTab(bool videos)
    {
        if (_youTubeVideos == videos)
            return;
        _youTubeVideos = videos;
        YouTubeSongsTab.Tag = videos ? null : "Active";
        YouTubeVideosTab.Tag = videos ? "Active" : null;
        if (YouTubeQueryBox.Text.Trim().Length > 0 &&
            YouTubeMusicLibrary.VideoIdFromLink(YouTubeQueryBox.Text) is null)
            _ = RunYouTubeSearchAsync();
    }

    /// <summary>
    /// Lo escrito puede ser el enlace de un vídeo (se lee ese vídeo), el de una lista o un álbum (se
    /// abre el panel de listas) o el texto que se busca.
    /// </summary>
    private async Task RunYouTubeSearchAsync()
    {
        string text = YouTubeQueryBox.Text.Trim();
        if (text.Length == 0)
            return;
        int version = ++_youTubeSearchVersion;

        string? videoId = YouTubeMusicLibrary.VideoIdFromLink(text);
        if (videoId is null && OnlineSongs.ParseLink(text) is not null)
        {
            // Enlace de una lista entera: la añade el panel de siempre.
            CloseYouTubeSearch();
            OpenListDialog(OnlineSongs.ParseLink(text)!.Source);
            ListLinkBox.Text = text;
            _ = AddListAsync();
            return;
        }

        YouTubeSearchGo.IsEnabled = false;
        YouTubeStatus.Foreground = (Brush)FindResource("Muted");
        YouTubeStatus.Text = videoId is not null ? "Leyendo el vídeo…" : $"Buscando «{text}» en YouTube…";
        IReadOnlyList<OnlineTrack> results;
        try
        {
            results = videoId is not null
                ? [await YouTubeMusicLibrary.LoadVideoAsync(videoId)]
                : await YouTubeMusicLibrary.SearchAsync(text, _youTubeVideos);
        }
        catch (ListImportException ex)
        {
            if (version == _youTubeSearchVersion)
            {
                YouTubeSearchGo.IsEnabled = true;
                YouTubeStatus.Foreground = (Brush)FindResource("DangerRed");
                YouTubeStatus.Text = ex.Message;
            }
            return;
        }
        if (version != _youTubeSearchVersion)
            return;   // mientras tanto se ha buscado otra cosa o se ha cerrado el panel

        YouTubeSearchGo.IsEnabled = true;
        YouTubeStatus.Text = results.Count == 0
            ? $"YouTube no ha encontrado nada para «{text}»."
            : videoId is not null ? "Vídeo del enlace:"
            : _youTubeVideos ? "Vídeos:" : "Canciones:";
        YouTubeResults.Children.Clear();
        foreach (var track in results)
            YouTubeResults.Children.Add(CreateYouTubeRow(track));
        YouTubeScroll.ScrollToTop();
    }

    // ───────────────────────────── Resultados ─────────────────────────────

    /// <summary>Fila de un resultado: imagen, título, artista y duración, ▶ para escucharla ya y «Añadir».</summary>
    private Grid CreateYouTubeRow(OnlineTrack track)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var thumb = new Border
        {
            Width = YouTubeThumbSize,
            Height = YouTubeThumbSize,
            CornerRadius = new CornerRadius(6),
            Background = YouTubeThumbFill,
            Margin = new Thickness(4, 4, 0, 4),
        };
        try
        {
            // WPF la descarga en segundo plano; mientras tanto se ve el fondo gris.
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(YouTubeMusicLibrary.ThumbnailUrl(track));
            image.DecodePixelHeight = YouTubeThumbSize * 2;
            image.EndInit();
            thumb.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        }
        catch (Exception ex) when (ex is UriFormatException or NotSupportedException)
        {
            // Sin imagen: se queda el fondo.
        }

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 12, 0) };
        texts.Children.Add(new TextBlock
        {
            Text = track.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = track.Title,
        });
        string detail = track.Duration > TimeSpan.Zero ? $"{track.Artists}  ·  {Format(track.Duration)}" : track.Artists;
        texts.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 12,
            Foreground = (Brush)FindResource("Muted"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0),
        });

        var play = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Width = 40,
            Height = 40,
            FontSize = 15,
            Content = "",   // reproducir
            ToolTip = "Escuchar ahora (también se guarda en «Tus canciones»)",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var add = new Button
        {
            Style = (Style)FindResource("ColorPillButton"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(16, 7, 16, 7),
            Margin = new Thickness(6, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        UpdateYouTubeAddButton(add, track);
        play.Click += (_, _) =>
        {
            AddFromYouTube(track, play: true);
            UpdateYouTubeAddButton(add, track);
        };
        add.Click += (_, _) =>
        {
            AddFromYouTube(track, play: false);
            UpdateYouTubeAddButton(add, track);
        };

        Grid.SetColumn(texts, 1);
        Grid.SetColumn(play, 2);
        Grid.SetColumn(add, 3);
        row.Children.Add(thumb);
        row.Children.Add(texts);
        row.Children.Add(play);
        row.Children.Add(add);
        return row;
    }

    /// <summary>«Añadir» en rojo, o apagado si ya está en «Tus canciones» o se tiene en .mp3.</summary>
    private void UpdateYouTubeAddButton(Button add, OnlineTrack track)
    {
        string? mp3 = OwnedMp3(track);
        bool saved = _library.Contains(track.Key);
        add.Content = mp3 is not null ? "Ya la tienes en .mp3" : saved ? "✓  En tus canciones" : "Añadir";
        add.IsEnabled = mp3 is null && !saved;
        add.Background = add.IsEnabled ? (Brush)FindResource("YouTubeMusicRed") : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        add.Foreground = add.IsEnabled ? Brushes.White : (Brush)FindResource("Muted");
        add.ToolTip = add.IsEnabled ? "Guardarla en «Tus canciones» y en el mosaico" : null;
    }

    /// <summary>El .mp3 cargado que es esta misma canción, si lo hay (entonces suena el archivo, que está entero).</summary>
    private string? OwnedMp3(OnlineTrack track) =>
        _localSongs.TryGetValue(OnlineSongs.MatchKey(track.Artists, track.Title), out var path) ? path : null;

    /// <summary>
    /// Guarda la canción en «Tus canciones» y la pone en el mosaico; con <paramref name="play"/>, además
    /// suena. Si ya se tiene en .mp3, no se repite: suena el archivo.
    /// </summary>
    private void AddFromYouTube(OnlineTrack track, bool play)
    {
        if (OwnedMp3(track) is { } mp3)
        {
            if (play)
                SelectSong(mp3);
            return;
        }

        string key = track.Key;
        bool isNew = !_library.Contains(key);
        OnlineSongs.Register(track);
        _library.Add([SavedSong.FromTrack(track)]);
        if (!_tiles.ContainsKey(key))
            AddToPlaylist([key], startWith: play ? key : null);
        if (play && (_playlist.Current != key || !_isPlaying))
            SelectSong(key);

        ShowListStatus(isNew ? $"YouTube: +1 canción  ·  «{track.Title}»" : $"«{track.Title}» ya estaba en tus canciones",
                       OnlineSource.YouTubeMusic);
        YouTubeStatus.Foreground = (Brush)FindResource("Muted");
        YouTubeStatus.Text = play ? $"Suena «{track.Title}»" : $"«{track.Title}» se ha guardado en «Tus canciones»";
    }
}
