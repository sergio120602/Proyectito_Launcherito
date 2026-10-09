using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Launcherito;

/// <summary>
/// Animaciones del mosaico: cada canción nueva entra con una animación elegida al azar y las portadas
/// que ya estaban se desplazan hasta su nuevo sitio en lugar de saltar. Las nuevas entran en tandas de
/// 10: mientras esperan están ocultas y sin animación en marcha. Al terminar, cada tesela suelta sus
/// transformaciones y relojes, así que no queda nada en memoria.
/// </summary>
internal sealed class TileAnimator
{
    private enum Intro { Spin, Grow, Crash, Drop, Skid, Card, Flicker }

    private static readonly Intro[] Intros = Enum.GetValues<Intro>();
    // Todas las animaciones van 2,5 veces más lentas que los tiempos escritos abajo, para que se vean bien.
    private const double Slowdown = 2.5;
    // Las nuevas entran de 10 en 10, una tras otra dentro de cada tanda (150 ms reales entre una y la
    // siguiente). La tanda siguiente empieza cuando la anterior ha terminado de colocarse.
    private const int WaveSize = 10;
    private const double WaveStepMs = 150;
    private const double LongestIntroMs = 900;   // la entrada más larga (Drop), sin ralentizar

    // Rutas a las transformaciones del TransformGroup que monta Prepare (escala, sesgo, giro, traslación).
    private const string ScaleX = "(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)";
    private const string ScaleY = "(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)";
    private const string SkewX = "(UIElement.RenderTransform).(TransformGroup.Children)[1].(SkewTransform.AngleX)";
    private const string Angle = "(UIElement.RenderTransform).(TransformGroup.Children)[2].(RotateTransform.Angle)";
    private const string MoveX = "(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.X)";
    private const string MoveY = "(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.Y)";
    private const string Opacity = "(UIElement.Opacity)";

    private readonly ScrollViewer _viewer;
    private readonly MosaicPanel _panel;
    private readonly Dictionary<MosaicTile, Storyboard> _running = new();
    private readonly HashSet<MosaicTile> _waiting = new();   // nuevas, ocultas hasta que les toque su tanda
    private readonly DispatcherTimer _waveTimer = new();
    private int _lastIntro = -1;

    public TileAnimator(ScrollViewer viewer, MosaicPanel panel)
    {
        _viewer = viewer;
        _panel = panel;
        _waveTimer.Tick += (_, _) => NextWave(from: null);
    }

    /// <summary>Un fotograma clave: instante (ms desde que empieza), valor y curva, o salto seco si Step.</summary>
    private readonly record struct K(double Ms, double Value, IEasingFunction? Ease = null, bool Step = false);

    /// <summary>Guarda dónde está cada tesela antes de recolocar el mosaico (null si no se está viendo).</summary>
    public Dictionary<MosaicTile, Rect>? CaptureSlots()
    {
        if (_viewer.Visibility != Visibility.Visible)
            return null;

        var slots = new Dictionary<MosaicTile, Rect>(_panel.Children.Count);
        foreach (MosaicTile tile in _panel.Children)
            slots[tile] = LayoutInformation.GetLayoutSlot(tile);
        return slots;
    }

    /// <summary>
    /// Con el mosaico ya recolocado: hace entrar las teselas nuevas y lleva las demás de su sitio
    /// antiguo al nuevo. El mosaico se forma desde <paramref name="active"/> (la canción que suena) hacia
    /// fuera; si <paramref name="activeLocked"/> (se está arrastrando la barra), esa portada no se anima.
    /// </summary>
    public void Animate(IReadOnlyList<MosaicTile> added, Dictionary<MosaicTile, Rect>? oldSlots,
        MosaicTile? active, bool activeLocked)
    {
        if (_viewer.Visibility != Visibility.Visible)
            return;
        var skip = activeLocked ? active : null;

        // Todas las nuevas se ocultan y esperan su tanda. Se hace antes de pintar, así no llegan a verse.
        foreach (var tile in added)
        {
            if (tile == skip || tile.Parent != _panel)
                continue;
            tile.Opacity = 0;
            _waiting.Add(tile);
        }

        // Si ya hay tandas en marcha, las nuevas se suman a la cola; si no, empieza la primera,
        // alrededor de la canción que suena (el mosaico se desplaza enseguida hasta ella).
        TimeSpan? impact = _waveTimer.IsEnabled ? null : NextWave(from: active);

        if (oldSlots is null)
            return;
        // Las que ya estaban se desplazan a su sitio nuevo; solo las que están en pantalla o cerca.
        double top = _viewer.VerticalOffset - _viewer.ViewportHeight;
        double bottom = _viewer.VerticalOffset + _viewer.ViewportHeight * 2;
        bool InView(Rect slot) => slot.Bottom > top && slot.Top < bottom;
        foreach (var (tile, old) in oldSlots)
        {
            if (tile == skip || tile.Parent != _panel || _waiting.Contains(tile))
                continue;
            var now = LayoutInformation.GetLayoutSlot(tile);
            if (old != now && !old.IsEmpty && (InView(old) || InView(now)))
                PlayMove(tile, old, now, impact);
        }
    }

    /// <summary>
    /// Hace entrar la siguiente tanda: las 10 que esperan más cerca de <paramref name="from"/> o, si es
    /// null, del centro de lo que se está viendo (así el mosaico se forma donde se mira).
    /// Devuelve el instante del primer choque, si alguna entra chocando.
    /// </summary>
    private TimeSpan? NextWave(MosaicTile? from)
    {
        _waveTimer.Stop();
        _waiting.RemoveWhere(tile => tile.Parent != _panel);   // quitadas de la lista entretanto
        if (_waiting.Count == 0)
            return null;

        if (_viewer.Visibility != Visibility.Visible)
        {
            // Se ha cambiado a la vista original: las que faltan se muestran sin animación.
            foreach (var tile in _waiting)
                tile.ClearValue(UIElement.OpacityProperty);
            _waiting.Clear();
            return null;
        }

        var anchor = from?.Parent == _panel
            ? Center(LayoutInformation.GetLayoutSlot(from))
            : new Point(_panel.ActualWidth / 2, _viewer.VerticalOffset + _viewer.ViewportHeight / 2);
        var wave = _waiting
            .Select(tile => (Tile: tile, Slot: LayoutInformation.GetLayoutSlot(tile)))
            .OrderBy(item => (Center(item.Slot) - anchor).LengthSquared)
            .Take(WaveSize)
            .ToList();

        // Los tiempos de las animaciones se escriben sin ralentizar: se divide entre Slowdown.
        var step = TimeSpan.FromMilliseconds(WaveStepMs / Slowdown);
        TimeSpan? impact = null;   // primer choque, para que las demás se aparten justo entonces
        for (int i = 0; i < wave.Count; i++)
        {
            _waiting.Remove(wave[i].Tile);
            var hit = PlayIntro(wave[i].Tile, wave[i].Slot, step * i, _viewer.VerticalOffset);
            if (hit < impact || (impact is null && hit is not null))
                impact = hit;
        }

        if (_waiting.Count > 0)
        {
            // La siguiente tanda, cuando la última de esta haya terminado de colocarse.
            _waveTimer.Interval = TimeSpan.FromMilliseconds((wave.Count - 1) * WaveStepMs + LongestIntroMs * Slowdown);
            _waveTimer.Start();
        }
        return impact;
    }

    private static Point Center(Rect slot) => new(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);

    /// <summary>Corta la animación de una tesela y la deja en su sitio.</summary>
    public void Finish(MosaicTile? tile)
    {
        if (tile is null || !_running.Remove(tile, out var storyboard))
            return;
        storyboard.Remove(tile);   // para la animación y suelta sus relojes
        tile.IsAnimating = false;  // ya en su sitio: carátula nítida
        tile.ClearValue(UIElement.RenderTransformProperty);
        tile.ClearValue(UIElement.RenderTransformOriginProperty);
        tile.ClearValue(Panel.ZIndexProperty);
    }

    /// <summary>Lanza una entrada al azar (sin repetir la anterior). Devuelve el instante del choque, si lo hay.</summary>
    private TimeSpan? PlayIntro(MosaicTile tile, Rect slot, TimeSpan delay, double viewTop)
    {
        int pick = _lastIntro < 0
            ? Random.Shared.Next(Intros.Length)
            : (_lastIntro + 1 + Random.Shared.Next(Intros.Length - 1)) % Intros.Length;
        _lastIntro = pick;

        var sb = Prepare(tile, new Point(0.5, 0.5));
        // Entra por el lado del panel más cercano, desde justo fuera de él.
        int dir = slot.Left + slot.Width / 2 < _panel.ActualWidth / 2 ? 1 : -1;
        double offstage = -dir * (dir > 0 ? slot.Right + 40 : _panel.ActualWidth - slot.Left + 40);
        TimeSpan? impact = null;

        switch (Intros[pick])
        {
            case Intro.Spin:
                // Entra girando mientras crece.
                int turn = Random.Shared.Next(2) == 0 ? -1 : 1;
                Add(sb, Angle, delay, turn * 540, new K(750, 0, Out(new CubicEase())));
                Add(sb, ScaleX, delay, 0.1, new K(750, 1, Out(new BackEase { Amplitude = 0.4 })));
                Add(sb, ScaleY, delay, 0.1, new K(750, 1, Out(new BackEase { Amplitude = 0.4 })));
                Add(sb, Opacity, delay, 0, new K(200, 1));
                break;

            case Intro.Grow:
                // Se hincha de más y rebota hasta su tamaño.
                foreach (var axis in new[] { ScaleX, ScaleY })
                    Add(sb, axis, delay, 0, new K(280, 1.3, Out(new CubicEase())), new K(400, 0.9), new K(500, 1.06), new K(600, 1));
                Add(sb, Opacity, delay, 0, new K(120, 1));
                break;

            case Intro.Crash:
                // Llega acelerando desde el borde, se aplasta contra las demás y rebota.
                Add(sb, MoveX, delay, offstage, new K(380, dir * 22, In(new QuadraticEase())),
                    new K(470, -dir * 12), new K(550, dir * 5), new K(620, 0));
                Add(sb, Angle, delay, -dir * 25, new K(380, dir * 8), new K(470, -dir * 4), new K(620, 0));
                Add(sb, ScaleX, delay, 1, new K(330, 1), new K(380, 0.82), new K(470, 1.06), new K(560, 1));
                Add(sb, ScaleY, delay, 1, new K(330, 1), new K(380, 1.12), new K(470, 0.96), new K(560, 1));
                impact = delay + TimeSpan.FromMilliseconds(380);
                break;

            case Intro.Drop:
                // Cae desde arriba de la pantalla y bota.
                Add(sb, MoveY, delay, -(slot.Bottom - viewTop + 30),
                    new K(900, 0, Out(new BounceEase { Bounces = 3, Bounciness = 2.2 })));
                Add(sb, Angle, delay, Random.Shared.Next(-12, 13), new K(900, 0, Out(new CubicEase())));
                break;

            case Intro.Skid:
                // Entra derrapando de lado y frena en seco inclinándose.
                Add(sb, MoveX, delay, offstage, new K(380, dir * 30, Out(new CubicEase())), new K(520, -dir * 8), new K(640, 0));
                Add(sb, SkewX, delay, -dir * 25, new K(380, dir * 18), new K(520, -dir * 6), new K(640, 0));
                Add(sb, Opacity, delay, 0, new K(150, 1));
                break;

            case Intro.Card:
                // Se da la vuelta como una carta.
                Add(sb, ScaleX, delay, 0, new K(320, 1.08, Out(new CubicEase())), new K(420, 1));
                Add(sb, ScaleY, delay, 0.8, new K(420, 1, Out(new CubicEase())));
                Add(sb, Opacity, delay, 0, new K(100, 1));
                break;

            case Intro.Flicker:
                // Parpadea como un fluorescente al encenderse.
                Add(sb, Opacity, delay, 0, new K(70, 1, Step: true), new K(130, 0, Step: true), new K(210, 1, Step: true),
                    new K(260, 0.15, Step: true), new K(330, 1, Step: true), new K(400, 0.4, Step: true), new K(450, 1, Step: true));
                Add(sb, ScaleX, delay, 0.94, new K(450, 1));
                Add(sb, ScaleY, delay, 0.94, new K(450, 1));
                break;
        }

        Start(tile, sb, zIndex: 1);   // por encima de las demás mientras entra
        return impact;
    }

    /// <summary>Lleva una tesela de su sitio (y tamaño) anterior al nuevo.</summary>
    private void PlayMove(MosaicTile tile, Rect old, Rect now, TimeSpan? impact)
    {
        var sb = Prepare(tile, new Point(0, 0));
        double dx = old.X - now.X;
        double dy = old.Y - now.Y;
        double sx = now.Width > 0 ? old.Width / now.Width : 1;
        double sy = now.Height > 0 ? old.Height / now.Height : 1;

        if (impact is { } hit)
        {
            // Si alguna entra chocando, las demás aguantan hasta el golpe y luego ceden a trompicones.
            Stumble(sb, MoveX, hit, dx, 0);
            Stumble(sb, MoveY, hit, dy, 0);
            Stumble(sb, ScaleX, hit, sx, 1);
            Stumble(sb, ScaleY, hit, sy, 1);
            int shake = Random.Shared.Next(2) == 0 ? -1 : 1;
            Add(sb, Angle, hit, 0, new K(60, shake * 4), new K(140, -shake * 3), new K(230, shake * 2), new K(320, 0));
        }
        else
        {
            var ease = Out(new CubicEase());
            Add(sb, MoveX, TimeSpan.Zero, dx, new K(450, 0, ease));
            Add(sb, MoveY, TimeSpan.Zero, dy, new K(450, 0, ease));
            Add(sb, ScaleX, TimeSpan.Zero, sx, new K(450, 1, ease));
            Add(sb, ScaleY, TimeSpan.Zero, sy, new K(450, 1, ease));
        }

        Start(tile, sb, zIndex: 0);
    }

    /// <summary>Avanza de golpe en golpe con parones, se pasa un poco y se asienta.</summary>
    private static void Stumble(Storyboard sb, string path, TimeSpan start, double from, double to)
    {
        double rest = from - to;
        Add(sb, path, start, from,
            new K(50, to + rest * 0.65), new K(140, to + rest * 0.65),
            new K(190, to + rest * 0.3), new K(280, to + rest * 0.3),
            new K(340, to - rest * 0.08), new K(420, to));
    }

    private Storyboard Prepare(MosaicTile tile, Point origin)
    {
        Finish(tile);
        tile.ClearValue(UIElement.OpacityProperty);   // la ocultaba mientras esperaba su tanda
        tile.IsAnimating = true;   // carátula desenfocada mientras se mueve
        tile.RenderTransformOrigin = origin;
        tile.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(), new SkewTransform(), new RotateTransform(), new TranslateTransform() },
        };
        // Stop: al acabar, todo vuelve a su valor base (escala 1, giro 0, opacidad 1), que es el final.
        return new Storyboard { FillBehavior = FillBehavior.Stop, SpeedRatio = 1 / Slowdown };
    }

    private void Start(MosaicTile tile, Storyboard sb, int zIndex)
    {
        if (zIndex != 0)
            Panel.SetZIndex(tile, zIndex);
        sb.Completed += (_, _) =>
        {
            if (_running.TryGetValue(tile, out var current) && current == sb)
                Finish(tile);
        };
        _running[tile] = sb;
        sb.Begin(tile, isControllable: true);
    }

    /// <summary>
    /// Añade una animación por fotogramas. Hasta <paramref name="delay"/> se mantiene en
    /// <paramref name="from"/>, para que la tesela no se vea antes de que le toque entrar.
    /// </summary>
    private static void Add(Storyboard sb, string path, TimeSpan delay, double from, params K[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (delay > TimeSpan.Zero)
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(delay)));

        foreach (var frame in frames)
        {
            var time = KeyTime.FromTimeSpan(delay + TimeSpan.FromMilliseconds(frame.Ms));
            animation.KeyFrames.Add(frame.Step
                ? new DiscreteDoubleKeyFrame(frame.Value, time)
                : new EasingDoubleKeyFrame(frame.Value, time, frame.Ease));
        }

        Storyboard.SetTargetProperty(animation, new PropertyPath(path));
        sb.Children.Add(animation);
    }

    private static IEasingFunction Out(EasingFunctionBase ease)
    {
        ease.EasingMode = EasingMode.EaseOut;
        return ease;
    }

    private static IEasingFunction In(EasingFunctionBase ease)
    {
        ease.EasingMode = EasingMode.EaseIn;
        return ease;
    }
}
