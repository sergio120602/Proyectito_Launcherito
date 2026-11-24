using System.Windows;

namespace Launcherito;

/// <summary>
/// Botón de actualizar: en la pantalla inicial se ve la versión abierta y «Buscar actualizaciones».
/// Al abrir el programa se mira en silencio si hay una versión nueva; si la hay, sale «Actualizar a
/// la X» (también arriba, en el reproductor). Al pulsarlo se descarga de GitHub, se pone en el sitio
/// del .exe y Launcherito se vuelve a abrir solo, con tus canciones y playlists como estaban.
/// </summary>
public partial class MainWindow
{
    private AppUpdate? _update;     // versión nueva encontrada (null: no se sabe o no hay)
    private bool _updateBusy;       // buscando o descargando

    private void InitUpdates()
    {
        Updater.CleanUp();   // el .exe anterior, si se acaba de actualizar
        VersionText.Text = $"Versión {Updater.CurrentText}";
        // Sin molestar: si falla (sin conexión) no se dice nada; el botón sigue para buscar a mano.
        Loaded += async (_, _) => await CheckForUpdateAsync(quiet: true);
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updateBusy)
            return;
        if (_update is { } update)
            await InstallUpdateAsync(update);
        else
            await CheckForUpdateAsync(quiet: false);
    }

    /// <summary>Pregunta a GitHub si hay una versión más nueva y cambia los botones.</summary>
    private async Task CheckForUpdateAsync(bool quiet)
    {
        _updateBusy = true;
        if (!quiet)
            ShowUpdateStatus("Buscando versiones nuevas…");
        try
        {
            _update = await Updater.CheckAsync();
            if (_update is null && !quiet)
                ShowUpdateStatus($"Tienes la última versión ({Updater.CurrentText}).");
            else if (_update is not null)
                ShowUpdateStatus(null);
        }
        catch (UpdateException ex)
        {
            if (!quiet)
                ShowUpdateStatus(ex.Message);
        }
        finally
        {
            _updateBusy = false;
        }
        UpdateUpdateButtons();
    }

    private void UpdateUpdateButtons()
    {
        bool available = _update is not null;
        UpdateButton.Visibility = available ? Visibility.Collapsed : Visibility.Visible;
        InstallUpdateButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        TopUpdateButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        if (_update is { } update)
        {
            InstallUpdateText.Text = $"Actualizar a la {update.Version}";
            InstallUpdateButton.ToolTip = update.Title;
            TopUpdateButton.Content = $"⬆  Actualizar a la {update.Version}";
            TopUpdateButton.ToolTip = $"{update.Title}: se descarga y Launcherito se vuelve a abrir solo";
        }
    }

    /// <summary>Descarga e instala la versión nueva, y vuelve a abrir Launcherito.</summary>
    private async Task InstallUpdateAsync(AppUpdate update)
    {
        string size = update.Size > 0 ? $" (unos {update.Size / 1_048_576} MB)" : "";
        string notes = update.Notes.Length > 0 ? "\n\n" + Shorten(update.Notes, 600) : "";
        if (MessageBox.Show(this,
                $"¿Actualizar Launcherito de la {Updater.CurrentText} a la {update.Version}?{notes}\n\n" +
                $"Se descarga de GitHub{size} y Launcherito se vuelve a abrir solo. Tus canciones, playlists y " +
                "ajustes se quedan como están; la música se para un momento.",
                $"Actualizar a «{update.Title}»", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _updateBusy = true;
        InstallUpdateButton.IsEnabled = false;
        TopUpdateButton.IsEnabled = false;
        var progress = new Progress<double>(p =>
        {
            string text = $"Descargando la {update.Version}… {(int)(p * 100)} %";
            ShowUpdateStatus(text);
            TopUpdateButton.Content = text;
        });
        ShowUpdateStatus($"Descargando la {update.Version}…");
        try
        {
            await Updater.InstallAsync(update, progress);
        }
        catch (UpdateException ex)
        {
            _updateBusy = false;
            InstallUpdateButton.IsEnabled = true;
            TopUpdateButton.IsEnabled = true;
            UpdateUpdateButtons();
            ShowUpdateStatus(ex.Message);
            if (MessageBox.Show(this, ex.Message + "\n\n¿Abrir la página de la versión en GitHub para descargarla a mano?",
                    "Launcherito", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                YouTubeLinks.Open(update.PageUrl);
            return;
        }

        // Al cerrar la ventana se guarda todo (canciones, catálogo, playlists); después se abre la nueva.
        ShowUpdateStatus("Instalada. Abriendo la versión nueva…");
        Close();
        Updater.Restart();
        Application.Current.Shutdown();
    }

    private void ShowUpdateStatus(string? text)
    {
        UpdateStatusText.Text = text ?? "";
        UpdateStatusText.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string Shorten(string text, int max) =>
        text.Length <= max ? text.Trim() : text[..max].TrimEnd() + "…";
}
