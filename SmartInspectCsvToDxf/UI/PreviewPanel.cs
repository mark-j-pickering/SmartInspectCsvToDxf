using SmartInspectCsvToDxf.Models;
using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.UI;

public sealed class PreviewPanel : Panel
{
    private const string BackgroundResourceName = "SmartInspectCsvToDxf.Resources.BUM_logo_background.png";
    private static readonly Image EmbeddedBackgroundImage = LoadEmbeddedBackgroundImage();
    private static readonly DrawingPlane[] Planes = Enum.GetValues<DrawingPlane>();

    private const float HitToleranceScreenPixels = 6f;

    // The features drawn/hit-tested here are always already in their final, current state -
    // MainForm mutates its own live feature list directly on every rotate/mirror/align click
    // and hands the result straight through; this panel has no notion of "orientation" to
    // apply, it just draws whatever it's given.
    private List<Feature> _features = [];
    private object? _featuresSource;
    private string _orientationDescription = string.Empty;
    private bool _showText = true;
    private DrawingPlane _drawingPlane = DrawingPlane.XY;
    private bool _planeOverridden;
    private bool _alignModeActive;
    // Screen-space endpoints of every line feature, cached from the last OnPaint, keyed by
    // index into _features rather than by Feature reference, since MainForm hands in a new
    // list instance on every rotate/mirror/align click and reference equality would never
    // match hover state across frames.
    private List<(int Index, PointF Start, PointF End)> _screenLines = [];
    private int? _hoveredLineIndex;

    public DrawingPlane DrawingPlane => _drawingPlane;
    public bool IsPlaneOverridden => _planeOverridden;

    // MainForm sets this from the Align toggle button.
    public bool AlignModeActive
    {
        get => _alignModeActive;
        set
        {
            if (_alignModeActive == value)
                return;

            _alignModeActive = value;
            if (!value)
            {
                _hoveredLineIndex = null;
                Cursor = Cursors.Default;
            }

            Invalidate();
        }
    }

    // Fired once, on a successful line pick, with the rotation angle/plane needed to level it.
    public event Action<double, DrawingPlane>? LineAligned;

    // Fired whenever align mode ends: a successful pick (after LineAligned), an Escape
    // cancel, or a new report being loaded while align mode was still active - lets
    // MainForm keep its toggle button's Checked state in sync.
    public event Action? AlignModeExited;

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

    // featuresIdentity: a token that stays the same reference across rotate/mirror/align
    // clicks for the same file (MainForm passes its stable, never-reassigned raw feature
    // list), but changes when a genuinely different report is loaded. features itself
    // (what's actually drawn) is a *different* list instance on every click, since it's the
    // live, already-transformed result - so it can't be used for this "same file?" check.
    public void SetFeatures(IEnumerable<Feature> features, object featuresIdentity, bool showText, string orientationDescription = "")
    {
        _features = features.ToList();

        if (!ReferenceEquals(featuresIdentity, _featuresSource))
        {
            _featuresSource = featuresIdentity;
            _drawingPlane = DrawingPlaneDetector.Detect(_features);
            _planeOverridden = false;

            if (_alignModeActive)
            {
                _alignModeActive = false;
                AlignModeExited?.Invoke();
            }

            _hoveredLineIndex = null;
        }

        _orientationDescription = orientationDescription;
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
            case Keys.Escape:
                if (_alignModeActive)
                {
                    _alignModeActive = false;
                    _hoveredLineIndex = null;
                    Cursor = Cursors.Default;
                    AlignModeExited?.Invoke();
                    Invalidate();
                    e.Handled = true;
                }
                break;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (_alignModeActive && e.Button == MouseButtons.Left && _hoveredLineIndex is int index)
            ConfirmAlign(index);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_alignModeActive || _screenLines.Count == 0)
        {
            if (_hoveredLineIndex is not null)
            {
                _hoveredLineIndex = null;
                Cursor = Cursors.Default;
                Invalidate();
            }

            return;
        }

        var nearest = FindNearestLineIndex(e.Location);
        if (nearest == _hoveredLineIndex)
            return;

        _hoveredLineIndex = nearest;
        Cursor = nearest.HasValue ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hoveredLineIndex is null)
            return;

        _hoveredLineIndex = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    private int? FindNearestLineIndex(Point location)
    {
        int? bestIndex = null;
        var bestDistance = float.MaxValue;

        foreach (var line in _screenLines)
        {
            var distance = DistanceToSegment(location, line.Start, line.End);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = line.Index;
            }
        }

        return bestDistance <= HitToleranceScreenPixels ? bestIndex : null;
    }

    private static float DistanceToSegment(Point point, PointF start, PointF end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared <= float.Epsilon)
            return Distance(point, start);

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);

        var closest = new PointF(start.X + t * dx, start.Y + t * dy);
        return Distance(point, closest);
    }

    private static float Distance(Point a, PointF b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    // Implements the align pick: rotates the whole feature set so that the picked line
    // becomes exactly horizontal in the current drawing plane.
    private void ConfirmAlign(int index)
    {
        // _features already holds the current, live-transformed state (MainForm mutates
        // its own feature list directly on every click and hands the result straight
        // through), so the required rotation is measured directly against it - no
        // "orientation" object to apply first.
        var oriented = _features[index];

        var (u1, v1, _) = DrawingPlaneMapper.Project(oriented, _drawingPlane);
        var (u2, v2, _) = DrawingPlaneMapper.Project(oriented.X2!.Value, oriented.Y2!.Value, oriented.Z2 ?? 0.0, _drawingPlane);

        var angleDeg = Math.Atan2(v2 - v1, u2 - u1) * 180.0 / Math.PI;

        // Reduce mod 180 into (-90, 90] - a line has no inherent direction, so a segment
        // pointing "backwards" should still be treated as already-horizontal, not needing a
        // near-180-degree spin.
        var normalized = angleDeg % 180.0;
        if (normalized <= -90.0)
            normalized += 180.0;
        else if (normalized > 90.0)
            normalized -= 180.0;

        // Feature.WithRotatedInPlane's rotation formula solves to v' = 0 exactly when
        // theta = atan2(v, u) - i.e. the required align angle is this normalized angle
        // itself, not its negation (verified numerically: a 45-degree segment needs +45 fed
        // into WithRotatedInPlane to land horizontal, not -45).
        var requiredAlignAngle = normalized;

        LineAligned?.Invoke(requiredAlignAngle, _drawingPlane);

        _alignModeActive = false;
        _hoveredLineIndex = null;
        Cursor = Cursors.Default;
        AlignModeExited?.Invoke();
        Invalidate();
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
            _screenLines = [];
            using var brush = new SolidBrush(Color.DimGray);
            g.DrawString("Select a report file to preview", Font, brush, new PointF(16, 16));
            return;
        }

        var projected = _features
            .Select(f =>
            {
                var (u, v, _) = DrawingPlaneMapper.Project(f, _drawingPlane);
                double? u2 = null, v2 = null;
                if (f.IsLine)
                {
                    var (lu, lv, _) = DrawingPlaneMapper.Project(f.X2!.Value, f.Y2!.Value, f.Z2 ?? 0.0, _drawingPlane);
                    u2 = lu;
                    v2 = lv;
                }
                return (Feature: f, U: u, V: v, U2: u2, V2: v2);
            })
            .ToList();

        var minX = projected.Min(p => Math.Min(p.U - p.Feature.Radius, p.U2 - p.Feature.Radius ?? p.U));
        var maxX = projected.Max(p => Math.Max(p.U + p.Feature.Radius, p.U2 + p.Feature.Radius ?? p.U));
        var minY = projected.Min(p => Math.Min(p.V - p.Feature.Radius, p.V2 - p.Feature.Radius ?? p.V));
        var maxY = projected.Max(p => Math.Max(p.V + p.Feature.Radius, p.V2 + p.Feature.Radius ?? p.V));

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
        using var linePen = new Pen(Color.FromArgb(160, 60, 25), 1.4f);
        using var pointBrush = new SolidBrush(Color.FromArgb(20, 20, 20));
        using var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40));

        var (uLabel, vLabel) = DrawingPlaneMapper.AxisLabels(_drawingPlane);
        DrawAxes(g, ToScreen, axisPen, uLabel, vLabel);

        using var highlightPen = new Pen(Color.FromArgb(255, 140, 0), 3.2f);
        var newScreenLines = new List<(int Index, PointF Start, PointF End)>();

        for (var index = 0; index < projected.Count; index++)
        {
            var p = projected[index];
            var centre = ToScreen(p.U, p.V);

            if (p.Feature.IsLine)
            {
                var end = ToScreen(p.U2!.Value, p.V2!.Value);
                newScreenLines.Add((index, centre, end));

                g.DrawLine(linePen, centre, end);
                if (_alignModeActive && index == _hoveredLineIndex)
                    g.DrawLine(highlightPen, centre, end);

                g.FillEllipse(pointBrush, centre.X - 2.2f, centre.Y - 2.2f, 4.4f, 4.4f);
                g.FillEllipse(pointBrush, end.X - 2.2f, end.Y - 2.2f, 4.4f, 4.4f);

                if (_showText)
                {
                    var midX = (centre.X + end.X) / 2f;
                    var midY = (centre.Y + end.Y) / 2f;
                    g.DrawString(p.Feature.Name, Font, textBrush, midX + 4f, midY - 4f - Font.Height);
                }

                continue;
            }

            var r = ToScreenLength(p.Feature.Radius);
            g.DrawEllipse(circlePen, centre.X - r, centre.Y - r, r * 2, r * 2);
            g.FillEllipse(pointBrush, centre.X - 2.2f, centre.Y - 2.2f, 4.4f, 4.4f);

            if (_showText)
            {
                var offset = Math.Max(r * 0.08f, 4f);
                g.DrawString(p.Feature.Name, Font, textBrush, centre.X + offset, centre.Y - offset - Font.Height);
            }
        }

        _screenLines = newScreenLines;

        using var footerBrush = new SolidBrush(Color.DimGray);
        var planeSource = _planeOverridden ? "manual" : "auto";
        var footer = $"{_features.Count} features | Plane {_drawingPlane} ({planeSource}, ↑↓←→ to change) | {uLabel} {minX:0.###} to {maxX:0.###} | {vLabel} {minY:0.###} to {maxY:0.###}";
        if (_orientationDescription.Length > 0)
            footer += $" | {_orientationDescription}";
        if (_alignModeActive)
            footer += " | Align mode: click a line to level it (Esc to cancel)";
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
