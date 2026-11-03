using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GamesDock
{
    public sealed partial class MaterialArt
    {
        public static readonly DependencyProperty AmbientPhaseProperty = MotionProperty("AmbientPhase", 0);
        public static readonly DependencyProperty InteractionLevelProperty = MotionProperty("InteractionLevel", 0);
        public static readonly DependencyProperty EventPhaseProperty = MotionProperty("EventPhase", -1);
        static readonly Brush Radar = Themes.Br("#FF63C58E"), Ember = Themes.Br("#FFF3A04A");
        static readonly Brush MassField = Themes.Br("#FF70BFF4"), Phosphor = Themes.Br("#FF78DCAD");
        static readonly Brush Head = Themes.Br("#FFE0FFE8");
        bool _interacting, _launchEvent, _motionEnabled;
        double _eventPosition;
        int _interactionGeneration, _eventGeneration;

        // Previews and settings thumbnails stay static unless explicitly enabled by the dock.
        public bool MotionEnabled
        {
            get { return _motionEnabled; }
            set
            {
                if (_motionEnabled == value) return;
                _motionEnabled = value;
                if (!value) StopMotion();
                else if (CanAnimate) TriggerEvent(_length / 2, false);
            }
        }
        public double AmbientPhase { get { return (double)GetValue(AmbientPhaseProperty); } set { SetValue(AmbientPhaseProperty, value); } }
        public double InteractionLevel { get { return (double)GetValue(InteractionLevelProperty); } set { SetValue(InteractionLevelProperty, value); } }
        public double EventPhase { get { return (double)GetValue(EventPhaseProperty); } set { SetValue(EventPhaseProperty, value); } }

        public MaterialArt()
        {
            Overlay = new MaterialFocus(this) { IsHitTestVisible = false };
            Loaded += delegate { if (CanAnimate && EventPhase < 0) TriggerEvent(_length / 2, false); };
            Unloaded += delegate { StopMotion(); };
            IsVisibleChanged += delegate { if (!IsVisible) StopMotion(); };
        }

        static DependencyProperty MotionProperty(string name, double value)
        {
            return DependencyProperty.Register(name, typeof(double), typeof(MaterialArt),
                new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender,
                    (owner, args) => { var art = (MaterialArt)owner; if (art.Overlay != null) art.Overlay.InvalidateVisual(); }));
        }

        bool CanAnimate
        {
            get { return MotionEnabled && IsVisible && _theme != null && _theme.Material != null; }
        }

        static DoubleAnimation Clock(double from, double to, double seconds)
        {
            var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds)) { FillBehavior = FillBehavior.Stop };
            Timeline.SetDesiredFrameRate(animation, 60);
            return animation;
        }

        void ResetMotion(bool reveal)
        {
            bool resume = _interacting;
            StopMotion();
            if (reveal && CanAnimate) TriggerEvent(_length / 2, false);
            if (resume) SetInteraction(true);
        }

        public void StopMotion()
        {
            _interacting = false;
            _interactionGeneration++; _eventGeneration++;
            _focusGeneration++;
            BeginAnimation(AmbientPhaseProperty, null);
            BeginAnimation(InteractionLevelProperty, null);
            BeginAnimation(EventPhaseProperty, null);
            BeginAnimation(FocusTransitionProperty, null);
            AmbientPhase = 0; InteractionLevel = 0; EventPhase = -1;
            FocusTransition = 1;
        }

        public void SetInteraction(bool active)
        {
            if (active && !CanAnimate) { StopMotion(); return; }
            if (_interacting == active) return;
            _interacting = active;
            int generation = ++_interactionGeneration;
            if (active)
            {
                var loop = Clock(0, 1, ThemeMotionProfile.For(_theme.Material).Cycle);
                loop.RepeatBehavior = RepeatBehavior.Forever;
                BeginAnimation(AmbientPhaseProperty, loop, HandoffBehavior.SnapshotAndReplace);
                var fade = Clock(InteractionLevel, 1, 0.18);
                InteractionLevel = 1;
                BeginAnimation(InteractionLevelProperty, fade, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                var fade = Clock(InteractionLevel, 0, 0.24);
                InteractionLevel = 0;
                fade.Completed += delegate
                {
                    if (generation != _interactionGeneration || _interacting) return;
                    BeginAnimation(AmbientPhaseProperty, null);
                    BeginAnimation(InteractionLevelProperty, null);
                    AmbientPhase = 0;
                };
                BeginAnimation(InteractionLevelProperty, fade, HandoffBehavior.SnapshotAndReplace);
            }
        }

        public void Launch(double position)
        {
            if (CanAnimate) TriggerEvent(position, true);
        }

        void TriggerEvent(double position, bool launch)
        {
            _eventPosition = position; _launchEvent = launch;
            int generation = ++_eventGeneration;
            double seconds = launch ? ThemeMotionProfile.For(_theme.Material).LaunchTime : 1.85;
            var sequence = Clock(0, 1, seconds);
            sequence.Completed += delegate
            {
                if (generation != _eventGeneration) return;
                BeginAnimation(EventPhaseProperty, null);
                EventPhase = -1;
            };
            BeginAnimation(EventPhaseProperty, sequence, HandoffBehavior.SnapshotAndReplace);
        }

        static double Unit(double n) { return n - Math.Floor(n); }
        static double Clamp(double n) { return Math.Max(0, Math.Min(1, n)); }
        static double Envelope(double progress) { return Clamp(progress * 9) * Clamp((1 - progress) * 4); }
        static double Between(double a, double b, double amount) { return a + (b - a) * amount; }

        void DrawSagaMotion(DrawingContext dc, double start, double end, double top, double bottom)
        {
            double eventProgress = EventPhase;
            double activity = Clamp(InteractionLevel);
            if (activity < 0.001 && eventProgress < 0) return;
            double phase = eventProgress >= 0 ? Clamp(eventProgress) : AmbientPhase;
            double energy = eventProgress >= 0 ? Envelope(phase) * (_launchEvent ? 1 : 0.75) : activity * 0.48;
            if (energy < 0.001) return;
            double position = _marker >= 0 ? _marker : _length / 2;
            if (eventProgress >= 0) position = _eventPosition;
            position = Math.Max(start, Math.Min(end, position));
            dc.PushOpacity(energy);
            dc.PushOpacity(.32);
            dc.DrawEllipse(_focusGlow, null, new Point(position, _center), _half * 1.8, _half * 1.1);
            dc.Pop();
            switch (_theme.Material)
            {
                case "industrial": DrawTracker(dc, start, end, top, bottom, position, phase, eventProgress >= 0); break;
                case "shrine": DrawBonfire(dc, start, end, top, bottom, position, phase); break;
                case "portal": DrawPortals(dc, start, end, top, bottom, position, phase, eventProgress >= 0); break;
                case "memory": DrawMemory(dc, start, end, top, bottom, phase); break;
                case "orbit": DrawMassField(dc, start, end, top, bottom, position, phase, eventProgress >= 0); break;
                case "terminal": DrawCode(dc, start, end, top, bottom, position, phase, eventProgress >= 0); break;
            }
            dc.Pop();
        }

        // Alien: a motion-tracker sweep, phosphor persistence and sequenced airlock lamps.
        void DrawTracker(DrawingContext dc, double start, double end, double top, double bottom,
                         double position, double phase, bool triggered)
        {
            double scan = Between(start, end, phase);
            dc.PushOpacity(Envelope(phase));
            for (int i = 0; i < 9; i++)
            {
                double x = scan - i * 5;
                if (x < start) continue;
                dc.PushOpacity((1 - i / 9.0) * 0.7);
                Line(dc, Stroke(Radar, i == 0 ? 2 : 1), x, bottom - 8, x, bottom - 2);
                dc.Pop();
            }
            dc.Pop();
            for (int i = 0; i < 5; i++)
            {
                dc.PushOpacity(i <= Math.Floor(phase * 5) ? 0.9 : 0.16);
                dc.DrawRectangle(_accent, null, new Rect(start + i * 9, top + 2, 6, 2)); dc.Pop();
            }
            double ping = 3 + Unit(phase * 2) * 13;
            dc.PushOpacity(0.7 * (1 - Unit(phase * 2)));
            dc.DrawEllipse(null, Stroke(Radar, 1), new Point(position, bottom - 5), ping, ping * 0.6);
            dc.Pop();
            if (triggered)
            {
                dc.PushOpacity(0.09);
                dc.DrawRectangle(Radar, null, new Rect(scan - 2, top, 4, bottom - top));
                dc.Pop();
            }
        }

        // Dark Souls: embers rise from a kindled bonfire; a broken seal grows around the chosen slot.
        void DrawBonfire(DrawingContext dc, double start, double end, double top, double bottom,
                         double position, double phase)
        {
            double heat = 0.7 + 0.3 * Math.Sin(phase * Math.PI * 6);
            Line(dc, Stroke(Ember, 1.5), position - 12 * heat, bottom - 2.5, position + 12 * heat, bottom - 2.5);
            for (int i = 0; i < 11; i++)
            {
                double age = Unit(phase * 2 + i * 0.137);
                double x = position + Math.Sin(i * 2.4 + age * 3) * (10 + age * 23);
                double y = bottom - 3 - age * (bottom - top - 5);
                dc.PushOpacity((1 - age) * 0.8);
                dc.DrawEllipse(i % 3 == 0 ? _accent : Ember, null, new Point(x, y), 0.7, 1.1 + age);
                dc.Pop();
            }
            double radius = Math.Min(30, _half - 5);
            dc.PushOpacity(0.6);
            Arc(dc, Stroke(_accent, 1), position, _center, radius, radius,
                -Math.PI * 0.8, Math.PI * (0.6 + 0.4 * Math.Sin(phase * Math.PI)));
            dc.Pop();
        }

        // Portal: paired mouths exchange energy; launch opens a local aperture and transfers charge.
        void DrawPortals(DrawingContext dc, double start, double end, double top, double bottom,
                         double position, double phase, bool triggered)
        {
            double charge = 0.5 + 0.5 * Math.Sin(phase * Math.PI);
            for (int side = 0; side < 2; side++)
            {
                Brush color = side == 0 ? Blue : Orange;
                double x = side == 0 ? 8 : _length - 8;
                for (int ring = 0; ring < 3; ring++)
                {
                    dc.PushOpacity(0.65 - ring * 0.19);
                    dc.DrawEllipse(null, Stroke(color, 1), new Point(x, _center),
                        2 + ring * 1.8 + charge, _half * 0.48 + ring * 2); dc.Pop();
                }
            }
            for (int i = 0; i < 7; i++)
            {
                double travel = Unit(phase * 2 + i / 7.0);
                double x = Between(start, end, travel);
                double y = bottom - 4 - Math.Sin(travel * Math.PI) * 2;
                dc.PushOpacity(0.8 - i * 0.06);
                Line(dc, Stroke(travel < 0.5 ? Blue : Orange, 1.5), x - 3, y, x + 1, y); dc.Pop();
            }
            if (triggered)
            {
                double opening = Math.Sin(phase * Math.PI);
                dc.DrawEllipse(null, Stroke(phase < 0.5 ? Blue : Orange, 1.5),
                    new Point(position, _center), Math.Max(1, opening * (_half - 4)), _half - 3);
            }
        }

        // PlayStation memory hardware: discrete address packets, a write LED and the four boot glyphs.
        void DrawMemory(DrawingContext dc, double start, double end, double top, double bottom, double phase)
        {
            int step = (int)(phase * 28);
            double track = Math.Max(1, end - start - 42);
            for (int i = 0; i < 6; i++)
            {
                double x = start + Unit((step - i) / 28.0) * track;
                dc.PushOpacity(0.8 - i * 0.11);
                dc.DrawRectangle(_accent, null, new Rect(x, bottom - 7, 4, 3)); dc.Pop();
            }
            dc.PushOpacity(step % 3 == 0 ? 0.2 : 1);
            dc.DrawEllipse(_accent, null, new Point(9, _center - 10), 2, 2); dc.Pop();
            for (int i = 0; i < 4; i++)
            {
                dc.PushOpacity(0.3 + 0.7 * Clamp((phase * 4 - i) * 3));
                DrawBootGlyph(dc, i, end - 32 + i * 10, top + 5); dc.Pop();
            }
        }

        void DrawBootGlyph(DrawingContext dc, int glyph, double x, double y)
        {
            var pen = Stroke(_accent, 0.9);
            if (glyph == 0)
            {
                Line(dc, pen, x, y + 4, x + 2, y); Line(dc, pen, x + 2, y, x + 4, y + 4);
                Line(dc, pen, x + 4, y + 4, x, y + 4);
            }
            else if (glyph == 1) dc.DrawEllipse(null, pen, new Point(x + 2, y + 2), 2, 2);
            else if (glyph == 2)
            {
                Line(dc, pen, x, y, x + 4, y + 4); Line(dc, pen, x + 4, y, x, y + 4);
            }
            else dc.DrawRectangle(null, pen, new Rect(x, y, 4, 4));
        }

        // Mass-effect field: a slow orbital lens becomes an expanding launch wave around the slot.
        void DrawMassField(DrawingContext dc, double start, double end, double top, double bottom,
                           double position, double phase, bool triggered)
        {
            double satellite = Between(start, end, phase);
            double pathY = bottom - 5 + 3 * Math.Sin(phase * Math.PI);
            dc.PushOpacity(Envelope(phase));
            dc.DrawEllipse(MassField, null, new Point(satellite, pathY), 2, 1.3);
            for (int i = 0; i < 8; i++)
            {
                double x = satellite - i * 4;
                if (x < start) continue;
                dc.PushOpacity((1 - i / 8.0) * 0.35);
                Line(dc, Stroke(MassField, 1), x - 2, pathY, x + 1, pathY); dc.Pop();
            }
            dc.Pop();
            for (int ring = 0; ring < 3; ring++)
            {
                double expansion = Unit(phase - ring * 0.17);
                double rx = (_half - 4) + expansion * (triggered ? 105 : 26);
                double ry = _half - 4 + expansion * 8;
                dc.PushOpacity((1 - expansion) * 0.6);
                Arc(dc, Stroke(MassField, 1), position, _center, rx, ry, Math.PI * 0.08, Math.PI * 0.84);
                Arc(dc, Stroke(_accent, 0.8), position, _center, rx, ry, Math.PI * 1.08, Math.PI * 0.84);
                dc.Pop();
            }
        }

        // Matrix: deterministic phosphor columns with bright heads and a stepped decode rail.
        void DrawCode(DrawingContext dc, double start, double end, double top, double bottom,
                      double position, double phase, bool triggered)
        {
            int columns = Math.Max(3, Math.Min(14, (int)(_length / 52)));
            for (int column = 0; column < columns; column++)
            {
                double x = Between(start, end, (column + 0.5) / columns);
                double fall = Unit(phase * (column % 2 == 0 ? 1.0 : 2.0) + column * 0.271);
                double headY = top + fall * (bottom - top + 25);
                for (int digit = 0; digit < 5; digit++)
                {
                    double y = headY - digit * 6;
                    if (y < top + 2 || y > bottom - 6) continue;
                    dc.PushOpacity((digit == 0 ? 0.7 : 0.23 * (1 - digit / 5.0)) * (triggered ? 1 : 0.65));
                    Brush color = digit == 0 ? Head : Phosphor;
                    var pen = Stroke(color, 0.8);
                    if ((column + digit + (int)(phase * 16)) % 3 == 0)
                        dc.DrawRectangle(null, pen, new Rect(x, y, 2.5, 4));
                    else Line(dc, pen, x + 1, y, x + 1, y + 4);
                    dc.Pop();
                }
            }
            int count = triggered ? 10 : 5;
            for (int i = 0; i < count; i++)
            {
                dc.PushOpacity(i / (double)count < Unit(phase * 2) ? 0.85 : 0.16);
                dc.DrawRectangle(Phosphor, null, new Rect(position - count * 3 + i * 6, bottom - 5, 3, 2)); dc.Pop();
            }
        }

        static void Arc(DrawingContext dc, Pen pen, double x, double y, double rx, double ry,
                        double startAngle, double sweep)
        {
            if (rx <= 0 || ry <= 0) return;
            var path = new StreamGeometry();
            using (var ctx = path.Open())
            {
                ctx.BeginFigure(new Point(x + rx * Math.Cos(startAngle), y + ry * Math.Sin(startAngle)), false, false);
                ctx.ArcTo(new Point(x + rx * Math.Cos(startAngle + sweep), y + ry * Math.Sin(startAngle + sweep)),
                    new Size(rx, ry), 0, sweep > Math.PI, SweepDirection.Clockwise, true, false);
            }
            path.Freeze(); dc.DrawGeometry(null, pen, path);
        }
    }
}
