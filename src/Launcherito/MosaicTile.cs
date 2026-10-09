using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Launcherito;

/// <summary>
/// Una portada del mosaico. La carátula se carga en segundo plano solo cuando la tesela está
/// cerca de la zona visible y se suelta al alejarse, para que una lista larga no llene la memoria.
/// </summary>
public sealed class MosaicTile : Border
{
    // Carátula a propósito diminuta: al estirarla hasta la tesela queda muy desenfocada. Se lee,
    // decodifica y guarda ~150 veces menos píxeles que a 400 px, y no hace falta un BlurEffect, que
    // obligaría a recalcular el desenfoque en cada fotograma (sobre todo durante las animaciones).
    private const int CoverSize = 32;
    private const double Radius = 14;

    // Como mucho 4 lecturas de disco a la vez al desplazarse rápido por el mosaico.
    private static readonly SemaphoreSlim Loader = new(4);
    private static readonly Brush EmptyBackground = Frozen(new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x15)));
    private static readonly Brush Shade = Frozen(new LinearGradientBrush(
        Color.FromArgb(0x00, 0, 0, 0), Color.FromArgb(0xE6, 0, 0, 0), 90));

    private readonly Grid _root = new();
    private readonly TextBlock _placeholder;
    private Border? _hoverOverlay;
    private TextBlock? _hoverTitle;
    private Border? _activeOverlay;
    private TextBlock? _activeTitle;
    private TextBlock? _activeArtist;
    private bool _coverWanted;
    private int _coverVersion;   // invalida cargas en curso cuando la carátula se suelta

    public MosaicTile(string path)
    {
        SongPath = path;
        Title = System.IO.Path.GetFileNameWithoutExtension(path);
        CornerRadius = new CornerRadius(Radius);
        Background = EmptyBackground;
        Cursor = Cursors.Hand;
        ToolTip = Title;

        _placeholder = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 48,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _root.Children.Add(_placeholder);
        Child = _root;
    }

    /// <summary>Se pulsa una tesela que no es la que está sonando.</summary>
    public event EventHandler? Selected;

    public string SongPath { get; }
    public string Title { get; private set; }
    public string Artist { get; private set; } = "";
    public bool IsActive { get; private set; }

    /// <summary>Hueco dentro de la portada activa donde se colocan la barra y los botones.</summary>
    public Decorator? ControlsHost { get; private set; }

    public void EnsureCover()
    {
        if (_coverWanted)
            return;
        _coverWanted = true;
        _ = LoadCoverAsync(++_coverVersion);
    }

    public void ReleaseCover()
    {
        if (!_coverWanted)
            return;
        _coverWanted = false;
        _coverVersion++;
        Background = EmptyBackground;
        _placeholder.Visibility = Visibility.Visible;
    }

    private async Task LoadCoverAsync(int version)
    {
        SongInfo info;
        await Loader.WaitAsync();
        try
        {
            if (version != _coverVersion)
                return;
            info = await Task.Run(() => SongInfo.Read(SongPath, CoverSize));
        }
        finally
        {
            Loader.Release();
        }

        // De vuelta en el hilo de la interfaz.
        Title = info.Title;
        Artist = info.Artist;
        ToolTip = $"{Title} — {Artist}";
        UpdateTexts();

        if (version != _coverVersion || info.Cover is null)
            return;
        var cover = new ImageBrush(info.Cover) { Stretch = Stretch.UniformToFill };
        // Escalado lineal: suaviza los píxeles al estirarla, que es lo que da el desenfoque.
        RenderOptions.SetBitmapScalingMode(cover, BitmapScalingMode.Linear);
        cover.Freeze();
        Background = cover;
        _placeholder.Visibility = Visibility.Collapsed;
    }

    /// <summary>Marca la tesela como la que suena: muestra título, artista y un hueco para los controles.</summary>
    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;
        IsActive = active;
        ToolTip = active ? null : $"{Title} — {Artist}";
        Cursor = active ? Cursors.Arrow : Cursors.Hand;

        if (active)
        {
            HideHover();
            _activeOverlay ??= BuildActiveOverlay();
            _root.Children.Add(_activeOverlay);
            BorderBrush = (Brush)FindResource("Accent");
            BorderThickness = new Thickness(2);
        }
        else
        {
            if (ControlsHost is not null)
                ControlsHost.Child = null;
            _root.Children.Remove(_activeOverlay);
            BorderThickness = new Thickness(0);
        }
        UpdateTexts();
    }

    private Border BuildActiveOverlay()
    {
        _activeTitle = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis };
        _activeArtist = new TextBlock { FontSize = 13, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 2, 0, 6), TextTrimming = TextTrimming.CharacterEllipsis };
        // Viewbox reduce los controles si la portada es más estrecha que ellos.
        ControlsHost = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };

        var content = new StackPanel { Margin = new Thickness(16, 48, 16, 10) };
        content.Children.Add(_activeTitle);
        content.Children.Add(_activeArtist);
        content.Children.Add(ControlsHost);

        return new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(0, 0, Radius, Radius),
            Background = Shade,
            Child = content,
        };
    }

    private void UpdateTexts()
    {
        if (_activeTitle is not null)
        {
            _activeTitle.Text = Title;
            _activeArtist!.Text = Artist;
        }
        if (_hoverTitle is not null)
            _hoverTitle.Text = Title;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (IsActive)
            return;

        if (_hoverOverlay is null)
        {
            _hoverTitle = new TextBlock
            {
                Text = Title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(12, 28, 12, 10),
            };
            _hoverOverlay = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(0, 0, Radius, Radius),
                Background = Shade,
                IsHitTestVisible = false,
                Child = _hoverTitle,
            };
        }
        if (!_root.Children.Contains(_hoverOverlay))
            _root.Children.Add(_hoverOverlay);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HideHover();
    }

    private void HideHover()
    {
        if (_hoverOverlay is not null)
            _root.Children.Remove(_hoverOverlay);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!IsActive)
            Selected?.Invoke(this, EventArgs.Empty);
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
