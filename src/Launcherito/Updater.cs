using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Launcherito;

/// <summary>Una versión publicada en GitHub más nueva que la que está abierta.</summary>
/// <param name="Version">Número, sin la "v" ("1.12").</param>
/// <param name="Title">Título de la versión ("v1.12 - Barra de reproducción").</param>
/// <param name="DownloadUrl">El .zip de Windows de esa versión.</param>
/// <param name="PageUrl">Página de la versión en GitHub, para descargarla a mano si hace falta.</param>
public sealed record AppUpdate(string Version, string Title, string Notes, string DownloadUrl, long Size, string PageUrl);

/// <summary>No se ha podido buscar o instalar la actualización; el mensaje se muestra tal cual.</summary>
public sealed class UpdateException(string message) : Exception(message);

/// <summary>
/// Actualizaciones desde las versiones publicadas en GitHub (Releases). Cada versión lleva el .zip
/// de Windows con un único Launcherito.exe. Para instalarla, el .exe que está abierto se renombra a
/// Launcherito.old.exe (Windows deja renombrar un programa en marcha, pero no sobrescribirlo), el
/// nuevo ocupa su sitio y se abre; el viejo se borra al arrancar la próxima vez.
/// </summary>
public static class Updater
{
    private const string LatestUrl = "https://api.github.com/repos/sergio120602/Proyectito_Launcherito/releases/latest";
    public const string ReleasesPage = "https://github.com/sergio120602/Proyectito_Launcherito/releases/latest";
    private const string OldSuffix = ".old.exe";

    private static readonly HttpClient Http = CreateClient();
    private static readonly HttpClient NoRedirect = CreateClient(new HttpClientHandler { AllowAutoRedirect = false });

    /// <summary>Versión que está abierta ("1.11"), la de &lt;Version&gt; del .csproj.</summary>
    public static Version Current { get; } =
        Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0));

    public static string CurrentText => Current.ToString(Current.Revision > 0 ? 4 : Current.Build > 0 ? 3 : 2);

    /// <summary>
    /// La última versión publicada si es más nueva que esta; null si ya se tiene la última.
    /// </summary>
    /// <exception cref="UpdateException">Sin conexión o GitHub no responde.</exception>
    public static async Task<AppUpdate?> CheckAsync()
    {
        try
        {
            return await CheckWithApiAsync();
        }
        catch (UpdateException)
        {
            // La API de GitHub admite 60 consultas por hora desde una misma conexión sin cuenta. Si
            // no responde, se mira a qué versión lleva la página «latest» y se usa el nombre fijo del .zip.
            return await CheckWithRedirectAsync();
        }
    }

    private static async Task<AppUpdate?> CheckWithRedirectAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await NoRedirect.SendAsync(new HttpRequestMessage(HttpMethod.Head, ReleasesPage), timeout.Token);
            // https://github.com/…/releases/tag/v1.12
            string? location = response.Headers.Location?.ToString();
            string tag = location is null ? "" : location[(location.LastIndexOf('/') + 1)..];
            if (!TryParse(tag, out var latest))
                throw new UpdateException("No se puede comprobar si hay versiones nuevas. Comprueba la conexión a Internet.");
            if (latest <= Current)
                return null;
            string repo = ReleasesPage[..ReleasesPage.LastIndexOf("/latest", StringComparison.Ordinal)];
            return new AppUpdate(tag.TrimStart('v', 'V'), tag, "", $"{repo}/download/{tag}/Launcherito-{tag}-win-x64.zip",
                0, $"{repo}/tag/{tag}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UpdateException("No se puede comprobar si hay versiones nuevas. Comprueba la conexión a Internet.");
        }
    }

    private static async Task<AppUpdate?> CheckWithApiAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await Http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = json.RootElement;
            string tag = Text(root, "tag_name") ?? "";
            if (!TryParse(tag, out var latest) || latest <= Current)
                return null;

            // El .zip de Windows ("Launcherito-v1.12-win-x64.zip"); sin él no se puede instalar desde aquí.
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                string name = Text(asset, "name") ?? "";
                if (name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase) &&
                    Text(asset, "browser_download_url") is { } url)
                {
                    return new AppUpdate(tag.TrimStart('v', 'V'), Text(root, "name") ?? tag, Text(root, "body") ?? "",
                        url, asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                        Text(root, "html_url") ?? ReleasesPage);
                }
            }
            return null;   // publicada sin el .zip de Windows: para este programa no hay nada nuevo que instalar
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException)
        {
            throw new UpdateException("No se puede comprobar si hay versiones nuevas. Comprueba la conexión a Internet.");
        }
    }

    /// <summary>
    /// Descarga la versión y deja el .exe nuevo en el sitio del actual (que pasa a ser .old.exe).
    /// Después hay que abrir el nuevo con <see cref="Restart"/> y cerrar este.
    /// </summary>
    /// <param name="progress">De 0 a 1 mientras se descarga.</param>
    /// <exception cref="UpdateException">Sin conexión, sin permiso para escribir en la carpeta, o .zip raro.</exception>
    public static async Task InstallAsync(AppUpdate update, IProgress<double>? progress = null)
    {
        string exe = Environment.ProcessPath ?? throw new UpdateException("No se sabe dónde está Launcherito.exe.");
        string folder = Path.GetDirectoryName(exe)!;
        string zip = Path.Combine(Path.GetTempPath(), $"Launcherito-{update.Version}.zip");
        string fresh = Path.Combine(folder, $"Launcherito-{update.Version}.new");
        string old = Path.ChangeExtension(exe, null) + OldSuffix;

        try
        {
            await DownloadAsync(update, zip, progress);

            // Del .zip solo hace falta el .exe (el resto son archivos de depuración).
            using (var archive = ZipFile.OpenRead(zip))
            {
                var entry = archive.Entries.FirstOrDefault(e =>
                                string.Equals(e.Name, "Launcherito.exe", StringComparison.OrdinalIgnoreCase))
                            ?? throw new UpdateException("La descarga no trae Launcherito.exe. Descárgala a mano desde GitHub.");
                entry.ExtractToFile(fresh, overwrite: true);
            }

            if (File.Exists(old))
                File.Delete(old);
            File.Move(exe, old);          // el que está abierto se aparta…
            try
            {
                File.Move(fresh, exe);    // …y el nuevo ocupa su sitio
            }
            catch
            {
                File.Move(old, exe);      // si falla, se deja todo como estaba
                throw;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UpdateException("Se ha cortado la descarga. Comprueba la conexión e inténtalo otra vez.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new UpdateException(
                $"Launcherito no puede escribir en «{folder}». Descarga la versión nueva a mano desde GitHub " +
                "o mueve Launcherito.exe a una carpeta tuya (Escritorio, Documentos…).");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            throw new UpdateException("No se ha podido instalar la versión nueva: " + ex.Message);
        }
        finally
        {
            TryDelete(zip);
            TryDelete(fresh);
        }
    }

    /// <summary>Abre el Launcherito.exe nuevo (ya en el sitio del anterior). Este hay que cerrarlo después.</summary>
    public static void Restart()
    {
        if (Environment.ProcessPath is { } exe)
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true })?.Dispose();
    }

    /// <summary>Al arrancar: borra el .exe anterior que dejó la última actualización.</summary>
    public static void CleanUp()
    {
        if (Environment.ProcessPath is { } exe)
        {
            // Recién actualizado, el viejo puede seguir cerrándose: se reintenta un momento.
            string old = Path.ChangeExtension(exe, null) + OldSuffix;
            if (File.Exists(old))
                _ = Task.Run(async () =>
                {
                    for (int i = 0; i < 10 && File.Exists(old); i++)
                    {
                        TryDelete(old);
                        await Task.Delay(1000);
                    }
                });
        }
    }

    private static async Task DownloadAsync(AppUpdate update, string path, IProgress<double>? progress)
    {
        using var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? update.Size;
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(path);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read));
            done += read;
            if (total > 0)
                progress?.Report((double)done / total);
        }
    }

    /// <summary>"v1.12" o "1.10.1" → versión comparable.</summary>
    internal static bool TryParse(string tag, out Version version)
    {
        bool ok = Version.TryParse(tag.Trim().TrimStart('v', 'V'), out var parsed);
        version = ok ? Normalize(parsed!) : new Version(0, 0);
        return ok;
    }

    /// <summary>Las partes que faltan cuentan como 0: así «1.11» y «1.11.0.0» son la misma versión.</summary>
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Se borrará la próxima vez.
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static HttpClient CreateClient(HttpMessageHandler? handler = null)
    {
        // La descarga de 60-70 MB puede tardar: sin límite de tiempo total (se corta si se cae la conexión).
        var client = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Launcherito");   // GitHub exige un User-Agent
        return client;
    }
}
