using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Launcherito;

/// <summary>
/// Vídeos de YouTube para las canciones de Spotify, que no se pueden escuchar enteras dentro de
/// Launcherito. Se busca "artista título" en YouTube (sin clave) y se toman los primeros vídeos.
/// </summary>
public static class YouTubeLinks
{
    private const int MaxVideos = 5;   // si el primero no se deja ver dentro de otra app, se prueban los siguientes

    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> Videos = new();   // clave de Spotify → ids
    private static readonly Regex VideoPattern = new("\"videoId\":\"(?<id>[A-Za-z0-9_-]{11})\"", RegexOptions.Compiled);

    /// <summary>
    /// Ids de los primeros vídeos que da YouTube para la canción, en orden; vacío si no hay conexión.
    /// Se recuerdan mientras la aplicación está abierta.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FindVideosAsync(SpotifyTrack track)
    {
        if (Videos.TryGetValue(track.Key, out var known))
            return known;
        try
        {
            string html = await Http.GetStringAsync(SearchUrl(track));
            var ids = VideoPattern.Matches(html).Select(m => m.Groups["id"].Value).Distinct().Take(MaxVideos).ToList();
            if (ids.Count > 0)
                Videos[track.Key] = ids;
            return ids;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return [];   // sin conexión o YouTube no responde
        }
    }

    /// <summary>Abre en el navegador el vídeo o, si no se conoce, la búsqueda de la canción.</summary>
    public static void OpenInBrowser(SpotifyTrack track, string? videoId = null) =>
        Open(videoId is null ? SearchUrl(track) : "https://www.youtube.com/watch?v=" + videoId);

    public static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();

    private static string SearchUrl(SpotifyTrack track) =>
        "https://www.youtube.com/results?search_query=" +
        Uri.EscapeDataString($"{track.MainArtist} {MusicCatalog.CleanTitle(track.Title)}");

    private static HttpClient CreateClient()
    {
        // UseCookies false: la cabecera Cookie de abajo se envía tal cual en lugar de la del gestor de cookies.
        var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        // Sin esta cookie, en Europa YouTube responde con su página de aceptar cookies.
        client.DefaultRequestHeaders.Add("Cookie", "SOCS=CAI");
        return client;
    }
}
