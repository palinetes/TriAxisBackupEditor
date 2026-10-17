using System.Windows;
using System.Windows.Media;

namespace TriAxisBackupEditor.App.Controls;

/// <summary>
/// Display de LED de 7 segmentos (como os do painel do TriAxis), desenhado em vetor.
/// O texto é alinhado à direita; '.' acende o ponto decimal do dígito anterior ("8.0" → 8. 0).
/// </summary>
public sealed class SevenSegmentDisplay : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SevenSegmentDisplay),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DigitsProperty = DependencyProperty.Register(
        nameof(Digits), typeof(int), typeof(SevenSegmentDisplay),
        new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OnBrushProperty = DependencyProperty.Register(
        nameof(OnBrush), typeof(Brush), typeof(SevenSegmentDisplay),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xFF, 0x2A, 0x1A)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffBrushProperty = DependencyProperty.Register(
        nameof(OffBrush), typeof(Brush), typeof(SevenSegmentDisplay),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0x2E, 0x0B, 0x08)), FrameworkPropertyMetadataOptions.AffectsRender));

    // Bits: a=1 b=2 c=4 d=8 e=16 f=32 g=64
    private static readonly Dictionary<char, byte> Glyphs = new()
    {
        ['0'] = 0x3F, ['1'] = 0x06, ['2'] = 0x5B, ['3'] = 0x4F, ['4'] = 0x66,
        ['5'] = 0x6D, ['6'] = 0x7D, ['7'] = 0x07, ['8'] = 0x7F, ['9'] = 0x6F,
        ['-'] = 0x40, [' '] = 0x00, ['?'] = 0x53, ['_'] = 0x08,
        ['A'] = 0x77, ['b'] = 0x7C, ['C'] = 0x39, ['d'] = 0x5E, ['E'] = 0x79, ['F'] = 0x71,
        ['H'] = 0x76, ['h'] = 0x74, ['L'] = 0x38, ['n'] = 0x54, ['o'] = 0x5C, ['P'] = 0x73,
        ['r'] = 0x50, ['S'] = 0x6D, ['t'] = 0x78, ['U'] = 0x3E, ['y'] = 0x6E
    };

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public int Digits
    {
        get => (int)GetValue(DigitsProperty);
        set => SetValue(DigitsProperty, value);
    }

    public Brush OnBrush
    {
        get => (Brush)GetValue(OnBrushProperty);
        set => SetValue(OnBrushProperty, value);
    }

    public Brush OffBrush
    {
        get => (Brush)GetValue(OffBrushProperty);
        set => SetValue(OffBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsInfinity(availableSize.Height) ? 44 : Math.Min(availableSize.Height, 44);
        if (!double.IsNaN(Height))
        {
            height = Height;
        }

        var (cellWidth, pitch) = Metrics(height);
        return new Size((pitch * (Math.Max(1, Digits) - 1)) + cellWidth + (cellWidth * 0.35), height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var height = ActualHeight;
        if (height <= 0)
        {
            return;
        }

        var (cellWidth, pitch) = Metrics(height);
        var thickness = cellWidth * 0.2;
        var cells = Layout(Text ?? string.Empty, Math.Max(1, Digits));

        for (var i = 0; i < cells.Count; i++)
        {
            var (mask, dot) = cells[i];
            var x = i * pitch;
            DrawCell(dc, x, cellWidth, height, thickness, mask, dot);
        }
    }

    private static (double CellWidth, double Pitch) Metrics(double height)
    {
        var cellWidth = height * 0.52;
        return (cellWidth, cellWidth + (height * 0.24));
    }

    private static List<(byte Mask, bool Dot)> Layout(string text, int digits)
    {
        var cells = new List<(byte Mask, bool Dot)>();
        foreach (var ch in text)
        {
            if (ch == '.' || ch == ',')
            {
                if (cells.Count == 0)
                {
                    cells.Add((0, true));
                }
                else
                {
                    cells[^1] = (cells[^1].Mask, true);
                }

                continue;
            }

            cells.Add((Glyphs.TryGetValue(ch, out var mask) ? mask : Glyphs['?'], false));
        }

        if (cells.Count > digits)
        {
            cells = cells.GetRange(cells.Count - digits, digits);
        }

        while (cells.Count < digits)
        {
            cells.Insert(0, (0, false));
        }

        return cells;
    }

    private void DrawCell(DrawingContext dc, double x, double w, double h, double t, byte mask, bool dot)
    {
        var gap = t * 0.15;
        var x0 = x + (t / 2);
        var x1 = x + w - (t / 2);
        var y0 = t / 2;
        var ym = h / 2;
        var y1 = h - (t / 2);

        var segments = new[]
        {
            Horizontal(x0 + gap, y0, x1 - x0 - (2 * gap), t), // a
            Vertical(x1, y0 + gap, ym - y0 - (2 * gap), t),     // b
            Vertical(x1, ym + gap, y1 - ym - (2 * gap), t),     // c
            Horizontal(x0 + gap, y1, x1 - x0 - (2 * gap), t), // d
            Vertical(x0, ym + gap, y1 - ym - (2 * gap), t),     // e
            Vertical(x0, y0 + gap, ym - y0 - (2 * gap), t),     // f
            Horizontal(x0 + gap, ym, x1 - x0 - (2 * gap), t)  // g
        };

        for (var s = 0; s < segments.Length; s++)
        {
            var lit = (mask & (1 << s)) != 0;
            dc.DrawGeometry(lit ? OnBrush : OffBrush, null, segments[s]);
        }

        var radius = t * 0.5;
        dc.DrawEllipse(dot ? OnBrush : OffBrush, null, new Point(x + w + (t * 0.7), y1), radius, radius);
    }

    private static Geometry Horizontal(double x, double y, double length, double t)
    {
        var half = t / 2;
        return Polygon(
            new Point(x, y),
            new Point(x + half, y - half),
            new Point(x + length - half, y - half),
            new Point(x + length, y),
            new Point(x + length - half, y + half),
            new Point(x + half, y + half));
    }

    private static Geometry Vertical(double x, double y, double length, double t)
    {
        var half = t / 2;
        return Polygon(
            new Point(x, y),
            new Point(x + half, y + half),
            new Point(x + half, y + length - half),
            new Point(x, y + length),
            new Point(x - half, y + length - half),
            new Point(x - half, y + half));
    }

    private static Geometry Polygon(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: true, isClosed: true);
            ctx.PolyLineTo(points[1..], isStroked: false, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }
}
