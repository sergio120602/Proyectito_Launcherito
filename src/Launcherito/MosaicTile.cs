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
    // La carátula se decodifica al tamaño real de la tesela en píxeles (una normal mide ~190), con
    // este tope para las grandes. Cargarlas todas a 400 px costaba ~55 MB más con 90 canciones.
    private const int MaxCoverSize = 400;
    private const int CoverStep = 32;   // se redondea hacia arriba para no recargar por pocos píxeles
    // Mientras la tesela se anima se ve una copia diminuta de la carátula: estirada queda muy
    // desenfocada y apenas cuesta dibujarla. Así no hace falta un BlurEffect, que recalcularía el
    // desenfoque en cada fotograma. Al quedarse quieta se cambia por la nítida.
    private const int PreviewSize = 32;
    private const double Radius = 14;

    // Como mucho 4 lecturas de disco a la vez al desplazarse rápido por el mosaico.
    private static readonly SemaphoreSlim Loader = new(4);
    private static readonly Brush EmptyBackground = Frozen(new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x15)));
    private static readonly Brush PlaceholderBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));
    private static readonly Brush YouTubeRed = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x33)));
    // Marca de procedencia, arriba a la izquierda: morada con una nota si es tu .mp3, verde con las
    // tres ondas del logo si viene de Spotify.
    private static readonly Brush OwnPurple = Frozen(new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)));
    private static readonly Brush SpotifyGreen = Frozen(new SolidColorBrush(Color.FromRgb(0x1D, 0xB9, 0x54)));
    private static readonly Geometry SpotifyWaves = FrozenGeometry(
        "M 6.5,9.6 Q 12,7.6 17.5,10.6 M 7.3,12.7 Q 12,11.1 16.6,13.5 M 8.1,15.6 Q 12,14.4 15.6,16.3");
    private static readonly Brush GripBackground = Frozen(new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)));
    private static readonly Geometry GripLines = FrozenGeometry("M 17,7 L 7,17 M 17,12 L 12,17");
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly Brush Shade = Frozen(new LinearGradientBrush(
        Color.FromArgb(0x00, 0, 0, 0), Color.FromArgb(0xE6, 0, 0, 0), 90));

    private readonly Grid _root = new();
    private readonly TextBlock _placeholder;
    private Border? _hoverOverlay;
    private TextBlock? _hoverTitle;
    private Border? _activeOverlay;
    private TextBlock? _activeTitle;
    private TextBlock? _activeArtist;
    private Border? _youTubeBadge;
    private readonly Border _sourceBadge;
    private Grid? _videoHost;
    private Brush? _sharpCover;
    private Brush? _blurredCover;
    private bool _isAnimating;
    private bool _isEditable;
    private bool _coverWanted;
    private int _coverVersion;   // invalida cargas en curso cuando la carátula se suelta
    private int _coverPixels;    // ancho con el que se ha decodificado la carátula nítida actual

    public MosaicTile(string path)
    {
        SongPath = path;
        Title = SongInfo.FallbackTitle(path);
        CornerRadius = new CornerRadius(Radius);
        Background = EmptyBackground;
        Cursor = Cursors.Hand;
        ToolTip = Title;

        _placeholder = new TextBlock
        {
            Text = "",
            FontFamily = IconFont,
            FontSize = 48,
            Foreground = PlaceholderBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _root.Children.Add(_placeholder);
        bool spotify = SpotifyLibrary.IsSpotify(path);
        _sourceBadge = spotify ? SpotifyBadge() : OwnBadge();
        _root.Children.Add(_sourceBadge);
        if (spotify)
        {
            _youTubeBadge = YouTubeBadge();
            _root.Children.Add(_youTubeBadge);
        }
        Child = _root;
    }

    /// <summary>Círculo de la esquina de arriba a la izquierda que dice de dónde es la canción.</summary>
    private static Border SourceBadge(Brush background, UIElement content) => new()
    {
        Width = 24,
        Height = 24,
        Margin = new Thickness(10),
        CornerRadius = new CornerRadius(12),
        Background = background,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
        Child = content,
    };

    /// <summary>Tu música: un .mp3 tuyo, que suena entero.</summary>
    private static Border OwnBadge() => SourceBadge(OwnPurple, new TextBlock
    {
        Text = "",   // nota musical
        FontFamily = IconFont,
        FontSize = 12,
        Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    });

    /// <summary>De Spotify: el círculo verde con las tres ondas negras del logo.</summary>
    private static Border SpotifyBadge() => SourceBadge(SpotifyGreen, new System.Windows.Shapes.Path
    {
        Data = SpotifyWaves,
        Stroke = Brushes.Black,
        StrokeThickness = 1.8,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    });

    /// <summary>Marca roja con el triángulo de reproducir: al pulsar la portada se ve su vídeo de YouTube.</summary>
    private static Border YouTubeBadge() => new()
    {
        Width = 34,
        Height = 24,
        Margin = new Thickness(10),
        CornerRadius = new CornerRadius(7),
        Background = YouTubeRed,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
        Child = new TextBlock
        {
            Text = "",   // reproducir
            FontFamily = IconFont,
            FontSize = 11,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    /// <summary>Se pulsa una tesela que no es la que está sonando.</summary>
    public event EventHandler? Selected;

    /// <summary>Se pulsa la X del vídeo.</summary>
    public event EventHandler? VideoClosed;

    public string SongPath { get; }
    public string Title { get; private set; }
    public string Artist { get; private set; } = "";
    public bool IsActive { get; private set; }

    /// <summary>Celdas de lado que ocupa la portada: la elegida por el usuario o la automática.</summary>
    public int BaseSpan { get; set; } = 1;

    /// <summary>La que suena y la del vídeo ocupan al menos 2x2, para que quepan los controles o el vídeo.</summary>
    public int MinSpan => IsActive || HasVideo ? 2 : 1;

    /// <summary>Celdas de lado con las que se coloca en el mosaico.</summary>
    public int EffectiveSpan => Math.Max(MinSpan, BaseSpan);

    /// <summary>Tirador de la esquina para agrandar o encoger la portada (se crea la primera vez que se pasa el ratón).</summary>
    public FrameworkElement? ResizeGrip { get; private set; }

    /// <summary>
    /// Modo edición: la portada se puede arrastrar (cursor de mover) y lleva siempre a la vista el
    /// tirador de la esquina. Fuera de él se comporta como siempre.
    /// </summary>
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            if (_isEditable == value)
                return;
            _isEditable = value;
            if (value)
                ResizeGrip ??= BuildResizeGrip();
            if (ResizeGrip is not null)
                ResizeGrip.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            UpdateCursor();
        }
    }

    /// <summary>Se está viendo un vídeo de YouTube dentro de la portada.</summary>
    public bool HasVideo => _videoHost is not null;

    /// <summary>Hueco dentro de la portada activa donde se colocan la barra y los botones.</summary>
    public Decorator? ControlsHost { get; private set; }

    /// <summary>Lo activa TileAnimator mientras la tesela se mueve: entretanto la carátula se ve desenfocada.</summary>
    public bool IsAnimating
    {
        get => _isAnimating;
        set
        {
            if (_isAnimating == value)
                return;
            _isAnimating = value;
            ShowCover();
        }
    }

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
        _coverPixels = 0;
        _sharpCover = null;
        _blurredCover = null;
        ShowCover();
    }

    /// <summary>Ancho en píxeles de pantalla con el que se ve la tesela (teniendo en cuenta el escalado de Windows).</summary>
    private int NeededCoverSize()
    {
        if (ActualWidth <= 0)
            return MaxCoverSize;
        double pixels = ActualWidth * VisualTreeHelper.GetDpi(this).DpiScaleX;
        return Math.Min(MaxCoverSize, (int)Math.Ceiling(pixels / CoverStep) * CoverStep);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        // Si la tesela crece (pasa a ser la que suena o se agranda la ventana), se recarga más nítida;
        // mientras tanto sigue viéndose la que había.
        if (_coverWanted && _sharpCover is not null && NeededCoverSize() > _coverPixels)
            _ = LoadCoverAsync(++_coverVersion);
    }

    private async Task LoadCoverAsync(int version)
    {
        SongInfo info;
        int size = NeededCoverSize();
        await Loader.WaitAsync();
        try
        {
            if (version != _coverVersion)
                return;
            info = await Task.Run(() => SongInfo.Read(SongPath, size, PreviewSize));
        }
        finally
        {
            Loader.Release();
        }

        // De vuelta en el hilo de la interfaz.
        Title = info.Title;
        Artist = info.Artist;
        if (!HasVideo)
            ToolTip = $"{Title} — {Artist}";
        UpdateTexts();

        if (version != _coverVersion || info.Cover is null)
            return;
        _sharpCover = CoverBrush(info.Cover);
        _coverPixels = size;
        if (NeededCoverSize() > size)
            _ = LoadCoverAsync(++_coverVersion);   // ha crecido mientras se leía
        _blurredCover = info.Preview is null ? null : CoverBrush(info.Preview);
        ShowCover();
    }

    private void ShowCover()
    {
        var cover = _isAnimating ? _blurredCover ?? _sharpCover : _sharpCover;
        Background = cover ?? EmptyBackground;
        _placeholder.Visibility = cover is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Brush CoverBrush(ImageSource image)
    {
        var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        // Escalado lineal: suaviza los píxeles de la copia diminuta al estirarla, que es lo que la desenfoca.
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.Linear);
        return Frozen(brush);
    }

    /// <summary>Marca la tesela como la que suena: muestra título, artista y un hueco para los controles.</summary>
    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;
        IsActive = active;
        ToolTip = active ? null : $"{Title} — {Artist}";
        UpdateCursor();

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

    /// <summary>Pone el vídeo encima de la carátula, recortado con las mismas esquinas redondeadas, y una X para cerrarlo.</summary>
    public void ShowVideo(UIElement video)
    {
        HideVideo();
        HideHover();
        ToolTip = null;
        _sourceBadge.Visibility = Visibility.Collapsed;
        if (_youTubeBadge is not null)
            _youTubeBadge.Visibility = Visibility.Collapsed;

        var close = new Button
        {
            Style = (Style)FindResource("VideoCloseButton"),
            Content = "\uE711",   // cerrar
            ToolTip = "Cerrar el vídeo",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10),
        };
        close.Click += (_, _) => VideoClosed?.Invoke(this, EventArgs.Empty);

        _videoHost = new Grid();
        _videoHost.Children.Add(video);
        _videoHost.Children.Add(close);
        _videoHost.SizeChanged += (_, e) => _videoHost.Clip = new RectangleGeometry(
            new Rect(e.NewSize), Radius, Radius);
        _root.Children.Add(_videoHost);
        UpdateCursor();
    }

    /// <summary>Quita el vídeo (quien lo creó lo libera) y vuelve a mostrar la carátula.</summary>
    public void HideVideo()
    {
        if (_videoHost is null)
            return;
        _videoHost.Children.Clear();
        _root.Children.Remove(_videoHost);
        _videoHost = null;
        ToolTip = $"{Title} — {Artist}";
        UpdateCursor();
        _sourceBadge.Visibility = Visibility.Visible;
        if (_youTubeBadge is not null)
            _youTubeBadge.Visibility = Visibility.Visible;
    }

    /// <summary>Mano para pulsar; flechas de mover en modo edición; flecha normal en la que suena o la del vídeo.</summary>
    private void UpdateCursor() =>
        Cursor = _isEditable ? Cursors.SizeAll : IsActive || HasVideo ? Cursors.Arrow : Cursors.Hand;

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
        if (IsActive || HasVideo)
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
                Margin = new Thickness(12, 28, 34, 10),   // a la derecha, sitio para el tirador
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

    /// <summary>Dos rayas en diagonal sobre un cuadrado oscuro, abajo a la derecha y por encima de todo (también del vídeo).</summary>
    private FrameworkElement BuildResizeGrip()
    {
        var grip = new Border
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(0, 0, 4, 4),
            CornerRadius = new CornerRadius(7),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = GripBackground,
            Cursor = Cursors.SizeNWSE,
            ToolTip = "Arrastra para cambiar el tamaño",
            Child = new System.Windows.Shapes.Path
            {
                Data = GripLines,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            },
        };
        Panel.SetZIndex(grip, 10);
        _root.Children.Add(grip);
        return grip;
    }

    private void HideHover()
    {
        if (_hoverOverlay is not null)
            _root.Children.Remove(_hoverOverlay);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!IsActive && !HasVideo)
            Selected?.Invoke(this, EventArgs.Empty);
    }

    private static Geometry FrozenGeometry(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
