using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Launcherito;

/// <summary>
/// Reproductor de YouTube dentro de una portada del mosaico. Usa WebView2 (el Edge que trae Windows)
/// en su versión de composición: se dibuja como cualquier otro elemento de WPF, así que respeta las
/// esquinas redondeadas, el desplazamiento del mosaico y las animaciones. Al cerrarlo se libera
/// entero (el navegador ocupa bastante memoria), y se crea otro para el siguiente vídeo.
/// </summary>
public sealed class YouTubeVideo : IDisposable
{
    // Página propia servida desde la memoria: YouTube solo deja insertar sus vídeos desde una web
    // con dirección (no desde un archivo ni una página en blanco).
    private const string Host = "https://launcherito.local/";
    private const string Page = """
        <!doctype html>
        <html><head><meta charset="utf-8">
        <style>html,body{margin:0;height:100%;overflow:hidden;background:#000}#player{width:100%;height:100%}</style>
        </head><body><div id="player"></div>
        <script>
          const ids = new URLSearchParams(location.search).get('v').split(',');
          let index = 0;
          function onYouTubeIframeAPIReady() {
            const player = new YT.Player('player', {
              videoId: ids[0], width: '100%', height: '100%',
              playerVars: { autoplay: 1, playsinline: 1, rel: 0 },
              events: {
                onReady: e => e.target.playVideo(),
                // 101/150: el dueño no deja verlo fuera de YouTube; se prueba el siguiente resultado.
                onError: () => ++index < ids.length ? player.loadVideoById(ids[index]) : chrome.webview.postMessage('unavailable'),
              },
            });
          }
        </script>
        <script src="https://www.youtube.com/iframe_api" onerror="chrome.webview.postMessage('unavailable')"></script>
        </body></html>
        """;

    private static Task<CoreWebView2Environment>? _environment;

    public WebView2CompositionControl View { get; } = new() { DefaultBackgroundColor = System.Drawing.Color.Black };

    /// <summary>No se puede ver ninguno de los vídeos dentro de la aplicación (o no hay conexión).</summary>
    public event EventHandler? Unavailable;

    /// <summary>
    /// Carga los vídeos (prueba el siguiente si uno no se deja insertar). Llamarlo con <see cref="View"/>
    /// ya colocado en la ventana.
    /// </summary>
    /// <exception cref="WebView2RuntimeNotFoundException">Windows no tiene WebView2.</exception>
    public async Task StartAsync(IReadOnlyList<string> videoIds)
    {
        await View.EnsureCoreWebView2Async(await (_environment ??= CreateEnvironmentAsync()));
        var core = View.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.AddWebResourceRequestedFilter(Host + "*", CoreWebView2WebResourceContext.Document);
        core.WebResourceRequested += (_, e) => e.Response = core.Environment.CreateWebResourceResponse(
            new MemoryStream(Encoding.UTF8.GetBytes(Page)), 200, "OK", "Content-Type: text/html; charset=utf-8");
        core.WebMessageReceived += (_, e) =>
        {
            if (e.TryGetWebMessageAsString() == "unavailable")
                Unavailable?.Invoke(this, EventArgs.Empty);
        };
        // "Ver en YouTube" y demás enlaces del reproductor se abren en el navegador.
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            YouTubeLinks.Open(e.Uri);
        };
        core.Navigate($"{Host}?v={string.Join(',', videoIds)}");
    }

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
