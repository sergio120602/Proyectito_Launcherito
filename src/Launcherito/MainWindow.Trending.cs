using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Launcherito;

/// <summary>
/// Pestaña «Del momento»: las playlists más escuchadas de Spotify (<see cref="TrendingPlaylists"/>).
/// Se ven como tarjetas con su portada; al abrir una salen sus canciones por puesto, y al pulsar una
/// (o «Reproducir») suena la playlist: los datos son de Spotify y la música, el vídeo de YouTube de
/// cada canción, que se ve en la vista original como el de cualquier otra canción de Spotify.
/// </summary>
public partial class MainWindow
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private TrendingPlaylists _trending = TrendingPlaylists.Load();
    private DateTime _trendingFileTime;          // para notar que la tarea programada ha escrito unas nuevas
    private string? _sessionTrendingId;          // playlist del momento que se está escuchando, o null
    private bool _trendingRefreshing;
    private string? _trendingError;              // por qué falló la última vez que se intentó (sin conexión…)
    private readonly DispatcherTimer _trendingTimer = new() { Interval = TimeSpan.FromMinutes(5) };
    private readonly Dictionary<string, CategoryCard> _trendingCards = new();

    /// <summary>
    /// Al abrir la ventana: si ya han pasado las 7:00 y no se han renovado (el PC estaba apagado, p.
    /// ej.), se renuevan; y cada 5 minutos se mira si la tarea programada ha dejado unas nuevas o si
    /// ha llegado la hora estando la aplicación abierta.
    /// </summary>
    private void InitTrending()
    {
        _trendingFileTime = TrendingFileTime();
        _trendingTimer.Tick += (_, _) => CheckTrending();
        _trendingTimer.Start();
        Loaded += (_, _) => CheckTrending();
    }

    private static DateTime TrendingFileTime() =>
        File.Exists(TrendingPlaylists.FilePath) ? File.GetLastWriteTimeUtc(TrendingPlaylists.FilePath) : DateTime.MinValue;

    private void CheckTrending()
    {
        var time = TrendingFileTime();
        if (time != _trendingFileTime && !_trendingRefreshing)
        {
            // Las ha renovado la tarea programada: se leen las nuevas.
            _trendingFileTime = time;
            _trending = TrendingPlaylists.Load();
            if (_section == Section.Trending)
                RefreshTrending();
        }
        if (_trending.IsStale(DateTime.Now))
            _ = RefreshTrendingAsync(manual: false);
    }

    private void TrendingRefresh_Click(object sender, RoutedEventArgs e) => _ = RefreshTrendingAsync(manual: true);

    private async Task RefreshTrendingAsync(bool manual)
    {
        if (_trendingRefreshing)
            return;
        _trendingRefreshing = true;
        if (_section == Section.Trending)
            RefreshTrending();
        try
        {
            _trending = await TrendingPlaylists.RefreshAsync(_trending);
            _trendingFileTime = TrendingFileTime();
            _trendingError = null;
            if (manual)
                ShowListStatus("Del momento: playlists actualizadas", OnlineSource.Spotify);
        }
        catch (ListImportException ex)
        {
            // Se vuelve a intentar en el siguiente repaso (cada 5 minutos) o con «Actualizar ahora».
            _trendingError = ex.Message;
            if (manual)
                ShowListStatus(ex.Message, OnlineSource.Spotify);
        }
        finally
        {
            _trendingRefreshing = false;
            if (_section == Section.Trending)
                RefreshTrending();
            UpdateSessionLabel();
        }
    }

    // ─────────────────────────────── La pestaña ───────────────────────────────

    private void TrendingTab_Click(object sender, RoutedEventArgs e) => SetSection(Section.Trending);

    /// <summary>Desde la pantalla inicial se puede ir aunque no haya nada cargado.</summary>
    private void StartTrendingButton_Click(object sender, RoutedEventArgs e)
    {
        SetSection(Section.Trending);
        StartPanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;
    }

    private void HideTrendingUi()
    {
        TrendingInfo.Visibility = Visibility.Collapsed;
        TrendingActions.Visibility = Visibility.Collapsed;
    }

    private TrendingList? OpenTrending => _openCategory is { } id ? _trending.Get(id) : null;

    /// <summary>Tarjetas de las playlists del momento, o las canciones de la que esté abierta.</summary>
    private void RefreshTrending()
    {
        ClearListMosaic();
        PlaylistActions.Visibility = Visibility.Collapsed;
        if (OpenTrending is { Tracks.Count: > 0 } open)
        {
            ShowTrendingDetail(open);
            return;
        }
        _openCategory = null;

        TrendingActions.Visibility = Visibility.Collapsed;
        CategoryHeader.Visibility = Visibility.Collapsed;
        SongListPanel.Visibility = Visibility.Collapsed;
        CardsPanel.Visibility = Visibility.Visible;
        TrendingInfo.Visibility = Visibility.Visible;
        CategoryHeaderPictureHost.Child = null;
        UpdateTrendingStatus();

        CardsPanel.Children.Clear();
        foreach (var list in _trending.Lists.Where(list => list.Tracks.Count > 0))
        {
            var card = GetTrendingCard(list);
            card.SetDetail($"{Songs(list.Tracks.Count)}  ·  Spotify" + (_sessionTrendingId == list.Id ? "  ·  sonando" : ""));
            CardsPanel.Children.Add(card);
        }
        if (CardsPanel.Children.Count == 0)
        {
            CardsPanel.Children.Add(new TextBlock
            {
                Text = _trendingRefreshing
                    ? "Leyendo las playlists del momento en Spotify…"
                    : "Aún no se han podido leer las playlists del momento. Comprueba la conexión y pulsa «Actualizar ahora».",
                Foreground = (Brush)FindResource("Muted"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 10, 4, 10),
            });
        }
    }

    /// <summary>La tarjeta de cada playlist se guarda, así su portada no se descarga cada vez que se redibuja.</summary>
    private CategoryCard GetTrendingCard(TrendingList list)
    {
        if (!_trendingCards.TryGetValue(list.Id, out var card) || card.Key != list.Name)
        {
            card = new CategoryCard(list.Name, round: false, "", (Brush)FindResource("SpotifyGreen"))
            {
                Style = (Style)FindResource("CardButton"),
            };
            string id = list.Id;
            card.Click += (_, _) =>
            {
                _openCategory = id;
                CategoryScroll.ScrollToTop();
                RefreshTrending();
            };
            _trendingCards[list.Id] = card;
        }
        card.SetPicture(list.CoverUrl);
        card.RestorePicture();
        return card;
    }

    /// <summary>«De Spotify · actualizadas hoy a las 7:00 · se renuevan cada día a las 7:00».</summary>
    private void UpdateTrendingStatus()
    {
        string schedule = $"se renuevan cada día a las {TrendingPlaylists.RefreshHour}:00";
        TrendingStatusText.Text = _trendingRefreshing
            ? "Leyendo las playlists del momento en Spotify…"
            : _trending.Updated is { } updated
                ? $"De Spotify  ·  actualizadas {WhenText(updated)}  ·  {schedule}" +
                  (_trendingError is not null && _trending.IsStale(DateTime.Now) ? "  ·  no se han podido renovar hoy" : "")
                : $"De Spotify  ·  {schedule}";
        TrendingRefreshButton.IsEnabled = !_trendingRefreshing;
        TrendingRefreshButton.Opacity = _trendingRefreshing ? 0.4 : 1;
    }

    private static string WhenText(DateTime time)
    {
        string hour = time.ToString("H:mm", Spanish);
        return time.Date == DateTime.Today ? $"hoy a las {hour}"
            : time.Date == DateTime.Today.AddDays(-1) ? $"ayer a las {hour}"
            : $"el {time.ToString("d 'de' MMMM", Spanish)} a las {hour}";
    }

    /// <summary>Canciones de una playlist del momento, por puesto. Al pulsar una, suena la playlist desde ella.</summary>
    private void ShowTrendingDetail(TrendingList list)
    {
        RegisterTrending(list);
        CardsPanel.Visibility = Visibility.Collapsed;
        TrendingInfo.Visibility = Visibility.Collapsed;
        SongListPanel.Visibility = Visibility.Visible;
        CategoryHeader.Visibility = Visibility.Visible;
        TrendingActions.Visibility = Visibility.Visible;
        CategoryTitleText.Text = list.Name;
        CategorySubtitleText.Text = $"Spotify  ·  {Songs(list.Tracks.Count)}" +
                                    (_trending.Updated is { } updated ? $"  ·  actualizada {WhenText(updated)}" : "") +
                                    (_sessionTrendingId == list.Id ? "  ·  sonando ahora" : "");
        CategoryHeaderPictureHost.Child = GetTrendingCard(list).CreateThumbnail(72);

        SongListPanel.Children.Clear();
        for (int i = 0; i < list.Tracks.Count; i++)
        {
            var song = list.Tracks[i];
            string key = PlayKey(song);
            var row = CreateSongRow(key, $"{i + 1}.  {song.Title}", song.Artist);
            row.Click -= SongRow_Click;
            row.Click += (_, _) => PlayFromTrending(list, key);
            SongListPanel.Children.Add(row);
        }
    }

    // ─────────────────────────────── Escucharlas ───────────────────────────────

    /// <summary>Da a conocer sus canciones (para sus portadas, sus vídeos y el menú de clic derecho).</summary>
    private static void RegisterTrending(TrendingList list)
    {
        foreach (var song in list.Tracks)
        {
            if (!OnlineSongs.TryGet(song.Key, out _))
                OnlineSongs.Register(song.ToTrack());
        }
    }

    /// <summary>La que suena: el .mp3 si ya se tiene esa canción (está entera), si no la de Spotify (con su vídeo).</summary>
    private string PlayKey(SavedSong song) =>
        _localSongs.TryGetValue(OnlineSongs.MatchKey(song.Artist, song.Title), out var mp3) ? mp3 : song.Key;

    /// <summary>
    /// Deja en el mosaico solo las canciones de la playlist del momento y empieza a sonar
    /// <paramref name="startWith"/> (o la primera). Sin aleatorio suenan por puesto. No se guardan en
    /// «Tus canciones» (cambian cada día); sí las que se pongan en Favoritos o en una playlist.
    /// </summary>
    private void LoadTrendingSession(TrendingList list, string? startWith)
    {
        RegisterTrending(list);
        var keys = list.Tracks.Select(PlayKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (keys.Count == 0)
            return;

        SetEditing(false);
        Stop();
        var keep = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        ForgetLoaded(_playlist.Songs.Where(key => !keep.Contains(key)).ToList(), keepCurrent: false);
        _playlist.Clear();

        _sessionListId = null;
        _sessionTrendingId = list.Id;
        _loadingSession = true;
        try
        {
            _playlist.SetCustomOrder(keys);
            AddToPlaylist(keys, startWith);
        }
        finally
        {
            _loadingSession = false;
        }
        UpdateSessionLabel();
        ScheduleCategoryRefresh();
        ShowListStatus($"Escuchando «{list.Name}»: {Songs(keys.Count)}", OnlineSource.Spotify);
    }

    private void PlayFromTrending(TrendingList list, string key)
    {
        if (_sessionTrendingId == list.Id && _tiles.ContainsKey(key))
            SelectSong(key);
        else
            LoadTrendingSession(list, key);
    }

    /// <summary>«Reproducir»: suena la playlist y se pasa al mosaico con sus canciones.</summary>
    private void PlayTrending_Click(object sender, RoutedEventArgs e)
    {
        if (OpenTrending is not { Tracks.Count: > 0 } list)
            return;
        LoadTrendingSession(list, null);
        SetSection(Section.Songs);
    }

    /// <summary>Copia la playlist tal como está hoy a «Tus playlists» (y sus canciones a «Tus canciones»).</summary>
    private void SaveTrending_Click(object sender, RoutedEventArgs e)
    {
        if (OpenTrending is not { Tracks.Count: > 0 } list)
            return;
        RegisterTrending(list);
        string baseName = $"{list.Name} · {DateTime.Now.ToString("d MMM", Spanish)}";
        string name = baseName;
        for (int n = 2; _userLists.CheckName(name) is not null && n < 100; n++)
            name = $"{baseName} ({n})";

        var keys = list.Tracks.Select(PlayKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _library.Add(list.Tracks.Where(song => keys.Contains(song.Key)));   // las que no son .mp3 tuyos
        var saved = _userLists.Create(name);
        _userLists.Add(saved.Id, keys);
        UpdateLibraryButtons();
        ShowPlaylistStatus($"Guardada en tus playlists como «{name}»");
    }

    private void OpenTrendingInSpotify_Click(object sender, RoutedEventArgs e)
    {
        if (OpenTrending is { } list)
            YouTubeLinks.Open($"https://open.spotify.com/playlist/{list.Id}");
    }

    /// <summary>
    /// Una canción que no está en «Tus canciones» (de una playlist del momento) se guarda allí al
    /// ponerla en Favoritos o en una playlist, para que siga disponible cuando la lista cambie.
    /// </summary>
    private void KeepInLibrary(string key)
    {
        if (OnlineSongs.TryGet(key, out var track) && !_library.Contains(key))
        {
            _library.Add([SavedSong.FromTrack(track)]);
            UpdateLibraryButtons();
        }
    }
}
