using System;
using System.Windows;
using System.Windows.Media;

namespace GearPower
{
    public sealed class BatteryRing : FrameworkElement
    {
        private int? percent;
        private Color accent = Theme.MutedColor;
        private bool charging;
        private bool asleep;
        private bool stale;

        public bool Stale
        {
            get { return stale; }
            set { stale = value; InvalidateVisual(); }
        }

        public int? Percent
        {
            get { return percent; }
            set { percent = value; InvalidateVisual(); }
        }

        public Color Accent
        {
            get { return accent; }
            set { accent = value; InvalidateVisual(); }
        }

        public bool Charging
        {
            get { return charging; }
            set { charging = value; InvalidateVisual(); }
        }

        public bool Asleep
        {
            get { return asleep; }
            set { asleep = value; InvalidateVisual(); }
        }

        protected override void OnRender(DrawingContext dc)
        {
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 4) return;
            double radius = size / 2 - 6;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            double thickness = 7;

            var trackPen = new Pen(Theme.Track, thickness);
            trackPen.Freeze();
            dc.DrawEllipse(null, trackPen, center, radius, radius);

            if (asleep && !percent.HasValue)
            {
                var dashPen = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x4A, 0x5E)), 2);
                dashPen.DashStyle = new DashStyle(new double[] { 2, 4 }, 0);
                dashPen.Freeze();
                dc.DrawEllipse(null, dashPen, center, radius - 1, radius - 1);
                return;
            }

            if (!percent.HasValue) return;

            int value = Math.Max(0, Math.Min(100, percent.Value));
            Color drawColor = stale ? Color.FromArgb(118, accent.R, accent.G, accent.B) : accent;
            var accentBrush = new SolidColorBrush(drawColor);
            accentBrush.Freeze();
            var pen = new Pen(accentBrush, thickness);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.Freeze();

            if (stale)
            {
                var dashed = new Pen(new SolidColorBrush(Color.FromArgb(110, accent.R, accent.G, accent.B)), 1.4);
                dashed.DashStyle = new DashStyle(new double[] { 2, 3 }, 0);
                dashed.Freeze();
                dc.DrawEllipse(null, dashed, center, radius + 3, radius + 3);
            }

            if (charging && !stale)
            {
                var glow = new Pen(new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B)), thickness + 9);
                glow.Freeze();
                dc.DrawEllipse(null, glow, center, radius, radius);
            }

            if (value >= 100)
            {
                dc.DrawEllipse(null, pen, center, radius, radius);
            }
            else if (value > 0)
            {
                double sweep = 360.0 * value / 100.0;
                double startAngle = -90;
                double endAngle = startAngle + sweep;
                Point start = PointOnCircle(center, radius, startAngle);
                Point end = PointOnCircle(center, radius, endAngle);

                var geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(start, false, false);
                    ctx.ArcTo(end, new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        private static Point PointOnCircle(Point center, double radius, double angleDegrees)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }
    }
}
