using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GamesDock
{
    // Persistent sample controls and shared spring integration; no per-mouse-event animation clocks.
    public sealed class DockPreview : Border
    {
        static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(DockPreview),
            new PropertyMetadata(0.0, (o, e) => ((DockPreview)o).RequestSceneUpdate()));
        static readonly DependencyProperty PointerProperty = DependencyProperty.Register("Pointer", typeof(double), typeof(DockPreview),
            new PropertyMetadata(0.0, (o, e) => ((DockPreview)o).RequestSceneUpdate()));
        static readonly DependencyProperty FrameTickProperty = DependencyProperty.Register("FrameTick", typeof(double), typeof(DockPreview),
            new PropertyMetadata(0.0, (o, e) => ((DockPreview)o).Frame()));
        static readonly ControlTemplate TileTemplate = new ControlTemplate(typeof(Button))
        { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        readonly Dock _dock;
        readonly Canvas _scene;
        readonly System.Windows.Shapes.Path _surface, _edge;
        readonly MaterialArt _material;
        readonly List<Button> _tiles = new List<Button>();
        readonly Border[] _art = new Border[5];
        readonly TextBlock[] _letters = new TextBlock[5];
        readonly Brush[] _artwork = new Brush[5];
        readonly string[] _keys = new string[5];
        readonly Spring _pointerSpring = Spring.Make("ptr", 3200, 1, null);
        readonly Spring _growSpring = Spring.Make("preview-grow", 500, 1, null);
        readonly Stopwatch _clock = new Stopwatch();
        ThemeDef _theme;
        Brush _badgeBrush, _letterBrush;
        Geometry _geometry;
        string _shape;
        bool _inside, _subscribed, _frameActive, _batch, _sceneDirty;
        double _size, _pitch, _length, _lastTime;
        double _geoCenter, _geoHalf, _geoUniform, _geoAmplitude, _geoPointer;
        public double Progress { get { return (double)GetValue(ProgressProperty); } set { SetValue(ProgressProperty, value); } }
        public double Pointer { get { return (double)GetValue(PointerProperty); } set { SetValue(PointerProperty, value); } }

        public DockPreview(Dock dock)
        {
            _dock = dock; Height = 104; CornerRadius = new CornerRadius(9); Background = Themes.Br("#FF11171B");
            _scene = new Canvas { Height = 104, HorizontalAlignment = HorizontalAlignment.Center, Background = Brushes.Transparent };
            Child = _scene;
            _surface = new System.Windows.Shapes.Path(); _edge = new System.Windows.Shapes.Path { StrokeThickness = 1 };
            _material = new MaterialArt { IsHitTestVisible = false };
            _scene.Children.Add(_surface); _scene.Children.Add(_edge); _scene.Children.Add(_material);
            for (int i = 0; i < 5; i++)
            {
                int selected = i;
                _letters[i] = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                _art[i] = new Border();
                RenderOptions.SetBitmapScalingMode(_art[i], BitmapScalingMode.Fant);
                var tile = new Button { Content = _art[i], Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Padding = new Thickness(0), Cursor = Cursors.Hand, Template = TileTemplate,
                    RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
                System.Windows.Automation.AutomationProperties.SetName(tile, Lang.T("Probar animación"));
                tile.Click += delegate
                {
                    if (!_dock.Cfg.Motion) return;
                    SagaTileMotion.Launch(_tiles[selected], Themes.Get(_dock.Cfg.Theme).Material, false);
                    _material.Launch(14 + selected * _pitch + _size / 2);
                };
                _tiles.Add(tile); _scene.Children.Add(tile);
            }
            _scene.Children.Add(_material.Overlay);
            Loaded += delegate
            {
                if (!_subscribed) { _dock.AppearanceChanged += Refresh; _subscribed = true; }
                Refresh();
            };
            Unloaded += delegate
            {
                if (_subscribed) { _dock.AppearanceChanged -= Refresh; _subscribed = false; }
                _inside = false; StopFrames(); _material.StopMotion();
            };
            MouseEnter += delegate
            {
                _inside = true;
                _pointerSpring.X = _pointerSpring.Target = Mouse.GetPosition(_scene).X; _pointerSpring.V = 0;
                Pointer = _pointerSpring.X; AnimateProgress(1); _material.SetInteraction(true);
            };
            MouseMove += (s, e) => AnimatePointer(e.GetPosition(_scene).X);
            MouseLeave += delegate { _inside = false; AnimateProgress(0); _material.SetInteraction(false); };
            Refresh();
        }

        public void Refresh()
        {
            var cfg = _dock.Cfg; var theme = Themes.Get(cfg.Theme);
            double size = Math.Min(48, Math.Max(28, cfg.IconSize));
            bool changed = _theme != theme, resized = size != _size, reshaped = _shape != cfg.IconShape;
            _size = size; _pitch = _size + 14; _length = _pitch * 5 + 14; _scene.Width = _length;
            if (changed)
            {
                _theme = theme; _geometry = null;
                _badgeBrush = Themes.Br(theme.C["badge"]); _letterBrush = Themes.Br(theme.C["letter"]);
                _surface.Fill = Themes.Grad(theme.C["glass"], theme.C["glassBot"]);
                _edge.Stroke = Themes.Br(theme.C["edge"]);
                _edge.Fill = Themes.Grad(theme.C["sheenTop"], theme.C["sheenMid"], theme.C["sheenBot"]);
                var profile = ThemeMotionProfile.For(theme.Material);
                profile.Tune(_pointerSpring, profile.PointerK, 1);
                profile.Tune(_growSpring, profile.ShellK, 1);
            }
            _shape = cfg.IconShape;
            for (int i = 0; i < 5; i++)
            {
                var sourceTile = _dock.Tiles.Count == 0 ? null : _dock.Tiles[i % _dock.Tiles.Count];
                var source = sourceTile == null ? null : sourceTile.Content as Border;
                var spec = sourceTile == null ? null : sourceTile.Tag as ItemSpec;
                string key = spec == null ? null : spec.Key;
                if (_keys[i] != key) { _keys[i] = key; _artwork[i] = null; }
                if (source != null && source.Background is ImageBrush) _artwork[i] = source.Background;
                else if (source != null && source.Child != null && !(source.Child is TextBlock))
                {
                    var existing = _artwork[i] as VisualBrush;
                    if (existing == null || existing.Visual != source) _artwork[i] = new VisualBrush(source);
                }
                var art = _art[i]; var letter = _letters[i];
                if (changed || resized || reshaped || !cfg.Motion) SagaTileMotion.Stop(_tiles[i]);
                art.Width = art.Height = _tiles[i].Width = _tiles[i].Height = _size;
                art.CornerRadius = new CornerRadius(_dock.Lay.TileRadius(_size));
                if (_artwork[i] != null) { art.Background = _artwork[i]; art.Child = null; }
                else
                {
                    art.Background = _badgeBrush;
                    letter.Text = source != null && source.Child is TextBlock ? ((TextBlock)source.Child).Text : (i + 1).ToString();
                    letter.FontSize = _size * .38;
                    if (changed) letter.Foreground = _letterBrush;
                    art.Child = letter;
                }
                Canvas.SetLeft(_tiles[i], 14 + i * _pitch);
            }
            if (resized) _geometry = null;
            if (!cfg.Magnify)
            {
                _growSpring.X = _growSpring.Target = _growSpring.V = 0; Progress = 0;
            }
            _material.MotionEnabled = cfg.Motion;
            UpdateScene();
            if (_inside) _material.SetInteraction(true);
        }

        void AnimateProgress(double target)
        {
            _growSpring.Target = _dock.Cfg.Magnify ? target : 0;
            StartFrames();
        }

        void AnimatePointer(double target)
        {
            if (_pointerSpring.Target == target) return;
            _pointerSpring.Target = target; StartFrames();
        }

        void StartFrames()
        {
            if (_frameActive) return;
            _frameActive = true; _lastTime = 0; _clock.Restart();
            var tick = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(tick, 60);
            BeginAnimation(FrameTickProperty, tick, HandoffBehavior.SnapshotAndReplace);
        }

        void StopFrames()
        {
            if (!_frameActive) return;
            _frameActive = false; _clock.Stop(); BeginAnimation(FrameTickProperty, null);
        }

        void Frame()
        {
            if (!_frameActive) return;
            double now = _clock.Elapsed.TotalSeconds, dt = now - _lastTime;
            if (dt < .002) return;
            _lastTime = now; dt = Math.Min(dt, .0333);
            int count = Math.Max(1, Math.Min(8, (int)Math.Ceiling(dt / .004)));
            bool moving = false;
            for (int i = 0; i < count; i++)
            {
                moving |= Anim.Advance(_pointerSpring, dt / count);
                moving |= Anim.Advance(_growSpring, dt / count);
            }
            _batch = true;
            Pointer = _pointerSpring.X; Progress = _growSpring.X;
            _batch = false;
            if (_sceneDirty) { _sceneDirty = false; UpdateScene(); }
            if (!moving) StopFrames();
        }

        void RequestSceneUpdate()
        {
            if (_batch) { _sceneDirty = true; return; }
            UpdateScene();
        }

        void UpdateScene()
        {
            if (_material == null || _tiles.Count == 0 || _theme == null) return;
            var cfg = _dock.Cfg; var theme = _theme; var profile = ThemeMotionProfile.For(theme.Material);
            double level = cfg.Magnify ? Math.Max(0, Math.Min(1, Progress)) : 0;
            double half = _size / 2 + 6;
            double delta = (_size * profile.Peak / 2 + Math.Min(1, _size / 64) * profile.Lift + 8 - half) * level;
            double uniform = delta * (1 - profile.Locality), amplitude = delta * profile.Locality;
            double center = 52 - uniform, contourPointer = amplitude > .001 ? Pointer : 0;
            if (_geometry == null || _geoCenter != center || _geoHalf != half || _geoUniform != uniform ||
                _geoAmplitude != amplitude || _geoPointer != contourPointer)
            {
                _geometry = PillGeometry.Build(_length, center, half, uniform, amplitude, Pointer, _pitch,
                    profile.Peak, false, 0, 0, theme.CornerRatio, profile.Radius);
                _geoCenter = center; _geoHalf = half; _geoUniform = uniform; _geoAmplitude = amplitude; _geoPointer = contourPointer;
                _surface.Data = _edge.Data = _geometry;
            }
            _material.Update(theme, _geometry, _length, center, half + uniform, false, _inside ? Pointer : -1);
            int nearest = 0; double distance = double.MaxValue;
            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i]; double x = 14 + i * _pitch, tileCenter = x + _size / 2;
                double weight = profile.Influence(Pointer - tileCenter, _pitch * profile.Radius) * level;
                double scale = 1 + (profile.Peak - 1) * weight;
                ((ScaleTransform)tile.RenderTransform).ScaleX = scale; ((ScaleTransform)tile.RenderTransform).ScaleY = scale;
                if (Canvas.GetTop(tile) != center - _size / 2) Canvas.SetTop(tile, center - _size / 2);
                SagaTileMotion.Hover(tile, theme.Material, cfg.Motion ? weight : 0, (Pointer - tileCenter) / _pitch, false, 1, _size);
                double current = Math.Abs(Pointer - tileCenter);
                if (current < distance) { distance = current; nearest = i; }
            }
            if (_inside)
            {
                double x = 14 + nearest * _pitch;
                double weight = profile.Influence(Pointer - x - _size / 2, _pitch * profile.Radius) * level;
                double scale = 1 + (profile.Peak - 1) * weight;
                double lift = cfg.Motion ? profile.Lift * Math.Min(1, _size / 64) * weight : 0;
                _material.SetFocus(new Rect(x + _size * (1 - scale) / 2, center - _size * scale / 2 - lift,
                    _size * scale, _size * scale), Math.Min(_size / 2, _dock.Lay.TileRadius(_size)) * scale, weight);
            }
            else _material.SetFocus(Rect.Empty, 0, 0);
        }
    }
}
