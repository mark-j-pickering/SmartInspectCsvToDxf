using SmartInspectCsvToDxf.Models;
using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.UI;

public sealed class PreviewPanel : Panel
{
    private const string BackgroundResourceName = "SmartInspectCsvToDxf.Resources.BUM_logo_background.png";
    private static readonly Image EmbeddedBackgroundImage = LoadEmbeddedBackgroundImage();
    private static readonly DrawingPlane[] Planes = Enum.GetValues<DrawingPlane>();

    private List<Feature> _features = [];
    private object? _featuresSource;
    private bool _mirrorAboutYAxis;
    private bool _showText = true;
    private DrawingPlane _drawingPlane = DrawingPlane.XY;
    private bool _planeOverridden;

    public DrawingPlane DrawingPlane => _drawingPlane;
    public bool IsPlaneOverridden => _planeOverridden;

    public PreviewPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
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
        // Only re-detect/reset the drawing plane when this is a genuinely new feature
        // set (a different report was loaded) - not on every call, since toggling the
        // mirror/show-text checkboxes re-invokes this with the same underlying list and
        // should leave a manual plane override in place.
        if (!ReferenceEquals(features, _featuresSource))
        {
            _featuresSource = features;
            _features = features.ToList();
            _drawingPlane = DrawingPlaneDetector.Detect(_features);
            _planeOverridden = false;
        }

        _mirrorAboutYAxis = mirrorAboutYAxis;
        _showText = showText;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData switch
        {
            Keys.Up or Keys.Down or Keys.Left or Keys.Right => true,
            _ => base.IsInputKey(keyData)
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.KeyCode)
        {
            case Keys.Up:
            case Keys.Right:
                CyclePlane(1);
                e.Handled = true;
                break;
            case Keys.Down:
            case Keys.Left:
                CyclePlane(-1);
                e.Handled = true;
                break;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    private void CyclePlane(int direction)
    {
        if (_features.Count == 0)
            return;

        var currentIndex = Array.IndexOf(Planes, _drawingPlane);
        var nextIndex = ((currentIndex + direction) % Planes.Length + Planes.Length) % Planes.Length;
        _drawingPlane = Planes[nextIndex];
        _planeOverridden = true;
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
        var projected = features
            .Select(f =>
            {
                var (u, v, _) = DrawingPlaneMapper.Project(f, _drawingPlane);
                return (Feature: f, U: u, V: v);
            })
            .ToList();

        var minX = projected.Min(p => p.U - p.Feature.Radius);
        var maxX = projected.Max(p => p.U + p.Feature.Radius);
        var minY = projected.Min(p => p.V - p.Feature.Radius);
        var maxY = projected.Max(p => p.V + p.Feature.Radius);

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

        var (uLabel, vLabel) = DrawingPlaneMapper.AxisLabels(_drawingPlane);
        DrawAxes(g, ToScreen, axisPen, uLabel, vLabel);

        foreach (var p in projected)
        {
            var centre = ToScreen(p.U, p.V);
            var r = ToScreenLength(p.Feature.Radius);
            g.DrawEllipse(circlePen, centre.X - r, centre.Y - r, r * 2, r * 2);
            g.FillEllipse(pointBrush, centre.X - 2.2f, centre.Y - 2.2f, 4.4f, 4.4f);

            if (_showText)
            {
                var offset = Math.Max(r * 0.08f, 4f);
                g.DrawString(p.Feature.Name, Font, textBrush, centre.X + offset, centre.Y - offset - Font.Height);
            }
        }

        using var footerBrush = new SolidBrush(Color.DimGray);
        var planeSource = _planeOverridden ? "manual" : "auto";
        var footer = $"{features.Count} features | Plane {_drawingPlane} ({planeSource}, ↑↓←→ to change) | {uLabel} {minX:0.###} to {maxX:0.###} | {vLabel} {minY:0.###} to {maxY:0.###}";
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

    private void DrawAxes(Graphics g, Func<double, double, PointF> toScreen, Pen axisPen, string uLabel, string vLabel)
    {
        var x0a = toScreen(-1_000_000, 0);
        var x0b = toScreen(1_000_000, 0);
        var y0a = toScreen(0, -1_000_000);
        var y0b = toScreen(0, 1_000_000);

        g.DrawLine(axisPen, x0a, x0b);
        g.DrawLine(axisPen, y0a, y0b);

        using var brush = new SolidBrush(Color.Gray);
        g.DrawString(uLabel, Font, brush, Width - 24, y0a.Y + 4);
        g.DrawString(vLabel, Font, brush, y0a.X + 4, 8);
    }
}
