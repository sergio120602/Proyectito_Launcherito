using System.Windows;

namespace Launcherito;

/// <summary>
/// Arranque de Launcherito. Normalmente abre la ventana. Con <see cref="TrendingPlaylists.RefreshArgument"/>
/// (lo usa la tarea programada de las 7:00) no abre nada: actualiza las playlists del momento y se cierra.
/// </summary>
public partial class App : Application
{
    private async void App_Startup(object sender, StartupEventArgs e)
    {
        if (e.Args.Contains(TrendingPlaylists.RefreshArgument, StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;   // sin ventana: se cierra al terminar
            int exitCode = 0;
            try
            {
                await TrendingPlaylists.RefreshAsync(TrendingPlaylists.Load());
            }
            catch (Exception)
            {
                exitCode = 1;   // ya queda apuntado en del-momento.log; la app lo reintentará al abrirse
            }
            Shutdown(exitCode);
            return;
        }

        new MainWindow().Show();
    }
}
