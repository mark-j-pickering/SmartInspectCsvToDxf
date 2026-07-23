using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.UI;

public sealed class PreviewPanel : Panel
{
    private const string BackgroundResourceName = "SmartInspectCsvToDxf.Resources.BUM_logo_background.png";
    private static readonly Image EmbeddedBackgroundImage = LoadEmbeddedBackgroundImage();

    private List<Feature> _features = [];
    private bool _mirrorAboutYAxis;
    private bool _showText = true;

    public PreviewPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        ResizeRedraw = true;
    }

    private static Image LoadEmbeddedBackgroundImage()
    {
        var assembly = typeof(PreviewPanel).Assembly;
        using var stream = assembly.GetManifestResourceStream(BackgroundResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{BackgroundResourceName}' not found.");
        return Image.FromStream(stream);
    }

    public void SetFeatures(IEnumerable<Feature> features, bool mirrorAboutYAxis, bool showText)
    {
        _features = features.ToList();
        _mirrorAboutYAxis = mirrorAboutYAxis;
        _showText = showText;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        DrawEmbeddedBackgroundImage(g);

        using var borderPen = new Pen(Color.Gainsboro, 1);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        if (_features.Count == 0)
        {
            using var brush = new SolidBrush(Color.DimGray);
            g.DrawString("Select a report file to preview", Font, brush, new PointF(16, 16));
            return;
        }

        var features = _mirrorAboutYAxis ? _features.Select(f => f.WithMirrorY()).ToList() : _features;

        var minX = features.Min(f => f.X - f.Radius);
        var maxX = features.Max(f => f.X + f.Radius);
        var minY = features.Min(f => f.Y - f.Radius);
        var maxY = features.Max(f => f.Y + f.Radius);

        var rangeX = Math.Max(maxX - minX, 1.0);
        var rangeY = Math.Max(maxY - minY, 1.0);
        var centreX = (minX + maxX) / 2.0;
        var centreY = (minY + maxY) / 2.0;
        var span = Math.Max(rangeX, rangeY) * 1.2; // extents + 20%

        var padding = 40f;
        var drawWidth = Math.Max(Width - padding * 2, 1);
        var drawHeight = Math.Max(Height - padding * 2, 1);
        var scale = Math.Min(drawWidth, drawHeight) / span;

        PointF ToScreen(double x, double y)
        {
            var sx = (float)(Width / 2.0 + (x - centreX) * scale);
            var sy = (float)(Height / 2.0 - (y - centreY) * scale);
            return new PointF(sx, sy);
        }

        float ToScreenLength(double length) => (float)(length * scale);

        using var gridPen = new Pen(Color.FromArgb(235, 235, 235), 1);
        using var axisPen = new Pen(Color.FromArgb(120, 120, 120), 1);
        using var circlePen = new Pen(Color.FromArgb(25, 90, 160), 1.4f);
        using var pointBrush = new SolidBrush(Color.FromArgb(20, 20, 20));
        using var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40));

        DrawAxes(g, ToScreen, axisPen);

        foreach (var f in features)
        {
            var centre = ToScreen(f.X, f.Y);
            var r = ToScreenLength(f.Radius);
            g.DrawEllipse(circlePen, centre.X - r, centre.Y - r, r * 2, r * 2);
            g.FillEllipse(pointBrush, centre.X - 2.2f, centre.Y - 2.2f, 4.4f, 4.4f);

            if (_showText)
            {
                var offset = Math.Max(r * 0.08f, 4f);
                g.DrawString(f.Name, Font, textBrush, centre.X + offset, centre.Y - offset - Font.Height);
            }
        }

        using var footerBrush = new SolidBrush(Color.DimGray);
        var footer = $"{features.Count} features | X {minX:0.###} to {maxX:0.###} | Y {minY:0.###} to {maxY:0.###}";
        if (_mirrorAboutYAxis)
            footer += " | mirrored about Y axis";
        g.DrawString(footer, Font, footerBrush, new PointF(8, Height - Font.Height - 8));
    }

    private void DrawEmbeddedBackgroundImage(Graphics g)
    {
        const float sizeBoost = 1.1f;
        var scale = Math.Min((float)Width / EmbeddedBackgroundImage.Width, (float)Height / EmbeddedBackgroundImage.Height) * sizeBoost;
        var drawWidth = EmbeddedBackgroundImage.Width * scale;
        var drawHeight = EmbeddedBackgroundImage.Height * scale;
        var x = (Width - drawWidth) / 2f;
        var y = (Height - drawHeight) / 2f;

        g.DrawImage(EmbeddedBackgroundImage, x, y, drawWidth, drawHeight);
    }

    private void DrawAxes(Graphics g, Func<double, double, PointF> toScreen, Pen axisPen)
    {
        var x0a = toScreen(-1_000_000, 0);
        var x0b = toScreen(1_000_000, 0);
        var y0a = toScreen(0, -1_000_000);
        var y0b = toScreen(0, 1_000_000);

        g.DrawLine(axisPen, x0a, x0b);
        g.DrawLine(axisPen, y0a, y0b);

        using var brush = new SolidBrush(Color.Gray);
        g.DrawString("X", Font, brush, Width - 24, y0a.Y + 4);
        g.DrawString("Y", Font, brush, y0a.X + 4, 8);
    }
}
