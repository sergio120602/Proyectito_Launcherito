using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Launcherito;

/// <summary>
/// Tarjeta de un artista (foto redonda) o de un género (foto rectangular) con su nombre y
/// el número de canciones. La foto se descarga en segundo plano; mientras tanto se ve un icono.
/// </summary>
public sealed class CategoryCard : Button
{
    private const int PictureDecodeSize = 240;

    // Compartidos por todas las tarjetas: congelados, no hace falta una copia por tarjeta.
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly Brush EmptyFill = Frozen(new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x15)));
    private static readonly Brush PlaceholderBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));
    private static readonly Brush CountBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)));

    private readonly bool _round;
    private readonly Shape _picture;
    private readonly TextBlock _count;
    private string? _pictureUrl;

    public CategoryCard(string name, bool round)
    {
        Key = name;
        _round = round;
        Width = round ? 170 : 230;
        Margin = new Thickness(0, 0, 18, 22);
        Cursor = System.Windows.Input.Cursors.Hand;
        ToolTip = name;

        double width = round ? 150 : 230;
        double height = round ? 150 : 140;
        _picture = round
            ? new Ellipse { Width = width, Height = height }
            : new Rectangle { Width = width, Height = height, RadiusX = 14, RadiusY = 14 };

        var placeholder = new TextBlock
        {
            Text = round ? "" : "",   // persona / nota musical
            FontFamily = IconFont,
            FontSize = 46,
            Foreground = PlaceholderBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Shape background = round
            ? new Ellipse { Width = width, Height = height }
            : new Rectangle { Width = width, Height = height, RadiusX = 14, RadiusY = 14 };
        background.Fill = EmptyFill;

        var pictureArea = new Grid { Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Center };
        pictureArea.Children.Add(background);
        pictureArea.Children.Add(placeholder);
        pictureArea.Children.Add(_picture);

        var title = new TextBlock
        {
            Text = name,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = round ? TextAlignment.Center : TextAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0),
        };
        _count = new TextBlock
        {
            FontSize = 12,
            Foreground = CountBrush,
            TextAlignment = round ? TextAlignment.Center : TextAlignment.Left,
        };

        var content = new StackPanel();
        content.Children.Add(pictureArea);
        content.Children.Add(title);
        content.Children.Add(_count);
        Content = content;
    }

    /// <summary>Nombre del artista o del género.</summary>
    public string Key { get; }

    /// <summary>Copia pequeña de la foto (misma imagen ya descargada) para la cabecera del detalle.</summary>
    public FrameworkElement CreateThumbnail(double size)
    {
        Shape shape = _round
            ? new Ellipse { Width = size, Height = size }
            : new Rectangle { Width = size * 1.6, Height = size, RadiusX = 10, RadiusY = 10 };
        shape.Fill = _picture.Fill ?? EmptyFill;
        return shape;
    }

    /// <summary>Ya se ha pedido una foto alternativa (para no repetir la petición en cada redibujado).</summary>
    public bool FallbackRequested { get; set; }

    public bool HasPicture => _pictureUrl is not null;

    public void SetCount(int songs) => _count.Text = songs == 1 ? "1 canción" : $"{songs} canciones";

    public void SetPicture(string? url)
    {
        if (string.IsNullOrEmpty(url) || (url == _pictureUrl && _picture.Fill is not null))
            return;
        _pictureUrl = url;
        try
        {
            // WPF descarga la imagen en segundo plano; DecodePixelWidth evita guardarla a 500 px.
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(url);
            image.DecodePixelWidth = PictureDecodeSize;
            image.EndInit();
            _picture.Fill = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        }
        catch (Exception ex) when (ex is UriFormatException or NotSupportedException)
        {
            // URL no válida: se queda el icono.
        }
    }

    /// <summary>Suelta la foto decodificada mientras la tarjeta no se ve; la URL se conserva.</summary>
    public void ReleasePicture() => _picture.Fill = null;

    /// <summary>Vuelve a cargar la foto soltada con <see cref="ReleasePicture"/>, si se conoce su URL.</summary>
    public void RestorePicture() => SetPicture(_pictureUrl);

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
