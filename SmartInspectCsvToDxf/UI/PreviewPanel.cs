using System.Diagnostics;
using SmartInspectCsvToDxf.Models;
using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.UI;

public sealed class PreviewPanel : Panel
{
    private const string BackgroundResourceName = "SmartInspectCsvToDxf.Resources.BUM_logo_background.png";
    private static readonly Image EmbeddedBackgroundImage = LoadEmbeddedBackgroundImage();
    private static readonly DrawingPlane[] Planes = Enum.GetValues<DrawingPlane>();

    private const float HitToleranceScreenPixels = 6f;
    private const float SnapToleranceScreenPixels = 10f;
    private const int AnimationDurationMs = 1000;
    private const int AnimationIntervalMs = 10;

    // The features drawn/hit-tested here are always already in their final, current state -
    // MainForm mutates its own live feature list directly on every rotate/mirror/align click
    // and hands the result straight through; this panel has no notion of "orientation" to
    // apply, it just draws whatever it's given. During an animated transition (see
    // AnimateRotation/AnimateMirror), _features instead holds a continuously-interpolated
    // in-between state, updated on a timer - OnPaint itself needs no special-casing for this,
    // since it already recomputes the camera fit fresh from _features on every repaint.
    private List<Feature> _features = [];
    private object? _featuresSource;
    private bool _showText = true;
    private DrawingPlane _drawingPlane = DrawingPlane.XY;
    private bool _planeOverridden;
    private bool _alignModeActive;
    // Which feature (and which of its two points, for a line) the axes are drawn through and
    // coordinate readouts are measured relative to. Null means the true world origin (0,0).
    // Deliberately a *reference* rather than a frozen (U,V) snapshot, so that when the picked
    // feature later moves under a rotate/mirror/align, the origin re-projects from wherever
    // that feature/point currently is (recomputed fresh every OnPaint via GetOriginUV) instead
    // of staying pinned to its old position - the axes then visibly follow the same physical
    // point through an animation, not just jump to it at the end. Never affects the underlying
    // Feature data drawn/exported - display only.
    private (int FeatureIndex, bool IsSecondPoint)? _originKeyPoint;
    private bool _originPickModeActive;
    // Screen-space endpoints of every line feature, cached from the last OnPaint, keyed by
    // index into _features rather than by Feature reference, since MainForm hands in a new
    // list instance on every rotate/mirror/align click and reference equality would never
    // match hover state across frames.
    private List<(int Index, PointF Start, PointF End)> _screenLines = [];
    private int? _hoveredLineIndex;

    // Feature centres (and line endpoints) in both screen and world space, cached from the
    // last OnPaint - reused on every mouse move to find the nearest key point to snap to,
    // without recomputing the whole projection/camera-fit pass on every pixel of mouse
    // travel. The view's own scale/centre are cached alongside for the same reason: OnPaint
    // recomputes them fresh from the current feature bounds every frame, and mouse-move needs
    // the exact same values to invert a screen point back to world coordinates.
    // IsSecondPoint distinguishes a line feature's two key points (X/Y vs X2/Y2) - needed so
    // Set Origin can remember which end of the line was picked (see _originKeyPoint above).
    private List<(PointF Screen, double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)> _keyPoints = [];
    // Screen-space bounding box of every drawn feature label, cached from the last OnPaint -
    // lets a feature be snapped/hovered by its name text, not just its geometry, since a
    // label can sit well outside SnapToleranceScreenPixels of its own key point in a crowded
    // drawing. Empty whenever _showText is off, since nothing is drawn to hit-test.
    private List<(RectangleF Bounds, int FeatureIndex)> _labelRects = [];
    private double _viewScale = 1.0;
    private double _viewCentreX;
    private double _viewCentreY;
    private (double U, double V)? _mouseWorldPoint;
    private (double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)? _snappedPoint;

    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly Stopwatch _animationStopwatch = new();
    private Func<double, List<Feature>>? _animationInterpolator;
    private List<Feature>? _animationFinalFeatures;

    public bool IsAnimating => _animationTimer.Enabled;

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
                _snappedPoint = null;
                Cursor = Cursors.Default;
            }

            Invalidate();
        }
    }

    // MainForm sets this from the Set Origin toggle button. Mutually exclusive with align
    // mode at the MainForm level (each button unchecks the other), so no interaction between
    // the two is handled here beyond both being independently cancellable.
    public bool OriginPickModeActive
    {
        get => _originPickModeActive;
        set
        {
            if (_originPickModeActive == value)
                return;

            _originPickModeActive = value;
            Cursor = value ? Cursors.Cross : Cursors.Default;
            Invalidate();
        }
    }

    // Fired once, on a successful line pick, with the rotation angle/plane needed to level it.
    public event Action<double, DrawingPlane>? LineAligned;

    // Fired whenever align mode ends: a successful pick (after LineAligned), an Escape
    // cancel, or a new report being loaded while align mode was still active - lets
    // MainForm keep its toggle button's Checked state in sync.
    public event Action? AlignModeExited;

    // Fired whenever origin-pick mode ends: a successful pick, an Escape cancel, a new report
    // loading mid-pick, or a rotate/mirror animation starting - lets MainForm keep its toggle
    // button's Checked state in sync, mirroring AlignModeExited.
    public event Action? OriginPickModeExited;

    public PreviewPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;

        _animationTimer = new System.Windows.Forms.Timer { Interval = AnimationIntervalMs };
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    // PreviewPanel is hand-written (no Designer partial/components container to dispose the
    // animation timer for free the way MainForm's _refreshTimer gets), so this needs its own
    // explicit lifecycle.
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
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
    public void SetFeatures(IEnumerable<Feature> features, object featuresIdentity, bool showText)
    {
        // Cancel first: without this, toggling Show Labels / Reset / loading a different file
        // while a rotate/mirror animation is mid-flight would set _features correctly for an
        // instant, then the animation timer's very next tick would overwrite it with a stale
        // interpolated frame before eventually snapping to the *old* animation's target.
        CancelAnimation();

        _features = features.ToList();

        if (!ReferenceEquals(featuresIdentity, _featuresSource))
        {
            _featuresSource = featuresIdentity;
            _drawingPlane = DrawingPlaneDetector.Detect(_features);
            _planeOverridden = false;
            _originKeyPoint = null;

            if (_alignModeActive)
            {
                _alignModeActive = false;
                AlignModeExited?.Invoke();
            }

            if (_originPickModeActive)
            {
                _originPickModeActive = false;
                Cursor = Cursors.Default;
                OriginPickModeExited?.Invoke();
            }

            _hoveredLineIndex = null;
            _snappedPoint = null;
        }

        _showText = showText;
        Invalidate();
    }

    // Animates the whole feature set rotating through angleDegrees about world (0,0) in the
    // given plane, then snaps to finalFeatures exactly (rather than trusting the last
    // interpolated frame, which could drift from MainForm's authoritative state by floating-
    // point rounding). Rotate Left/Right must pass DrawingPlane.XY here regardless of the
    // panel's *currently viewed* _drawingPlane - WithRotatedRight90/WithRotatedLeft90 always
    // rotate raw X/Y, so animating against any other plane would visibly spin the wrong axis
    // pair for the animation's duration and then pop to the correct final pose. Align passes
    // its own picked plane through instead, since it's already plane-aware.
    public void AnimateRotation(double angleDegrees, DrawingPlane plane, List<Feature> finalFeatures, object featuresIdentity, bool showText)
    {
        var fromFeatures = _features;
        StartAnimation(
            t => fromFeatures.Select(f => f.WithRotatedInPlane(angleDegrees * t, plane)).ToList(),
            finalFeatures, featuresIdentity, showText);
    }

    // Animates a mirror as a "card flip": scales the mirrored axis from +1 through 0 to -1.
    // A literal reflection has no continuous in-plane family of matrices to interpolate
    // through (determinant -1 isn't reachable from identity via rotation alone), so this
    // squash-then-unsquash is the standard substitute.
    public void AnimateMirror(bool mirrorX, List<Feature> finalFeatures, object featuresIdentity, bool showText)
    {
        var fromFeatures = _features;
        StartAnimation(
            t =>
            {
                var s = 1.0 - 2.0 * t; // Lerp(1, -1, t)
                return fromFeatures.Select(f => mirrorX ? f.WithScaled(1, s) : f.WithScaled(s, 1)).ToList();
            },
            finalFeatures, featuresIdentity, showText);
    }

    // Shared by AnimateRotation/AnimateMirror. If an animation is already running, this
    // retargets it: MainForm's own feature list is always mutated instantly/authoritatively
    // regardless of what the panel is mid-way through showing, so a new click just captures
    // whatever _features currently displays (even a half-finished frame) as the fresh
    // starting point and restarts the clock toward the new target, rather than queuing or
    // ignoring the click.
    private void StartAnimation(Func<double, List<Feature>> interpolator, List<Feature> finalFeatures, object featuresIdentity, bool showText)
    {
        if (_alignModeActive)
        {
            _alignModeActive = false;
            _hoveredLineIndex = null;
            _snappedPoint = null;
            Cursor = Cursors.Default;
            AlignModeExited?.Invoke();
        }

        if (_originPickModeActive)
        {
            _originPickModeActive = false;
            Cursor = Cursors.Default;
            OriginPickModeExited?.Invoke();
        }

        _animationInterpolator = interpolator;
        _animationFinalFeatures = finalFeatures.ToList();
        _featuresSource = featuresIdentity;
        _showText = showText;

        _animationStopwatch.Restart();
        _animationTimer.Start(); // no-op if already running - this is the retarget path.
    }

    private void CancelAnimation()
    {
        _animationTimer.Stop();
        _animationInterpolator = null;
        _animationFinalFeatures = null;
    }

    // Clears any picked origin and cancels an in-progress pick, back to the true world origin.
    // Called by MainForm's Reset button - like the drawing-plane override, this is view state
    // rather than feature data, but Reset is the one place that means "start this preview
    // fresh" so it clears it too rather than requiring a separate control.
    public void ResetOrigin()
    {
        var changed = false;

        if (_originPickModeActive)
        {
            _originPickModeActive = false;
            Cursor = Cursors.Default;
            OriginPickModeExited?.Invoke();
            changed = true;
        }

        if (_originKeyPoint is not null)
        {
            _originKeyPoint = null;
            changed = true;
        }

        if (changed)
            Invalidate();
    }

    // Reverts a drawing-plane override (arrow-key cycling, see CyclePlane) back to
    // auto-detection. Called by MainForm's Reset button alongside ResetOrigin() - same "view
    // state, not feature data, but Reset still means start this preview fresh" reasoning.
    public void ResetPlane()
    {
        if (!_planeOverridden)
            return;

        _planeOverridden = false;
        _drawingPlane = DrawingPlaneDetector.Detect(_features);
        Invalidate();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        // Elapsed real time, not a tick count * interval - Timer ticks aren't guaranteed to
        // land exactly on schedule under UI-thread load, so this self-corrects instead of
        // running slow when the message loop is busy.
        var t = Math.Clamp(_animationStopwatch.Elapsed.TotalMilliseconds / AnimationDurationMs, 0.0, 1.0);

        if (t >= 1.0)
        {
            _animationTimer.Stop();
            _features = _animationFinalFeatures!;
            _animationInterpolator = null;
            _animationFinalFeatures = null;
            Invalidate();
            return;
        }

        var eased = 1.0 - Math.Pow(1.0 - t, 3); // ease-out-cubic
        _features = _animationInterpolator!(eased);
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
                    _snappedPoint = null;
                    Cursor = Cursors.Default;
                    AlignModeExited?.Invoke();
                    Invalidate();
                    e.Handled = true;
                }
                else if (_originPickModeActive)
                {
                    _originPickModeActive = false;
                    Cursor = Cursors.Default;
                    OriginPickModeExited?.Invoke();
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
        else if (_originPickModeActive && e.Button == MouseButtons.Left && _snappedPoint is { } snapped)
            ConfirmSetOrigin(snapped);
    }

    // Picks the origin: a single-shot action just like Mirror/Rotate/Align - this replaces
    // whatever origin was previously picked (if any) rather than composing with it, and
    // there's no undo/redo. Unlike those, it's purely a display offset (axes + coordinate
    // readouts in OnPaint) and never touches Feature data, so MainForm has nothing to track
    // for it beyond keeping the toggle button's Checked state in sync via OriginPickModeExited.
    // Stores which feature/point was picked rather than its (U,V) at pick time, so the origin
    // keeps following that same point through any later rotate/mirror/align (see
    // _originKeyPoint).
    private void ConfirmSetOrigin((double U, double V, string Name, int FeatureIndex, bool IsSecondPoint) snapped)
    {
        _originKeyPoint = (snapped.FeatureIndex, snapped.IsSecondPoint);
        _originPickModeActive = false;
        Cursor = Cursors.Default;
        OriginPickModeExited?.Invoke();
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // Align-mode line hover is resolved first so UpdateMouseTracking (below) can key its
        // snap/highlight display off this move's _hoveredLineIndex rather than the previous
        // move's.
        if (!_alignModeActive || IsAnimating || _screenLines.Count == 0)
        {
            if (_hoveredLineIndex is not null)
            {
                _hoveredLineIndex = null;
                Cursor = Cursors.Default;
            }
        }
        else
        {
            // Falling back to a label hit lets a line be picked for align by its name text
            // too, not just by hovering the segment itself.
            var nearest = FindNearestLineIndex(e.Location) ?? FindLineIndexFromLabel(e.Location);
            if (nearest != _hoveredLineIndex)
            {
                _hoveredLineIndex = nearest;
                Cursor = nearest.HasValue ? Cursors.Hand : Cursors.Default;
            }
        }

        UpdateMouseTracking(e.Location);

        // Unlike the hover-only paths above, the coordinate readout changes on every pixel of
        // mouse travel, so this always repaints rather than only when something discrete
        // (hovered line, snap target) changes.
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        _mouseWorldPoint = null;
        _snappedPoint = null;

        if (_hoveredLineIndex is null)
        {
            Invalidate();
            return;
        }

        _hoveredLineIndex = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    // Converts the mouse position to world coordinates using the view transform cached from
    // the last OnPaint, then looks for the nearest key point (feature centre or line
    // endpoint) within SnapToleranceScreenPixels to snap to.
    private void UpdateMouseTracking(Point location)
    {
        if (_features.Count == 0)
        {
            _mouseWorldPoint = null;
            _snappedPoint = null;
            return;
        }

        var u = (location.X - Width / 2.0) / _viewScale + _viewCentreX;
        var v = _viewCentreY - (location.Y - Height / 2.0) / _viewScale;
        _mouseWorldPoint = (u, v);

        // Align mode has its own line hit-test (FindNearestLineIndex, a looser point-to-
        // segment distance rather than nearest-key-point) - reuse whatever it just found
        // instead of running an independent nearest-key-point search, so the green dot/label
        // highlight always tracks the same line align mode would act on if clicked. This also
        // keeps circles from ever snapping/highlighting during align mode "for free", since
        // _hoveredLineIndex only ever refers to a line feature.
        if (_alignModeActive)
        {
            _snappedPoint = _hoveredLineIndex is int hoveredIndex
                ? NearestKeyPointOnFeature(location, hoveredIndex)
                : null;
            return;
        }

        _snappedPoint = FindNearestKeyPoint(location, _keyPoints, SnapToleranceScreenPixels)
            ?? FindSnapPointFromLabel(location);
    }

    // Falls back to hit-testing feature labels when the cursor isn't within
    // SnapToleranceScreenPixels of any key point directly - lets a feature be snapped/selected
    // via its name text, not just its geometry, since a label can sit well clear of its own
    // key point once a drawing gets crowded.
    private (double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)? FindSnapPointFromLabel(Point location)
    {
        foreach (var label in _labelRects)
        {
            if (label.Bounds.Contains(location))
                return NearestKeyPointOnFeature(location, label.FeatureIndex);
        }

        return null;
    }

    // Line-only counterpart used by align mode's own hover, so a line can also be picked by
    // its label rather than only by hovering the segment itself.
    private int? FindLineIndexFromLabel(Point location)
    {
        foreach (var label in _labelRects)
        {
            if (label.Bounds.Contains(location) && _features[label.FeatureIndex].IsLine)
                return label.FeatureIndex;
        }

        return null;
    }

    // Nearest key point across every feature, gated by SnapToleranceScreenPixels so the mouse
    // has to actually be close to something to snap.
    private static (double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)? FindNearestKeyPoint(
        Point location, List<(PointF Screen, double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)> keyPoints, float tolerance)
    {
        (double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)? nearest = null;
        var bestDistance = float.MaxValue;
        foreach (var keyPoint in keyPoints)
        {
            var distance = Distance(location, keyPoint.Screen);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = (keyPoint.U, keyPoint.V, keyPoint.Name, keyPoint.FeatureIndex, keyPoint.IsSecondPoint);
            }
        }

        return bestDistance <= tolerance ? nearest : null;
    }

    // Nearest key point belonging to one specific feature, with no distance cutoff - used for
    // the align-hovered line, which is already known (via FindNearestLineIndex's own, looser
    // tolerance) to be the one the mouse is over.
    private (double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)? NearestKeyPointOnFeature(Point location, int featureIndex)
    {
        (double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)? nearest = null;
        var bestDistance = float.MaxValue;
        foreach (var keyPoint in _keyPoints)
        {
            if (keyPoint.FeatureIndex != featureIndex)
                continue;

            var distance = Distance(location, keyPoint.Screen);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = (keyPoint.U, keyPoint.V, keyPoint.Name, keyPoint.FeatureIndex, keyPoint.IsSecondPoint);
            }
        }

        return nearest;
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
    // becomes exactly horizontal *or* vertical in the current drawing plane, whichever is
    // the nearer orthogonal direction - not always horizontal regardless of how close the
    // line already is to vertical.
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

        // Reduce mod 90 into (-45, 45] - a line has no inherent direction (so a segment
        // pointing "backwards" is still just as horizontal/vertical), and unlike a mod-180
        // reduction (which always targets horizontal, up to a near-90-degree spin for a
        // near-vertical line), this picks whichever of horizontal or vertical is actually
        // closer, capping the rotation at 45 degrees.
        var normalized = angleDeg % 90.0;
        if (normalized <= -45.0)
            normalized += 90.0;
        else if (normalized > 45.0)
            normalized -= 90.0;

        // Feature.WithRotatedInPlane's rotation formula solves to v' = 0 (horizontal) when
        // theta = atan2(v, u), not its negation (verified numerically: a 45-degree segment
        // needs +45 fed into WithRotatedInPlane to land horizontal, not -45); since a target
        // 90 degrees away from that also satisfies u' = 0 (vertical) using the very same
        // formula, feeding it this mod-90-reduced angle correctly lands on whichever of the
        // two orthogonal directions is nearer.
        var requiredAlignAngle = normalized;
        var plane = _drawingPlane;

        // Exit align mode before invoking LineAligned, not after: MainForm's handler calls
        // back into AnimateRotation, whose StartAnimation would otherwise find align mode
        // still flagged active and perform this same cleanup itself, then this method would
        // redundantly repeat it (harmless - AlignModeExited firing twice just no-ops an
        // already-unchecked checkbox - but doing it in this order avoids it cleanly).
        _alignModeActive = false;
        _hoveredLineIndex = null;
        _snappedPoint = null;
        Cursor = Cursors.Default;
        AlignModeExited?.Invoke();
        Invalidate();

        LineAligned?.Invoke(requiredAlignAngle, plane);
    }

    private void CyclePlane(int direction)
    {
        if (_features.Count == 0)
            return;

        var currentIndex = Array.IndexOf(Planes, _drawingPlane);
        var nextIndex = ((currentIndex + direction) % Planes.Length + Planes.Length) % Planes.Length;
        _drawingPlane = Planes[nextIndex];
        _planeOverridden = true;
        // No need to clear a picked origin here - GetOriginUV() re-projects the tracked
        // feature/point fresh under whichever plane is active, so it stays meaningful (just
        // reported in different axes) rather than needing to be re-picked after cycling.
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
            _keyPoints = [];
            _labelRects = [];
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

        // Cached so OnMouseMove can invert a screen point back to world coordinates without
        // redoing this whole camera-fit computation on every pixel of mouse travel.
        _viewScale = scale;
        _viewCentreX = centreX;
        _viewCentreY = centreY;

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
        var (originU, originV) = GetOriginUV();
        DrawAxes(g, ToScreen, axisPen, uLabel, vLabel, originU, originV);

        using var highlightPen = new Pen(Color.FromArgb(255, 140, 0), 3.2f);
        using var boldLabelFont = new Font(Font, FontStyle.Bold);
        using var dimmedTextBrush = new SolidBrush(Color.FromArgb(190, 190, 190));
        using var snappedTextBrush = new SolidBrush(Color.FromArgb(0, 120, 0));
        var newScreenLines = new List<(int Index, PointF Start, PointF End)>();
        var newKeyPoints = new List<(PointF Screen, double U, double V, string Name, int FeatureIndex, bool IsSecondPoint)>();
        var newLabelRects = new List<(RectangleF Bounds, int FeatureIndex)>();

        // When something is snapped, its label is bolded/coloured to stand out and every
        // other label is dimmed - otherwise labels use their normal colour/weight.
        (Font Font, Brush Brush) LabelStyle(int index) => _snappedPoint?.FeatureIndex switch
        {
            null => (Font, textBrush),
            var snappedIndex when snappedIndex == index => (boldLabelFont, snappedTextBrush),
            _ => (Font, dimmedTextBrush)
        };

        for (var index = 0; index < projected.Count; index++)
        {
            var p = projected[index];
            var centre = ToScreen(p.U, p.V);

            if (p.Feature.IsLine)
            {
                var end = ToScreen(p.U2!.Value, p.V2!.Value);
                newScreenLines.Add((index, centre, end));
                newKeyPoints.Add((centre, p.U, p.V, p.Feature.Name, index, false));
                newKeyPoints.Add((end, p.U2.Value, p.V2.Value, p.Feature.Name, index, true));

                g.DrawLine(linePen, centre, end);
                // _snappedPoint already tracks the align-hovered line (see
                // UpdateMouseTracking), so this one check covers both align-mode hover and
                // ordinary key-point snapping.
                if (_snappedPoint?.FeatureIndex == index)
                    g.DrawLine(highlightPen, centre, end);

                g.FillEllipse(pointBrush, centre.X - 2.2f, centre.Y - 2.2f, 4.4f, 4.4f);
                g.FillEllipse(pointBrush, end.X - 2.2f, end.Y - 2.2f, 4.4f, 4.4f);

                if (_showText)
                {
                    var (labelFont, labelBrush) = LabelStyle(index);
                    var midX = (centre.X + end.X) / 2f;
                    var midY = (centre.Y + end.Y) / 2f;
                    var labelPos = new PointF(midX + 4f, midY - 4f - Font.Height);
                    newLabelRects.Add((new RectangleF(labelPos, g.MeasureString(p.Feature.Name, labelFont)), index));
                    g.DrawString(p.Feature.Name, labelFont, labelBrush, labelPos);
                }

                continue;
            }

            newKeyPoints.Add((centre, p.U, p.V, p.Feature.Name, index, false));

            var r = ToScreenLength(p.Feature.Radius);
            g.DrawEllipse(circlePen, centre.X - r, centre.Y - r, r * 2, r * 2);
            if (_snappedPoint?.FeatureIndex == index)
                g.DrawEllipse(highlightPen, centre.X - r, centre.Y - r, r * 2, r * 2);
            g.FillEllipse(pointBrush, centre.X - 2.2f, centre.Y - 2.2f, 4.4f, 4.4f);

            if (_showText)
            {
                var (labelFont, labelBrush) = LabelStyle(index);
                var offset = Math.Max(r * 0.08f, 4f);
                var labelPos = new PointF(centre.X + offset, centre.Y - offset - Font.Height);
                newLabelRects.Add((new RectangleF(labelPos, g.MeasureString(p.Feature.Name, labelFont)), index));
                g.DrawString(p.Feature.Name, labelFont, labelBrush, labelPos);
            }
        }

        _screenLines = newScreenLines;
        _keyPoints = newKeyPoints;
        _labelRects = newLabelRects;

        if (_snappedPoint is { } snapped)
        {
            var snapScreen = ToScreen(snapped.U, snapped.V);
            const float snapRadius = 5f;
            using var snapBrush = new SolidBrush(Color.FromArgb(60, 200, 60));
            using var snapPen = new Pen(Color.FromArgb(0, 130, 0), 1.5f);
            g.FillEllipse(snapBrush, snapScreen.X - snapRadius, snapScreen.Y - snapRadius, snapRadius * 2, snapRadius * 2);
            g.DrawEllipse(snapPen, snapScreen.X - snapRadius, snapScreen.Y - snapRadius, snapRadius * 2, snapRadius * 2);

            // For a line feature, also mark its other endpoint - smaller than the primary
            // dot above - so both ends of the highlighted line read as picked, not just the
            // one nearer the cursor that _snappedPoint itself tracks.
            if (_features[snapped.FeatureIndex].IsLine)
            {
                foreach (var keyPoint in _keyPoints)
                {
                    if (keyPoint.FeatureIndex != snapped.FeatureIndex || (keyPoint.U == snapped.U && keyPoint.V == snapped.V))
                        continue;

                    const float farRadius = 3f;
                    g.FillEllipse(snapBrush, keyPoint.Screen.X - farRadius, keyPoint.Screen.Y - farRadius, farRadius * 2, farRadius * 2);
                    g.DrawEllipse(snapPen, keyPoint.Screen.X - farRadius, keyPoint.Screen.Y - farRadius, farRadius * 2, farRadius * 2);
                    break;
                }
            }
        }

        using var footerBrush = new SolidBrush(Color.DimGray);
        var planeSource = _planeOverridden ? "manual" : "auto";
        var footer = $"{_features.Count} features | Plane {_drawingPlane} ({planeSource}, ↑↓←→ to change) | {uLabel} {minX - originU:0.###} to {maxX - originU:0.###} | {vLabel} {minY - originV:0.###} to {maxY - originV:0.###}";
        if (_alignModeActive)
            footer += " | Align mode: click a line to level it (Esc to cancel)";
        if (_originPickModeActive)
            footer += " | Set origin: click a key point (Esc to cancel)";

        if (_snappedPoint is { } snap)
        {
            footer += $" | Snapped to {snap.Name}: {uLabel}={snap.U - originU:0.###} {vLabel}={snap.V - originV:0.###}";

            // Length/angle (for a line) or diameter (for a circle) of whichever feature is
            // currently snapped - measured from the same projected U/V the preview actually
            // draws, so it matches what's on screen rather than the feature's raw 3D data.
            var snappedFeature = _features[snap.FeatureIndex];
            if (snappedFeature.IsLine)
            {
                var line = projected[snap.FeatureIndex];
                var du = line.U2!.Value - line.U;
                var dv = line.V2!.Value - line.V;
                var length = Math.Sqrt(du * du + dv * dv);
                var angle = Math.Atan2(dv, du) * 180.0 / Math.PI;
                footer += $" | Length={length:0.###} Angle={angle:0.##}°";
            }
            else
            {
                footer += $" | Diameter={snappedFeature.Diameter:0.###}";
            }
        }
        else if (_mouseWorldPoint is { } mouse)
            footer += $" | {uLabel}={mouse.U - originU:0.###} {vLabel}={mouse.V - originV:0.###}";

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

    // Re-projects the tracked origin feature/point (if any) under the current _drawingPlane
    // and current _features state - called fresh every OnPaint, including every animation
    // tick, so the axes visibly follow that point through a rotate/mirror/align instead of
    // staying pinned to wherever it was when picked.
    private (double U, double V) GetOriginUV()
    {
        if (_originKeyPoint is not { } key || key.FeatureIndex >= _features.Count)
            return (0.0, 0.0);

        var feature = _features[key.FeatureIndex];
        if (key.IsSecondPoint && feature.IsLine)
        {
            var (u, v, _) = DrawingPlaneMapper.Project(feature.X2!.Value, feature.Y2!.Value, feature.Z2 ?? 0.0, _drawingPlane);
            return (u, v);
        }

        var (u2, v2, _) = DrawingPlaneMapper.Project(feature, _drawingPlane);
        return (u2, v2);
    }

    private void DrawAxes(Graphics g, Func<double, double, PointF> toScreen, Pen axisPen, string uLabel, string vLabel, double originU, double originV)
    {
        var x0a = toScreen(originU - 1_000_000, originV);
        var x0b = toScreen(originU + 1_000_000, originV);
        var y0a = toScreen(originU, originV - 1_000_000);
        var y0b = toScreen(originU, originV + 1_000_000);

        g.DrawLine(axisPen, x0a, x0b);
        g.DrawLine(axisPen, y0a, y0b);

        // Labelled where each axis line actually meets the panel edge it runs alongside -
        // the horizontal (U) line meets the right edge, the vertical (V) line meets the top
        // - offset a few pixels so the text sits just clear of the line rather than on it.
        using var brush = new SolidBrush(Color.Gray);
        var uLabelSize = g.MeasureString(uLabel, Font);
        g.DrawString(uLabel, Font, brush, Width - uLabelSize.Width - 6, x0b.Y - uLabelSize.Height - 4);
        g.DrawString(vLabel, Font, brush, y0b.X + 6, 4);
    }
}
