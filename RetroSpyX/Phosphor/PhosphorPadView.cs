// RetroSpy, Phosphor edition: draws a controller the way Phosphor's controller test does, lit by its input.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).
//
// The drawing (which shapes, where, their names) comes from the SuperStation (PhosphorClient): circles, pills,
// triggers that fill, sticks whose knob moves, a D-pad whose arms light, an arcade lever. Pressed is amber,
// Phosphor's colour; a control the controller doesn't have is dimmed.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RetroSpy.Readers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RetroSpy.Phosphor
{
    public sealed class PhosphorPadView : Control
    {
        public static readonly IBrush Panel = new SolidColorBrush(Color.Parse("#1f1f23"));
        public static readonly IBrush PanelOn = new SolidColorBrush(Color.Parse("#2c2618"));
        public static readonly IBrush Line = new SolidColorBrush(Color.Parse("#4a4a50"));
        public static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#f0b54a"));
        public static readonly IBrush Text = new SolidColorBrush(Color.Parse("#ececec"));
        public static readonly IBrush Faint = new SolidColorBrush(Color.Parse("#5a5a62"));
        public static readonly IBrush OnText = new SolidColorBrush(Color.Parse("#1a1408"));
        private static readonly Typeface Font = new("Segoe UI, Inter, Arial", FontStyle.Normal, FontWeight.SemiBold);

        private PhosphorLayout? _layout;
        private PhosphorController? _ctrl;
        private ControllerStateEventArgs _state = ControllerStateEventArgs.Zero;

        /// <summary>Show the control's name on every element (on the layout's own drawing only the face buttons have one).</summary>
        public bool AllLabels { get; set; } = true;

        public void SetController(PhosphorLayout? layout, PhosphorController? ctrl)
        {
            _layout = layout;
            _ctrl = ctrl;
            InvalidateVisual();
        }

        public void SetState(ControllerStateEventArgs e)
        {
            _state = e;
            InvalidateVisual();
        }

        // ---------------------------------------------------------------- input, by control id

        private bool Has(string id) => _ctrl == null || id.Split('|').Any(i => _ctrl.Binds.ContainsKey(i));

        private float Axis(PhosphorBind b)
        {
            if (b.Axis < 0) return 0;
            float v = _state.Analogs.TryGetValue("a" + b.Axis.ToString(CultureInfo.InvariantCulture), out float f) ? f : 0;
            return b.Inverted ? -v : v;
        }

        /// <summary>0..1: how far one control is pressed.</summary>
        private float Value(string id)
        {
            if (_ctrl == null || !_ctrl.Binds.TryGetValue(id, out var b)) return 0;
            if (b.Button >= 0) return _state.Buttons.TryGetValue("b" + b.Button.ToString(CultureInfo.InvariantCulture), out bool on) && on ? 1 : 0;
            float v = Axis(b);
            if (b.Half > 0) return Math.Max(0, v);
            if (b.Half < 0) return Math.Max(0, -v);
            if (id == "l2" || id == "r2") return (v + 1) / 2;     // a trigger on its own axis: -1 released, 1 pressed
            return Math.Abs(v);
        }

        private bool On(string ids) => ids.Split('|').Any(id => Value(id) > (id == "l2" || id == "r2" ? 0.3f : 0.5f));

        private (float x, float y) Stick(bool right)
        {
            if (_ctrl == null) return (0, 0);
            float x = _ctrl.Binds.TryGetValue(right ? "rsx" : "lsx", out var bx) ? Axis(bx) : 0;
            float y = _ctrl.Binds.TryGetValue(right ? "rsy" : "lsy", out var by) ? Axis(by) : 0;
            return (x, y);
        }

        // ---------------------------------------------------------------- drawing

        public override void Render(DrawingContext ctx)
        {
            var L = _layout;
            if (L == null || Bounds.Width < 10 || Bounds.Height < 10) return;
            double s = Math.Min(Bounds.Width / L.Width, Bounds.Height / L.Height);
            double ox = (Bounds.Width - L.Width * s) / 2, oy = (Bounds.Height - L.Height * s) / 2;
            using var _ = ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(ox, oy));
            var thin = new Pen(Line, 2);
            foreach (var it in L.Items)
            {
                switch (it.T)
                {
                    case 'c': Circle(ctx, it, thin); break;
                    case 'r': Pill(ctx, it, thin); break;
                    case 't': Trigger(ctx, it, thin); break;
                    case 's': case 'l': StickEl(ctx, it, thin); break;
                    case 'd': Dpad(ctx, it, thin); break;
                }
            }
        }

        private void Element(DrawingContext ctx, Geometry g, bool has, bool on, Pen thin)
        {
            using var _ = ctx.PushOpacity(has ? 1 : 0.4);
            ctx.DrawGeometry(on ? Accent : Panel, on ? new Pen(Accent, 2) : thin, g);
        }

        private void Label(DrawingContext ctx, string text, double cx, double cy, double size, IBrush brush)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text == "−" ? "-" : text == "⌂" ? "Home" : text;
            var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush);
            ctx.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }

        private void Circle(DrawingContext ctx, PhosphorItem it, Pen thin)
        {
            bool rsDir = it.Id.Length == 3 && it.Id.StartsWith("rs", StringComparison.Ordinal);   // N64's C buttons
            bool has = rsDir ? Has("rsx") : Has(it.Id);
            bool on;
            if (rsDir)
            {
                var (x, y) = Stick(true);
                char d = it.Id[2];
                on = (d == 'u' && y < -.5f) || (d == 'd' && y > .5f) || (d == 'l' && x < -.5f) || (d == 'r' && x > .5f);
            }
            else on = On(it.Id);
            Element(ctx, new EllipseGeometry(new Rect(it.X - it.A, it.Y - it.A, 2 * it.A, 2 * it.A)), has, on, thin);
            var tc = on ? OnText : has ? Text : Faint;
            if (PsSymbol(ctx, it, tc)) return;
            bool small = it.Label.Length > 2;
            bool under = it.A < 14 || (small && it.Label.Length * 6 > it.A * 2 - 4);
            Label(ctx, it.Label, it.X, under ? it.Y + it.A + 12 : it.Y, small || under ? 11 : 15, under ? (has ? Text : Faint) : tc);
        }

        /// <summary>PlayStation's four symbols, drawn (as the TV does).</summary>
        private static bool PsSymbol(DrawingContext ctx, PhosphorItem it, IBrush tc)
        {
            int which = it.Label switch { "△" => 0, "○" => 1, "✕" => 2, "□" => 3, _ => -1 };
            if (which < 0) return false;
            double s = it.A * 9 / 20, cx = it.X, cy = it.Y;
            var pen = new Pen(tc, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            switch (which)
            {
                case 0:
                    var tri = new StreamGeometry();
                    using (var g = tri.Open())
                    {
                        g.BeginFigure(new Point(cx, cy - s * .87), false);
                        g.LineTo(new Point(cx + s, cy + s * .87));
                        g.LineTo(new Point(cx - s, cy + s * .87));
                        g.EndFigure(true);
                    }
                    ctx.DrawGeometry(null, pen, tri);
                    break;
                case 1: ctx.DrawEllipse(null, pen, new Point(cx, cy), s, s); break;
                case 2:
                    ctx.DrawLine(pen, new Point(cx - s, cy - s), new Point(cx + s, cy + s));
                    ctx.DrawLine(pen, new Point(cx + s, cy - s), new Point(cx - s, cy + s));
                    break;
                default: ctx.DrawRectangle(null, pen, new Rect(cx - s, cy - s, 2 * s, 2 * s), 2, 2); break;
            }
            return true;
        }

        private void Pill(DrawingContext ctx, PhosphorItem it, Pen thin)
        {
            bool has = Has(it.Id), on = On(it.Id);
            double r = Math.Min(it.B / 2, 9);
            Element(ctx, new RectangleGeometry(new Rect(it.X, it.Y, it.A, it.B), r, r), has, on, thin);
            Label(ctx, it.Label, it.X + it.A / 2, it.Y + it.B / 2, it.Label.Length > 2 || it.B < 18 ? 11 : 13, on ? OnText : has ? Text : Faint);
        }

        private void Trigger(DrawingContext ctx, PhosphorItem it, Pen thin)
        {
            bool has = Has(it.Id);
            float v = Value(it.Id);
            Element(ctx, new RectangleGeometry(new Rect(it.X, it.Y, it.A, it.B), 8, 8), has, false, thin);
            if (v > 0.02f)
            {
                double full = it.A - 6;
                ctx.DrawRectangle(Accent, null, new Rect(it.X + 3, it.Y + 3, Math.Max(4, full * Math.Min(1, v)), it.B - 6), 5, 5);
            }
            Label(ctx, it.Label, it.X + it.A / 2, it.Y + it.B / 2, 12, v > 0.55f ? OnText : has ? Text : Faint);
        }

        private void StickEl(DrawingContext ctx, PhosphorItem it, Pen thin)
        {
            bool lever = it.T == 'l', right = it.Id == "rs";
            float x, y;
            bool has;
            if (lever)
            {
                x = (On("right") ? 1 : 0) - (On("left") ? 1 : 0);
                y = (On("down") ? 1 : 0) - (On("up") ? 1 : 0);
                if (x == 0 && y == 0) (x, y) = Stick(false);
                has = Has("up") || Has("lsx");
            }
            else
            {
                (x, y) = Stick(right);
                has = Has(right ? "rsx" : "lsx");
            }
            bool on = x * x + y * y > 0.09f || (!lever && On(right ? "r3" : "l3"));
            using (ctx.PushOpacity(has ? 1 : 0.4))
            {
                ctx.DrawEllipse(Panel, thin, new Point(it.X, it.Y), it.A, it.A);
                double knob = lever ? it.A * .38 : it.A * .42, travel = lever ? it.A / 2 : it.A * .55;
                var c = new Point(it.X + x * travel, it.Y + y * travel);
                ctx.DrawEllipse(on ? Accent : Faint, null, c, knob, knob);
                if (!lever) Label(ctx, it.Label, c.X, c.Y, 11, on ? OnText : Text);
            }
        }

        private void Dpad(DrawingContext ctx, PhosphorItem it, Pen thin)
        {
            double w = it.A * 9 / 10, h = w / 2, ex = it.A + h, x = it.X, y = it.Y;
            bool has = Has("up") || Has("left");
            using (ctx.PushOpacity(has ? 1 : 0.4))
            {
                var g = new CombinedGeometry(GeometryCombineMode.Union,
                    new RectangleGeometry(new Rect(x - h, y - ex, w, 2 * ex), 4, 4),
                    new RectangleGeometry(new Rect(x - ex, y - h, 2 * ex, w), 4, 4));
                ctx.DrawGeometry(Panel, thin, g);
                var arms = new (string id, Rect r)[]
                {
                    ("up", new Rect(x - h + 3, y - ex + 3, w - 6, ex - h - 6)),
                    ("down", new Rect(x - h + 3, y + h + 3, w - 6, ex - h - 6)),
                    ("left", new Rect(x - ex + 3, y - h + 3, ex - h - 6, w - 6)),
                    ("right", new Rect(x + h + 3, y - h + 3, ex - h - 6, w - 6)),
                };
                foreach (var (id, r) in arms)
                {
                    if (On(id)) ctx.DrawRectangle(Accent, null, r, 3, 3);
                }
            }
        }
    }
}
