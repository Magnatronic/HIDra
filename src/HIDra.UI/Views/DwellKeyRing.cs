using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace HIDra.UI.Views;

/// <summary>
/// The keyboard's dwell countdown: a ring around the label of the highlighted key that
/// fills until the key is typed. It matches the ring shown by the cursor for a
/// dwell click, so both kinds of dwell look and behave the same.
///
/// Drawn as an adorner so it sits on top of the key without changing the key itself,
/// and is removed the moment the dwell finishes or is cancelled.
/// </summary>
public sealed class DwellKeyRing : Adorner
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(DwellKeyRing),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How full the ring is, from 0 to 1</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    // Drawn around the key's label rather than in a corner: the keys are too small for
    // a corner ring to miss the letter, and circling it makes plain which key is about
    // to be typed. Sized to the key, so it grows with the keyboard.
    private const double RadiusShareOfKey = 0.36;
    private const double Thickness = 5;

    private static readonly Pen TrackPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF))), Thickness));
    private static readonly Pen FillPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F))), Thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round
    });

    public DwellKeyRing(UIElement key) : base(key)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = AdornedElement.RenderSize;
        double radius = Math.Min(size.Width, size.Height) * RadiusShareOfKey;
        var centre = new Point(size.Width / 2, size.Height / 2);

        // Wide keys - the suggestions and the space bar - hold a word, which a ring in the
        // middle would cross. There it sits at the right-hand end instead.
        if (size.Width > size.Height * 1.6)
        {
            radius = size.Height * 0.28;
            centre = new Point(size.Width - radius - 12, size.Height / 2);
        }

        // Only the outline: the letter inside must stay readable
        dc.DrawEllipse(null, TrackPen, centre, radius, radius);

        double progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0)
        {
            return;
        }

        if (progress >= 0.999)
        {
            dc.DrawEllipse(null, FillPen, centre, radius, radius);
            return;
        }

        // Clockwise from twelve o'clock, as on the cursor's ring
        double angle = progress * 2 * Math.PI;
        var start = new Point(centre.X, centre.Y - radius);
        var end = new Point(centre.X + radius * Math.Sin(angle), centre.Y - radius * Math.Cos(angle));

        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0, isLargeArc: progress > 0.5,
                SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        arc.Freeze();

        dc.DrawGeometry(null, FillPen, arc);
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
