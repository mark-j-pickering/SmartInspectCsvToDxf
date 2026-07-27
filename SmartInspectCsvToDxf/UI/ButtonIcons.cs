using System.Drawing.Drawing2D;

namespace SmartInspectCsvToDxf.UI;

// Small vector icons for the preview navigation buttons, drawn at runtime with GDI+
// rather than shipped as image assets - keeps them crisp at any size and needs no
// external icon tool. Mirror icons show two triangles reflected across a dashed axis
// line; rotate icons are a curved arrow. Kept in sync with the actual transform each
// button performs (see Feature.WithMirrorX/WithMirrorY): Mirror X negates Y (a top/bottom flip),
// Mirror Y negates X (a left/right flip) - matching the app's existing axis-of-
// reflection naming, not the more common "flip along X" reading of the button name.
internal static class ButtonIcons
{
    private static readonly Color IconColor = Color.FromArgb(25, 90, 160);

    public static Bitmap MirrorX(int size = 24) => DrawMirror(size, vertical: true);

    public static Bitmap MirrorY(int size = 24) => DrawMirror(size, vertical: false);

    public static Bitmap ShowText(int size = 24) => DrawShowText(size);

    public static Bitmap RotateRight(int size = 24) => DrawRotateArc(size);

    public static Bitmap Align(int size = 24) => DrawAlign(size);

    public static Bitmap RotateLeft(int size = 24)
    {
        var bitmap = DrawRotateArc(size);
        bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
        return bitmap;
    }

    // vertical: true draws a top/bottom flip (triangles above/below a horizontal axis
    // line) - the icon for Mirror X, which negates Y. vertical: false draws a left/right
    // flip (triangles left/right of a vertical axis line) - Mirror Y, which negates X.
    private static Bitmap DrawMirror(int size, bool vertical)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var axisPen = new Pen(IconColor, Math.Max(1.2f, size / 16f)) { DashStyle = DashStyle.Dash };
        using var brush = new SolidBrush(IconColor);

        var margin = size * 0.16f;
        var mid = size / 2f;
        var gap = size * 0.06f;
        var triLength = size * 0.32f;
        var triWidth = size * 0.34f;

        if (vertical)
        {
            g.DrawLine(axisPen, margin, mid, size - margin, mid);

            g.FillPolygon(brush, new[]
            {
                new PointF(mid, mid - gap - triLength),
                new PointF(mid - triWidth / 2, mid - gap),
                new PointF(mid + triWidth / 2, mid - gap)
            });

            g.FillPolygon(brush, new[]
            {
                new PointF(mid, mid + gap + triLength),
                new PointF(mid - triWidth / 2, mid + gap),
                new PointF(mid + triWidth / 2, mid + gap)
            });
        }
        else
        {
            g.DrawLine(axisPen, mid, margin, mid, size - margin);

            g.FillPolygon(brush, new[]
            {
                new PointF(mid - gap - triLength, mid),
                new PointF(mid - gap, mid - triWidth / 2),
                new PointF(mid - gap, mid + triWidth / 2)
            });

            g.FillPolygon(brush, new[]
            {
                new PointF(mid + gap + triLength, mid),
                new PointF(mid + gap, mid - triWidth / 2),
                new PointF(mid + gap, mid + triWidth / 2)
            });
        }

        return bitmap;
    }

    // A little label/tag with lines of text on it, for the Show Text toggle.
    private static Bitmap DrawShowText(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var margin = size * 0.14f;
        var rect = new RectangleF(margin, margin, size - 2 * margin, size - 2 * margin);
        var cornerRadius = size * 0.12f;

        using var borderPen = new Pen(IconColor, Math.Max(1.4f, size / 14f));
        using var path = RoundedRect(rect, cornerRadius);
        g.DrawPath(borderPen, path);

        using var linePen = new Pen(IconColor, Math.Max(1.2f, size / 16f));
        var lineMarginX = rect.Width * 0.18f;
        var lineY1 = rect.Top + rect.Height * 0.35f;
        var lineY2 = rect.Top + rect.Height * 0.58f;
        var lineY3 = rect.Top + rect.Height * 0.81f;

        g.DrawLine(linePen, rect.Left + lineMarginX, lineY1, rect.Right - lineMarginX, lineY1);
        g.DrawLine(linePen, rect.Left + lineMarginX, lineY2, rect.Right - lineMarginX * 1.6f, lineY2);
        g.DrawLine(linePen, rect.Left + lineMarginX, lineY3, rect.Right - lineMarginX * 2.2f, lineY3);

        return bitmap;
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Canonical clockwise arc with an arrowhead; RotateLeft() mirrors this bitmap
    // horizontally rather than duplicating the geometry for a counter-clockwise sweep.
    private static Bitmap DrawRotateArc(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var margin = size * 0.2f;
        var rect = new RectangleF(margin, margin, size - 2 * margin, size - 2 * margin);
        var radius = rect.Width / 2f;
        var centre = new PointF(rect.X + radius, rect.Y + radius);

        const float startAngleDeg = 100f;
        const float sweepDeg = 250f;

        using var arcPen = new Pen(IconColor, Math.Max(1.8f, size / 11f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(arcPen, rect, startAngleDeg, sweepDeg);

        var endAngleRad = (startAngleDeg + sweepDeg) * Math.PI / 180.0;
        var tangentRad = endAngleRad + Math.PI / 2.0;

        var tip = new PointF(
            (float)(centre.X + radius * Math.Cos(endAngleRad)),
            (float)(centre.Y + radius * Math.Sin(endAngleRad)));

        var headLength = size * 0.26f;
        var headWidth = size * 0.24f;
        var back = new PointF(
            (float)(tip.X - headLength * Math.Cos(tangentRad)),
            (float)(tip.Y - headLength * Math.Sin(tangentRad)));

        var perpRad = tangentRad + Math.PI / 2.0;
        var corner1 = new PointF(
            (float)(back.X + headWidth / 2 * Math.Cos(perpRad)),
            (float)(back.Y + headWidth / 2 * Math.Sin(perpRad)));
        var corner2 = new PointF(
            (float)(back.X - headWidth / 2 * Math.Cos(perpRad)),
            (float)(back.Y - headWidth / 2 * Math.Sin(perpRad)));

        using var brush = new SolidBrush(IconColor);
        g.FillPolygon(brush, new[] { tip, corner1, corner2 });

        return bitmap;
    }

    // A tilted line snapping down onto a dashed horizontal target line, for the Align
    // (pick-a-line-to-level) toggle.
    private static Bitmap DrawAlign(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var margin = size * 0.16f;
        var baselineY = size - size * 0.28f;

        using var baselinePen = new Pen(IconColor, Math.Max(1.2f, size / 16f)) { DashStyle = DashStyle.Dash };
        g.DrawLine(baselinePen, margin, baselineY, size - margin, baselineY);

        var diagonalStart = new PointF(margin + size * 0.04f, baselineY);
        var diagonalEnd = new PointF(size * 0.6f, size * 0.18f);

        using var diagonalPen = new Pen(IconColor, Math.Max(1.6f, size / 13f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(diagonalPen, diagonalStart, diagonalEnd);

        // Small curved arrow bending from the diagonal's tip down onto the baseline,
        // echoing the sweep-plus-arrowhead style used by DrawRotateArc.
        var arcRect = new RectangleF(size * 0.42f, size * 0.34f, size * 0.5f, size * 0.5f);
        const float startAngleDeg = 200f;
        const float sweepDeg = 120f;

        using var arcPen = new Pen(IconColor, Math.Max(1.4f, size / 15f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(arcPen, arcRect, startAngleDeg, sweepDeg);

        var radius = arcRect.Width / 2f;
        var centre = new PointF(arcRect.X + radius, arcRect.Y + radius);
        var endAngleRad = (startAngleDeg + sweepDeg) * Math.PI / 180.0;
        var tangentRad = endAngleRad + Math.PI / 2.0;

        var tip = new PointF(
            (float)(centre.X + radius * Math.Cos(endAngleRad)),
            (float)(centre.Y + radius * Math.Sin(endAngleRad)));

        var headLength = size * 0.2f;
        var headWidth = size * 0.18f;
        var back = new PointF(
            (float)(tip.X - headLength * Math.Cos(tangentRad)),
            (float)(tip.Y - headLength * Math.Sin(tangentRad)));

        var perpRad = tangentRad + Math.PI / 2.0;
        var corner1 = new PointF(
            (float)(back.X + headWidth / 2 * Math.Cos(perpRad)),
            (float)(back.Y + headWidth / 2 * Math.Sin(perpRad)));
        var corner2 = new PointF(
            (float)(back.X - headWidth / 2 * Math.Cos(perpRad)),
            (float)(back.Y - headWidth / 2 * Math.Sin(perpRad)));

        using var brush = new SolidBrush(IconColor);
        g.FillPolygon(brush, new[] { tip, corner1, corner2 });

        return bitmap;
    }
}
