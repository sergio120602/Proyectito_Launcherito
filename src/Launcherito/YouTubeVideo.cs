using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Launcherito;

/// <summary>
/// Reproductor de YouTube. Usa WebView2 (el Edge que trae Windows) en su versión de composición: se
/// dibuja como cualquier otro elemento de WPF, así que respeta las esquinas redondeadas, el
/// desplazamiento del mosaico y las animaciones. Al cerrarlo se libera entero (el navegador ocupa
/// bastante memoria).
/// <para>
/// Dos modos: en una portada del mosaico es un reproductor de YouTube normal, con sus controles. En
/// la vista original (<c>controlled</c>) no tiene controles ni responde a los clics: lo manejan los
/// controles de Launcherito con <see cref="Play"/>, <see cref="Pause"/>, <see cref="Seek"/> y
/// <see cref="Load"/>, y avisa del tiempo (<see cref="Progress"/>) y de cuándo termina
/// (<see cref="Ended"/>).
/// </para>
/// </summary>
public sealed class YouTubeVideo : IDisposable
{
    // Página propia servida desde la memoria: YouTube solo deja insertar sus vídeos desde una web
    // con dirección (no desde un archivo ni una página en blanco).
    private const string Host = "https://launcherito.local/";
    private const string Page = """
        <!doctype html>
        <html><head><meta charset="utf-8">
        <style>
          html,body{margin:0;height:100%;overflow:hidden;background:#000}#player{width:100%;height:100%}
          .controlled #player{pointer-events:none}
        </style>
        </head><body><div id="player"></div>
        <script>
          var params = new URLSearchParams(location.search);
          var ids = params.get('v').split(','), index = 0, start = +(params.get('t') || 0);
          var controlled = params.get('c') === '1';
          var player = null, ready = false, wantPlay = true, waiting = false, shown = ids[0];
          if (controlled) document.documentElement.className = 'controlled';
          function send(message) { chrome.webview.postMessage(message); }
          function openVideo(id) {
            shown = id;
            var video = { videoId: id, startSeconds: start };
            wantPlay ? player.loadVideoById(video) : player.cueVideoById(video);
          }
          function onYouTubeIframeAPIReady() {
            player = new YT.Player('player', {
              videoId: ids[0], width: '100%', height: '100%',
              playerVars: controlled
                ? { autoplay: 0, playsinline: 1, rel: 0, controls: 0, disablekb: 1, fs: 0, iv_load_policy: 3, start: Math.floor(start) }
                : { autoplay: 1, playsinline: 1, rel: 0 },
              events: {
                onReady: function () {
                  ready = true;
                  // En modo controlado no arranca solo: suena si Launcherito no lo ha pausado mientras cargaba.
                  // Si mientras cargaba se ha pedido otra canción, se pone esa.
                  if (shown !== ids[index]) openVideo(ids[index]);
                  else if (wantPlay) player.playVideo();
                },
                onStateChange: function (e) { send({ type: 'state', state: e.data }); },
                // 101/150: el dueño no deja verlo fuera de YouTube; se prueba el siguiente resultado.
                onError: function () { ++index < ids.length ? openVideo(ids[index]) : send({ type: 'unavailable' }); },
              },
            });
            if (controlled)
              setInterval(function () {
                if (ready && !waiting && player.getCurrentTime)
                  send({ type: 'time', time: player.getCurrentTime(), duration: player.getDuration() });
              }, 250);
          }
          // Órdenes de Launcherito (modo controlado).
          function load(list, from) { waiting = false; ids = list; index = 0; start = from; if (ready) openVideo(ids[0]); }
          function hold() { waiting = true; if (ready) player.stopVideo(); }
          function play() { wantPlay = true; if (ready && !waiting) player.playVideo(); }
          function pause() { wantPlay = false; if (ready) player.pauseVideo(); }
          function seek(seconds) { if (ready) player.seekTo(seconds, true); }
        </script>
        <script src="https://www.youtube.com/iframe_api" onerror="send({ type: 'unavailable' })"></script>
        </body></html>
        """;

    private static Task<CoreWebView2Environment>? _environment;

    private readonly bool _controlled;
    private readonly List<string> _queued = new();   // órdenes que llegan antes de que la página esté lista
    private bool _pageReady;

    /// <param name="controlled">true: sin controles de YouTube, manejado desde Launcherito.</param>
    public YouTubeVideo(bool controlled = false)
    {
        _controlled = controlled;
        View.IsHitTestVisible = !controlled;
    }

    public WebView2CompositionControl View { get; } = new() { DefaultBackgroundColor = System.Drawing.Color.Black };

    /// <summary>No se puede ver ninguno de los vídeos dentro de la aplicación (o no hay conexión).</summary>
    public event EventHandler? Unavailable;

    /// <summary>Empieza a sonar el vídeo (también después de cada pausa o salto).</summary>
    public event EventHandler? Playing;

    /// <summary>Ha llegado al final.</summary>
    public event EventHandler? Ended;

    /// <summary>Cada 250 ms en el modo controlado: segundo actual y duración (0 mientras no se conoce).</summary>
    public event EventHandler<(double Time, double Duration)>? Progress;

    /// <summary>
    /// Carga los vídeos (prueba el siguiente si uno no se deja insertar), empezando en el segundo
    /// <paramref name="start"/>. Llamarlo con <see cref="View"/> ya colocado en la ventana.
    /// </summary>
    /// <exception cref="WebView2RuntimeNotFoundException">Windows no tiene WebView2.</exception>
    public async Task StartAsync(IReadOnlyList<string> videoIds, double start = 0)
    {
        await View.EnsureCoreWebView2Async(await (_environment ??= CreateEnvironmentAsync()));
        var core = View.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.AddWebResourceRequestedFilter(Host + "*", CoreWebView2WebResourceContext.Document);
        core.WebResourceRequested += (_, e) => e.Response = core.Environment.CreateWebResourceResponse(
            new MemoryStream(Encoding.UTF8.GetBytes(Page)), 200, "OK", "Content-Type: text/html; charset=utf-8");
        core.WebMessageReceived += (_, e) => OnMessage(e.WebMessageAsJson);
        core.NavigationCompleted += (_, _) =>
        {
            _pageReady = true;
            foreach (var script in _queued)
                _ = core.ExecuteScriptAsync(script);
            _queued.Clear();
        };
        // "Ver en YouTube" y demás enlaces del reproductor se abren en el navegador.
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            YouTubeLinks.Open(e.Uri);
        };
        core.Navigate($"{Host}?v={string.Join(',', videoIds)}&t={Number(start)}&c={(_controlled ? 1 : 0)}");
    }

    /// <summary>Cambia a otros vídeos (otra canción) sin crear otro navegador.</summary>
    public void Load(IReadOnlyList<string> videoIds, double start) =>
        Run($"load({JsonSerializer.Serialize(videoIds)}, {Number(start)})");

    /// <summary>Para el vídeo actual hasta el próximo <see cref="Load"/> (mientras se busca el de otra canción).</summary>
    public void Hold() => Run("hold()");

    public void Play() => Run("play()");

    public void Pause() => Run("pause()");

    public void Seek(double seconds) => Run($"seek({Number(seconds)})");

    private void Run(string script)
    {
        if (_pageReady && View.CoreWebView2 is { } core)
            _ = core.ExecuteScriptAsync(script);
        else
            _queued.Add(script);   // se manda en cuanto la página termine de cargar
    }

    private void OnMessage(string json)
    {
        try
        {
            using var message = JsonDocument.Parse(json);
            var root = message.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "unavailable":
                    Unavailable?.Invoke(this, EventArgs.Empty);
                    break;
                case "state" when root.GetProperty("state").GetInt32() is var state:
                    if (state == 1)
                        Playing?.Invoke(this, EventArgs.Empty);
                    else if (state == 0)
                        Ended?.Invoke(this, EventArgs.Empty);
                    break;
                case "time":
                    Progress?.Invoke(this, (root.GetProperty("time").GetDouble(), root.GetProperty("duration").GetDouble()));
                    break;
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Mensaje que no es de nuestra página: se ignora.
        }
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static Task<CoreWebView2Environment> CreateEnvironmentAsync() =>
        CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            // Fuera de la carpeta del .exe, para no llenarla de archivos del navegador.
            userDataFolder: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcherito", "WebView2"),
            // El clic que abre el vídeo se da en Launcherito, no en la página: sin esto no arrancaría solo.
            options: new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));

    public void Dispose() => View.Dispose();
}
