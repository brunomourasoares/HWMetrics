using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
// O projeto usa WinForms (NotifyIcon), então System.Drawing / System.Windows.Forms entram nos
// global usings e geram ambiguidade. Os aliases abaixo fazem os nomes apontarem para o WPF.
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;

namespace RMMonitor.Controls;

/// <summary>
/// Descrição visual do bloco selecionado. É montada em MainWindow a partir do CoolerVisualProfile.
/// Family: auraice | aurafrostpro | classic | square | allblack.
/// </summary>
public sealed record CoolerBlockStyle(
    string Family,
    bool IsBlack,
    bool HasNumericDisplay,
    bool Lit,
    bool Animated,
    double RingSeconds,
    IReadOnlyList<Color> Palette);

/// <summary>
/// Preview do bloco da bomba do water cooler, em vista 3/4 como nas fotos de produto.
/// Apenas visual: não controla hardware.
/// </summary>
public partial class CoolerBlockView : UserControl
{
    // Logo oficial da marca (o mesmo do display do Aura Ice), usado em camadas no túnel.
    private static readonly ImageBrush LogoMask = CreateLogoMask();

    // true: o PNG é usado só pelo canal alpha e pintado com a paleta (precisa ser PNG com transparência).
    // false: usa o PNG como está, em camadas, sem pintar (use se o logo for opaco).
    private static readonly bool TintLogoWithAlpha = true;

    private static readonly CoolerBlockStyle DefaultStyle = new(
        "auraice", false, true, true, true, 7.5,
        [
            Color.FromRgb(255, 42, 104),
            Color.FromRgb(255, 194, 54),
            Color.FromRgb(68, 255, 190),
            Color.FromRgb(45, 150, 255),
            Color.FromRgb(190, 74, 255)
        ]);

    // Gradiente do LED do cubo em coordenadas absolutas da cena; gira em torno do centro do topo.
    private readonly RotateTransform _cubeRotation = new(0, 150, 138.5);
    private readonly List<RotateTransform> _tunnelRotations = [];

    public CoolerBlockView()
    {
        InitializeComponent();

        Apply(DefaultStyle);
    }

    /// <summary>Texto do display numérico do Aura Ice ("56", "--").</summary>
    public void SetTemperature(string text) => DisplayTemp.Text = text;

    public void Apply(CoolerBlockStyle style)
    {
        var family = style.Family;
        var isCube = family == "square";
        var isIce = family == "auraice";
        var isTunnel = family is "classic" or "aurafrostpro";
        var isAllBlack = family == "allblack";
        IReadOnlyList<Color> palette = style.Palette ?? Array.Empty<Color>();
        var seconds = style.RingSeconds > 0 ? style.RingSeconds : 8;
        var spin = style.Lit && style.Animated;

        // Animações da família anterior não devem continuar rodando escondidas.
        StopSpin(PrismRotation);
        StopSpin(_cubeRotation);
        foreach (var rotation in _tunnelRotations) StopSpin(rotation);

        CylinderScene.Visibility = isCube ? Visibility.Collapsed : Visibility.Visible;
        CubeScene.Visibility = isCube ? Visibility.Visible : Visibility.Collapsed;
        AuraIceLayer.Visibility = isIce ? Visibility.Visible : Visibility.Collapsed;
        AuraIceDisplay.Visibility = isIce && style.HasNumericDisplay ? Visibility.Visible : Visibility.Collapsed;
        TunnelHost.Visibility = isTunnel ? Visibility.Visible : Visibility.Collapsed;
        AllBlackLayer.Visibility = isAllBlack ? Visibility.Visible : Visibility.Collapsed;
        GlassGlare.Visibility = isAllBlack ? Visibility.Collapsed : Visibility.Visible;

        ApplyFinish(style.IsBlack);

        if (isIce)
        {
            PrismRing.Stroke = CreateConicBrush(palette);
            PrismRing.Opacity = style.Lit ? 1 : 0;
            if (spin) Spin(PrismRotation, seconds);
        }
        else if (isTunnel)
        {
            BuildTunnel(palette, spin, seconds);
        }
        else if (isCube)
        {
            ApplyCubeLighting(palette, style.Lit, spin, seconds);
        }
    }

    // ------------------------------------------------------------------ acabamento

    private void ApplyFinish(bool black)
    {
        var edge = Hex(black ? "3E4650" : "B9C3CD");

        // Cilindro
        CylRim.Fill = Hex(black ? "1D2127" : "F4F6F9");
        CylRim.Stroke = edge;
        CylBody.Fill = HorizontalGradient(black
            ? new[] { "3C434C", "1B1F24", "0B0D10" }
            : new[] { "FFFFFF", "E8ECF0", "BCC5CE" });
        CylBody.Stroke = edge;

        // Cubo
        var cubeEdge = Hex(black ? "3A424C" : "B3BDC7");
        CubeTopFace.Fill = Hex(black ? "21262C" : "FAFBFD");
        CubeLeftFace.Fill = Hex(black ? "15181C" : "D8DEE5");
        CubeRightFace.Fill = Hex(black ? "101316" : "EDF0F4");
        CubeTopFace.Stroke = cubeEdge;
        CubeLeftFace.Stroke = cubeEdge;
        CubeRightFace.Stroke = cubeEdge;

        for (var i = 0; i < CubeRelief.Children.Count; i++)
        {
            if (CubeRelief.Children[i] is not Polygon facet) continue;
            var strong = i % 2 == 0;
            facet.Fill = black
                ? Hex("FFFFFF", strong ? (byte)0x10 : (byte)0x08)
                : Hex("000000", strong ? (byte)0x12 : (byte)0x08);
        }
    }

    // ------------------------------------------------------------------ cubo

    private void ApplyCubeLighting(IReadOnlyList<Color> palette, bool lit, bool spin, double seconds)
    {
        var visibility = lit ? Visibility.Visible : Visibility.Collapsed;
        CubeRing.Visibility = visibility;
        CubeRingGlow.Visibility = visibility;
        CubeStrip.Visibility = visibility;
        CubeStripGlow.Visibility = visibility;
        if (!lit) return;

        var average = Average(palette);

        // Um único brush (não congelado) em coordenadas absolutas alimenta o anel E a faixa lateral,
        // então os dois sempre mostram a mesma cor no mesmo ponto, mesmo enquanto giram.
        var led = CreateLinearPaletteBrush(palette, new Point(60, 138.5), new Point(240, 138.5), freeze: false);
        led.MappingMode = BrushMappingMode.Absolute;
        led.Transform = _cubeRotation;
        CubeRing.Stroke = led;
        CubeStrip.Stroke = led;
        var glow = new SolidColorBrush(Color.FromArgb(0xB0, average.R, average.G, average.B));
        CubeRingGlow.Stroke = glow;
        CubeStripGlow.Stroke = glow;

        if (spin) Spin(_cubeRotation, seconds);
    }

    // ------------------------------------------------------------------ túnel (espelho infinito)

    private void BuildTunnel(IReadOnlyList<Color> palette, bool spin, double seconds)
    {
        TunnelHost.Children.Clear();
        _tunnelRotations.Clear();

        var average = Average(palette);
        var conic = CreateConicBrush(palette);

        // Vidro levemente tingido pela cor média, mais forte perto da borda.
        var tint = new RadialGradientBrush();
        tint.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, average.R, average.G, average.B), 0.0));
        tint.GradientStops.Add(new GradientStop(Color.FromArgb(0x30, average.R, average.G, average.B), 0.78));
        tint.GradientStops.Add(new GradientStop(Color.FromArgb(0x70, average.R, average.G, average.B), 1.0));
        tint.Freeze();
        var glass = new Ellipse { Width = 172, Height = 172, Fill = tint };
        Canvas.SetLeft(glass, 14);
        Canvas.SetTop(glass, 14);
        TunnelHost.Children.Add(glass);

        // Anéis cada vez menores, mais finos, mais escuros e deslocados para baixo (profundidade).
        // Cada anel tem seu próprio relógio, com defasagem, para dar o efeito de espiral das fotos.
        const int ringCount = 9;
        for (var i = 0; i < ringCount; i++)
        {
            var scale = 1 - 0.098 * i;
            var diameter = 172 * scale;
            var rotation = new RotateTransform(i * 12);
            _tunnelRotations.Add(rotation);

            var ring = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Stroke = conic,
                StrokeThickness = Math.Max(1.6, 8.5 * Math.Pow(scale, 1.1)),
                Opacity = Math.Max(0.2, 1 - 0.095 * i),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = rotation
            };
            Canvas.SetLeft(ring, 100 - diameter / 2);
            Canvas.SetTop(ring, 100 - diameter / 2 + i * 3.6);
            TunnelHost.Children.Add(ring);

            if (spin) Spin(rotation, seconds, i * 12);
        }

        // Logo no fundo do túnel: cópias empilhadas para baixo, cada uma menor e mais transparente
        // (as "camadas" das fotos), e a camada principal por cima com o gradiente da paleta.
        var logoBrush = CreateLinearPaletteBrush(palette, new Point(0.2, 0), new Point(0.8, 1), freeze: true);
        const double cx = 100, cy = 112, logoSize = 96;
        for (var echo = 6; echo >= 1; echo--)
        {
            TunnelHost.Children.Add(MakeLogoLayer(
                cx, cy + echo * 2.8, logoSize * (1 - echo * 0.045), logoBrush, 0.55 - echo * 0.07));
        }

        TunnelHost.Children.Add(MakeLogoLayer(cx, cy, logoSize, logoBrush, 1));
    }

    private static FrameworkElement MakeLogoLayer(
        double centerX, double centerY, double size, Brush tint, double opacity)
    {
        FrameworkElement layer = TintLogoWithAlpha
            ? new System.Windows.Shapes.Rectangle { Fill = tint, OpacityMask = LogoMask }
            : new System.Windows.Controls.Image { Source = LogoMask.ImageSource, Stretch = Stretch.Uniform };
        layer.Width = size;
        layer.Height = size;
        layer.Opacity = opacity;
        Canvas.SetLeft(layer, centerX - size / 2);
        Canvas.SetTop(layer, centerY - size / 2);
        return layer;
    }

    private static ImageBrush CreateLogoMask()
    {
        var source = new BitmapImage(new Uri("pack://application:,,,/Resources/Images/rise-mode.png", UriKind.Absolute));
        source.Freeze();
        var brush = new ImageBrush(source) { Stretch = Stretch.Uniform };
        brush.Freeze();
        return brush;
    }

    // ------------------------------------------------------------------ animação

    // Ângulo positivo no WPF é horário; de "from" até "from - 360" o giro é anti-horário.
    private static void Spin(RotateTransform transform, double seconds, double from = 0)
    {
        transform.BeginAnimation(
            RotateTransform.AngleProperty,
            new DoubleAnimation(from, from - 360, TimeSpan.FromSeconds(seconds))
            {
                RepeatBehavior = RepeatBehavior.Forever
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void StopSpin(RotateTransform transform)
    {
        transform.BeginAnimation(RotateTransform.AngleProperty, null);
        transform.Angle = 0;
    }

    // ------------------------------------------------------------------ cores e brushes

    private static SolidColorBrush Hex(string rrggbb, byte alpha = 255)
    {
        var value = Convert.ToUInt32(rrggbb, 16);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, (byte)(value >> 16), (byte)(value >> 8), (byte)value));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush HorizontalGradient(string[] colors)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        for (var i = 0; i < colors.Length; i++)
            brush.GradientStops.Add(new GradientStop(Hex(colors[i]).Color, (double)i / (colors.Length - 1)));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush CreateLinearPaletteBrush(
        IReadOnlyList<Color> palette, Point start, Point end, bool freeze)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        if (palette.Count == 0)
        {
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(45, 55, 65), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(45, 55, 65), 1));
        }
        else if (palette.Count == 1)
        {
            brush.GradientStops.Add(new GradientStop(palette[0], 0));
            brush.GradientStops.Add(new GradientStop(palette[0], 1));
        }
        else
        {
            for (var i = 0; i < palette.Count; i++)
                brush.GradientStops.Add(new GradientStop(palette[i], (double)i / (palette.Count - 1)));
        }

        if (freeze) brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Gradiente cônico (WPF não tem) gerado uma vez como bitmap e usado como Stroke de Ellipse.
    /// Como o brush acompanha a forma, girar o Ellipse faz as cores "correrem" pelo anel, como em ARGB real.
    /// </summary>
    private static ImageBrush CreateConicBrush(IReadOnlyList<Color> palette, int size = 192)
    {
        var pixels = new byte[size * size * 4];
        var center = (size - 1) / 2d;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var t = (Math.Atan2(y - center, x - center) + Math.PI) / (2 * Math.PI);
                var color = SamplePalette(palette, t);
                var i = (y * size + x) * 4;
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bitmap.Freeze();
        var brush = new ImageBrush(bitmap) { Stretch = Stretch.Fill };
        brush.Freeze();
        return brush;
    }

    /// <summary>Amostra a paleta de forma circular (a última cor se mistura com a primeira).</summary>
    private static Color SamplePalette(IReadOnlyList<Color> palette, double t)
    {
        var count = palette.Count;
        if (count == 0) return Color.FromRgb(45, 55, 65);
        if (count == 1) return palette[0];

        var scaled = (t - Math.Floor(t)) * count;
        var from = (int)scaled % count;
        var to = (from + 1) % count;
        var f = scaled - Math.Floor(scaled);
        var a = palette[from];
        var b = palette[to];
        return Color.FromRgb(Lerp(a.R, b.R, f), Lerp(a.G, b.G, f), Lerp(a.B, b.B, f));
    }

    private static byte Lerp(byte a, byte b, double f) => (byte)Math.Round(a + (b - a) * f);

    private static Color Average(IReadOnlyList<Color> palette)
    {
        if (palette.Count == 0) return Color.FromRgb(45, 55, 65);
        int r = 0, g = 0, b = 0;
        foreach (var color in palette)
        {
            r += color.R;
            g += color.G;
            b += color.B;
        }

        return Color.FromRgb((byte)(r / palette.Count), (byte)(g / palette.Count), (byte)(b / palette.Count));
    }
}
