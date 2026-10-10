using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Launcherito;

/// <summary>
/// Modo edición del mosaico (solo mientras <see cref="IsEditing"/>): una portada se coge y se arrastra a otro sitio (las demás se
/// apartan), y con el tirador de su esquina se agranda o se encoge de celda en celda. Al agrandarla,
/// las grandes que quedan debajo se encogen para dejarle sitio; al encogerla, la siguiente crece y
/// ocupa el hueco.
/// </summary>
internal sealed class MosaicEditor
{
    // Hay que mover el ratón algo más que en un clic tembloroso para que empiece el arrastre.
    private const double DragThreshold = 8;
    // Al llevar una portada cerca del borde de arriba o de abajo, el mosaico se desplaza solo.
    private const double ScrollZone = 48;
    private const double ScrollStep = 14;

    private readonly ScrollViewer _viewer;
    private readonly MosaicPanel _panel;
    private readonly TileAnimator _animator;
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    private MosaicTile? _pressed;      // pulsada, pero aún sin mover lo bastante para arrastrarla
    private Point _pressPoint;
    private MosaicTile? _dragged;
    private Vector _grab;              // dónde se ha cogido, desde la esquina de la portada
    private MosaicTile? _lastTarget;   // la última sobre la que se ha soltado sitio, para no ir y volver
    private MosaicTile? _resized;
    private Rect _resizeOrigin;
    private int _startSpan;
    private Dictionary<MosaicTile, (int Span, Rect Slot)>? _before;   // cómo estaba todo al coger el tirador

    public MosaicEditor(ScrollViewer viewer, MosaicPanel panel, TileAnimator animator)
    {
        _viewer = viewer;
        _panel = panel;
        _animator = animator;
        _panel.PreviewMouseLeftButtonDown += Panel_PreviewMouseLeftButtonDown;
        _panel.PreviewMouseMove += Panel_PreviewMouseMove;
        _panel.PreviewMouseLeftButtonUp += Panel_PreviewMouseLeftButtonUp;
        _panel.LostMouseCapture += (_, _) => End();
        _scrollTimer.Tick += (_, _) => AutoScroll();
    }

    private bool _isEditing;

    /// <summary>
    /// Si se pueden mover y redimensionar las portadas. Fuera del modo edición el mosaico no se toca
    /// y un clic en una portada la reproduce; dentro, un clic no hace nada.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value)
                return;
            _isEditing = value;
            if (!value)
            {
                _pressed = null;
                End();   // si se sale con una portada cogida, se suelta donde esté
            }
        }
    }

    /// <summary>Se ha soltado una portada o el tirador: el orden o los tamaños han cambiado.</summary>
    public event EventHandler? Changed;

    /// <summary>Las portadas en el orden en que se colocan.</summary>
    public List<MosaicTile> Ordered() => _panel.Children.Cast<MosaicTile>().OrderBy(MosaicPanel.GetOrder).ToList();

    private void Panel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isEditing || _dragged is not null || _resized is not null)
            return;
        var tile = TileUnder(e.OriginalSource as DependencyObject, out bool onGrip, out bool onControl);
        if (tile is null || onControl)
            return;   // los botones, la barra y el vídeo siguen funcionando como siempre

        if (onGrip)
        {
            BeginResize(tile);
            e.Handled = true;
            return;
        }
        _pressed = tile;
        _pressPoint = e.GetPosition(_panel);
    }

    private void Panel_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(_panel);
        if (_resized is not null)
        {
            Resize(point);
            return;
        }
        if (_dragged is not null)
        {
            Drag(point);
            return;
        }
        if (_pressed is null)
            return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _pressed = null;
            return;
        }
        if ((point - _pressPoint).Length >= DragThreshold)
            BeginDrag(_pressed);
    }

    private void Panel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        bool click = _pressed is not null;
        _pressed = null;
        if (_dragged is null && _resized is null)
        {
            e.Handled = click;   // en modo edición un clic sin arrastrar no reproduce la canción
            return;
        }
        End();
        e.Handled = true;   // que soltarla no cuente como pulsarla
    }

    // ───────────────────────────── Mover ─────────────────────────────

    private void BeginDrag(MosaicTile tile)
    {
        _pressed = null;
        _dragged = tile;
        _animator.Finish(tile);
        _animator.Held = tile;
        _grab = _pressPoint - Slot(tile).TopLeft;
        tile.RenderTransform = new TranslateTransform();
        Panel.SetZIndex(tile, 10);   // por encima de las demás mientras se lleva
        _panel.CaptureMouse();
        _scrollTimer.Start();
        Drag(Mouse.GetPosition(_panel));
    }

    private void Drag(Point point)
    {
        var tile = _dragged!;
        var target = TileAt(point, except: tile);
        if (target != _lastTarget)
        {
            _lastTarget = target;
            if (target is not null)
                MoveBefore(tile, target);
        }

        // La portada sigue al ratón; su sitio puede haber cambiado al reordenar.
        if (tile.RenderTransform is TranslateTransform move)
        {
            var slot = Slot(tile);
            move.X = point.X - _grab.X - slot.X;
            move.Y = point.Y - _grab.Y - slot.Y;
        }
    }

    /// <summary>Pone la portada en el sitio de <paramref name="target"/>; las de en medio corren un puesto.</summary>
    private void MoveBefore(MosaicTile tile, MosaicTile target)
    {
        var tiles = Ordered();
        int to = tiles.IndexOf(target);
        tiles.Remove(tile);
        tiles.Insert(to, tile);
        Relayout(() =>
        {
            for (int i = 0; i < tiles.Count; i++)
                MosaicPanel.SetOrder(tiles[i], i);
        });
    }

    private void AutoScroll()
    {
        if (_dragged is null)
            return;
        double y = Mouse.GetPosition(_viewer).Y;
        double step = y < ScrollZone ? -ScrollStep
            : y > _viewer.ViewportHeight - ScrollZone ? ScrollStep
            : 0;
        if (step == 0)
            return;
        _viewer.ScrollToVerticalOffset(_viewer.VerticalOffset + step);
        _viewer.UpdateLayout();
        Drag(Mouse.GetPosition(_panel));
    }

    // ───────────────────────────── Tamaño ─────────────────────────────

    private void BeginResize(MosaicTile tile)
    {
        _resized = tile;
        _animator.Finish(tile);
        _resizeOrigin = Slot(tile);
        _startSpan = tile.EffectiveSpan;
        _before = _panel.Children.Cast<MosaicTile>().ToDictionary(t => t, t => (t.BaseSpan, Slot(t)));
        _panel.CaptureMouse();
    }

    private void Resize(Point point)
    {
        var tile = _resized!;
        // El tamaño lo marca el ratón respecto a la esquina de arriba a la izquierda, por la diagonal.
        double size = Math.Max(point.X - _resizeOrigin.X, point.Y - _resizeOrigin.Y);
        int span = Math.Max(tile.MinSpan, _panel.SpanForSize(size));
        if (span == tile.EffectiveSpan)
            return;

        Relayout(() =>
        {
            // Siempre desde como estaba al coger el tirador, así ir y volver no deja cambios sueltos.
            foreach (var (other, before) in _before!)
                other.BaseSpan = before.Span;
            tile.BaseSpan = span;
            if (span > _startSpan)
                ShrinkCovered(tile, span);
            else if (span < _startSpan)
                GrowNext(tile, _startSpan - span);
            foreach (MosaicTile other in _panel.Children)
                MosaicPanel.SetSpan(other, other.EffectiveSpan);
        });
    }

    /// <summary>Las grandes que quedarían debajo de la portada agrandada pasan a ocupar una celda.</summary>
    private void ShrinkCovered(MosaicTile tile, int span)
    {
        var area = _panel.AreaFrom(_resizeOrigin, span);
        area.Inflate(-1, -1);   // las que solo tocan el borde no cuentan
        foreach (var (other, before) in _before!)
        {
            if (other != tile && other.Parent == _panel && other.BaseSpan > 1 && before.Slot.IntersectsWith(area))
                other.BaseSpan = 1;
        }
    }

    /// <summary>La siguiente portada (o la anterior, si es la última) crece lo que ha encogido esta.</summary>
    private void GrowNext(MosaicTile tile, int freed)
    {
        var tiles = Ordered();
        int at = tiles.IndexOf(tile);
        var neighbour = tiles.Skip(at + 1).Concat(tiles.Take(at).Reverse())
            .FirstOrDefault(other => !other.IsActive && !other.HasVideo);
        if (neighbour is not null)
            neighbour.BaseSpan = Math.Min(_panel.Columns, neighbour.BaseSpan + freed);
    }

    // ───────────────────────────── Común ─────────────────────────────

    /// <summary>Suelta lo que se lleve cogido: la portada vuela hasta su sitio y se avisa del cambio.</summary>
    private void End()
    {
        _scrollTimer.Stop();
        if (_dragged is { } tile)
        {
            _dragged = null;   // antes de soltar el ratón, que vuelve a llamar aquí
            _lastTarget = null;
            _animator.Held = null;
            var from = Slot(tile);
            if (tile.RenderTransform is TranslateTransform move)
                from.Offset(move.X, move.Y);
            tile.ClearValue(UIElement.RenderTransformProperty);
            tile.ClearValue(Panel.ZIndexProperty);
            _animator.Settle(tile, from);
        }
        else if (_resized is not null)
        {
            _resized = null;
            _before = null;
        }
        else
        {
            return;
        }

        if (_panel.IsMouseCaptured)
            _panel.ReleaseMouseCapture();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Aplica un cambio de orden o de tamaños y anima a las portadas que cambian de sitio.</summary>
    private void Relayout(Action change)
    {
        var oldSlots = _animator.CaptureSlots();
        change();
        _panel.UpdateLayout();
        _animator.Animate([], oldSlots, active: null, activeLocked: false);
    }

    private MosaicTile? TileAt(Point point, MosaicTile except)
    {
        foreach (MosaicTile tile in _panel.Children)
        {
            if (tile != except && Slot(tile).Contains(point))
                return tile;
        }
        return null;
    }

    /// <summary>La portada que contiene <paramref name="source"/>, y si se ha pulsado en su tirador o en un control suyo.</summary>
    private MosaicTile? TileUnder(DependencyObject? source, out bool onGrip, out bool onControl)
    {
        onGrip = false;
        onControl = false;
        var path = new List<DependencyObject>();
        for (var node = source; node is not null; node = Parent(node))
        {
            if (node is MosaicTile tile)
            {
                onGrip = tile.ResizeGrip is { } grip && path.Contains(grip);
                return tile;
            }
            if (node is Control)
                onControl = true;
            path.Add(node);
        }
        return null;
    }

    private static DependencyObject? Parent(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);   // p. ej. un Run dentro de un TextBlock

    private static Rect Slot(MosaicTile tile) => LayoutInformation.GetLayoutSlot(tile);
}
