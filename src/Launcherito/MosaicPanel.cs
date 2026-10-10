using System.Windows;
using System.Windows.Controls;

namespace Launcherito;

/// <summary>
/// Panel en mosaico: reparte a sus hijos en una cuadrícula de celdas cuadradas. Cada hijo ocupa
/// Span x Span celdas, y los huecos que dejan las piezas grandes se rellenan con las pequeñas
/// siguientes (como "grid-auto-flow: dense" en CSS). Se colocan por su Order, no por su posición en
/// Children, así se pueden reordenar sin sacarlas del panel (un vídeo dentro se recargaría).
/// </summary>
public sealed class MosaicPanel : Panel
{
    public static readonly DependencyProperty SpanProperty = DependencyProperty.RegisterAttached(
        "Span", typeof(int), typeof(MosaicPanel),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public static readonly DependencyProperty OrderProperty = DependencyProperty.RegisterAttached(
        "Order", typeof(int), typeof(MosaicPanel),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public static int GetSpan(UIElement element) => (int)element.GetValue(SpanProperty);
    public static void SetSpan(UIElement element, int value) => element.SetValue(SpanProperty, value);

    public static int GetOrder(UIElement element) => (int)element.GetValue(OrderProperty);
    public static void SetOrder(UIElement element, int value) => element.SetValue(OrderProperty, value);

    /// <summary>Tamaño aproximado de una celda; el real se ajusta para llenar todo el ancho.</summary>
    public double TargetCellSize { get; set; } = 190;

    public double Gap { get; set; } = 10;

    /// <summary>Columnas y tamaño real de celda de la última vez que se colocó el mosaico.</summary>
    public int Columns { get; private set; } = 2;
    public double CellSize { get; private set; } = 190;

    private Rect[] _slots = [];

    /// <summary>Cuántas celdas de lado ocupa una pieza que mide <paramref name="size"/> (como mínimo 1, como mucho todas las columnas).</summary>
    public int SpanForSize(double size) => Math.Clamp((int)Math.Round((size + Gap) / (CellSize + Gap)), 1, Columns);

    /// <summary>Hueco que ocuparía una pieza de <paramref name="span"/> celdas puesta donde empieza <paramref name="slot"/>.</summary>
    public Rect AreaFrom(Rect slot, int span)
    {
        double step = CellSize + Gap;
        int column = Math.Min((int)Math.Round(slot.X / step), Columns - span);
        double size = span * CellSize + (span - 1) * Gap;
        return new Rect(Math.Max(0, column) * step, Math.Round(slot.Y / step) * step, size, size);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? TargetCellSize * 4 : availableSize.Width;
        int columns = Math.Max(2, (int)((width + Gap) / (TargetCellSize + Gap)));
        double cell = Math.Max(1, (width - Gap * (columns - 1)) / columns);
        Columns = columns;
        CellSize = cell;

        var occupied = new List<bool[]>();
        int firstFreeRow = 0;
        int rows = 0;
        int count = InternalChildren.Count;
        if (_slots.Length != count)
            _slots = new Rect[count];

        // Por Order; a igual Order, por su posición en Children.
        var order = new int[count];
        for (int i = 0; i < count; i++)
            order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            int byOrder = GetOrder(InternalChildren[a]).CompareTo(GetOrder(InternalChildren[b]));
            return byOrder != 0 ? byOrder : a.CompareTo(b);
        });

        foreach (int index in order)
        {
            var child = InternalChildren[index];
            int span = Math.Clamp(GetSpan(child), 1, columns);
            var (row, column) = FindSpot(occupied, ref firstFreeRow, columns, span);

            for (int r = row; r < row + span; r++)
                for (int c = column; c < column + span; c++)
                    occupied[r][c] = true;
            rows = Math.Max(rows, row + span);

            double size = span * cell + (span - 1) * Gap;
            var slot = new Rect(column * (cell + Gap), row * (cell + Gap), size, size);
            _slots[index] = slot;
            child.Measure(slot.Size);
        }

        double height = rows == 0 ? 0 : rows * cell + (rows - 1) * Gap;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (int i = 0; i < InternalChildren.Count && i < _slots.Length; i++)
            InternalChildren[i].Arrange(_slots[i]);
        return finalSize;
    }

    /// <summary>Busca la primera posición (por filas) donde cabe una pieza de span x span.</summary>
    private static (int Row, int Column) FindSpot(List<bool[]> occupied, ref int firstFreeRow, int columns, int span)
    {
        // Las filas ya completas no se vuelven a revisar.
        while (firstFreeRow < occupied.Count && Array.TrueForAll(occupied[firstFreeRow], cell => cell))
            firstFreeRow++;

        for (int row = firstFreeRow; ; row++)
        {
            while (occupied.Count < row + span)
                occupied.Add(new bool[columns]);

            for (int column = 0; column + span <= columns; column++)
            {
                if (Fits(occupied, row, column, span))
                    return (row, column);
            }
        }
    }

    private static bool Fits(List<bool[]> occupied, int row, int column, int span)
    {
        for (int r = row; r < row + span; r++)
            for (int c = column; c < column + span; c++)
                if (occupied[r][c])
                    return false;
        return true;
    }
}
