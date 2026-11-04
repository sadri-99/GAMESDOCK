using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GamesDock
{
    // Only the gallery scrolls. Search, game selection and the close button stay fixed.
    public partial class IconPicker
    {
        readonly Dock _dock;
        readonly GlassPanel _par;
        readonly ItemSpec _original;
        readonly string _title;
        readonly List<SgdIcono> _seed;
        readonly TextBox _query;
        readonly Button _search;
        readonly ComboBox _games;
        readonly CheckBox _light;
        readonly WrapPanel _rejilla;
        readonly ScrollViewer _scroll;
        readonly TextBlock _status, _footer, _emptyTitle, _emptyHint;
        readonly StackPanel _empty;
        readonly List<Ficha> _fichas = new List<Ficha>();
        CancellationTokenSource _request;
        int _revision;
        bool _closed, _started, _settingGames;
        string _elegida;

        sealed class Ficha
        {
            public RadioButton Btn;
            public Border Preview;
            public Image Image;
            public TextBlock Placeholder;
            public string Url, Thumb;
        }

        public static string Seleccionar(Dock dock, ItemSpec item)
        {
            if (dock == null || item == null) return null;
            var picker = new IconPicker(dock, item.Name, null, item);
            picker._par.ShowModal(); return picker._elegida;
        }

        public static string Seleccionar(Dock dock, string title, List<SgdIcono> icons)
        {
            if (dock == null) return null;
            var picker = new IconPicker(dock, title, icons, null);
            picker._par.ShowModal(); return picker._elegida;
        }

        IconPicker(Dock dock, string title, List<SgdIcono> seed, ItemSpec original)
        {
            _dock = dock; _title = title ?? string.Empty; _original = original; _seed = seed;
            _par = new GlassPanel(Lang.F("Elige un icono para {0}", _title), 800,
                Lang.T("Prueba otro nombre y elige el juego correcto."));
            _par.Win.SizeToContent = SizeToContent.Manual;
            _par.Win.Height = Math.Max(240, Math.Min(760, dock.MonitorWorkArea.Height - 28));
            _par.Tit.MaxWidth = Math.Max(140, _par.Win.Width - 140);
            _par.Tit.TextTrimming = TextTrimming.CharacterEllipsis; _par.Tit.ToolTip = _par.Tit.Text;

            var shell = (Border)_par.Win.Content;
            var previous = (ScrollViewer)shell.Child; previous.Content = null; shell.Child = null;
            var header = (FrameworkElement)_par.Body.Children[0]; _par.Body.Children.Remove(header);
            header.Margin = new Thickness(0, 0, 0, 12);
            var layout = new Grid { Margin = new Thickness(18, 15, 18, 14) };
            for (int i = 0; i < 6; i++) layout.RowDefinitions.Add(new RowDefinition { Height = i == 4 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            layout.Children.Add(header);

            var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _query = new TextBox { Style = Res.Style("GlassText"), Text = _title, Margin = new Thickness(0, 0, 8, 0), MaxLength = 160,
                ToolTip = Lang.T("Buscar por otro nombre en SteamGridDB") };
            AutomationProperties.SetName(_query, Lang.T("Buscar por otro nombre en SteamGridDB"));
            _search = new Button { Style = Res.Style("PillBtn"), Content = Lang.T("Buscar"), Padding = new Thickness(17, 7, 17, 7) };
            _search.Click += async delegate { await SearchAsync(false); };
            _query.KeyDown += async (sender, args) =>
            { if (args.Key == Key.Enter && !args.IsRepeat) { args.Handled = true; await SearchAsync(false); } };
            searchRow.Children.Add(_query); Grid.SetColumn(_search, 1); searchRow.Children.Add(_search);
            Grid.SetRow(searchRow, 1); layout.Children.Add(searchRow);

            var options = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _games = new ComboBox { Style = Res.Style("PickerGames"), Margin = new Thickness(0, 0, 12, 0), ClipToBounds = true };
            AutomationProperties.SetName(_games, Lang.T("Juego encontrado"));
            _games.SelectionChanged += async delegate
            {
                if (_settingGames) return;
                var choice = _games.SelectedItem as ComboBoxItem;
                var game = choice == null ? null : choice.Tag as SgdJuego;
                if (game != null) await SelectGameAsync(game);
            };
            SetGames(null, _title);
            _light = new CheckBox { Style = Res.Style("SwitchBox"), Content = Lang.T("Fondo claro"), VerticalAlignment = VerticalAlignment.Center };
            _light.SetResourceReference(Control.ForegroundProperty, "PanelFg");
            _light.Checked += delegate { UpdateBackground(); }; _light.Unchecked += delegate { UpdateBackground(); };
            options.Children.Add(_games); Grid.SetColumn(_light, 1); options.Children.Add(_light);
            Grid.SetRow(options, 2); layout.Children.Add(options);

            _status = Res.Label(Lang.T("Buscando iconos..."), 11.5, "PanelSub");
            _status.TextTrimming = TextTrimming.CharacterEllipsis; _status.Margin = new Thickness(1, 0, 0, 8);
            Grid.SetRow(_status, 3); layout.Children.Add(_status);
            var gallery = new Grid();
            _rejilla = new WrapPanel { ItemWidth = 140, ItemHeight = 156, HorizontalAlignment = HorizontalAlignment.Center };
            _scroll = new ScrollViewer { Content = _rejilla, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.VerticalOnly, Focusable = false, Padding = new Thickness(2, 3, 2, 3) };
            _scroll.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), Res.Style("IconGalleryScrollBar"));
            gallery.Children.Add(_scroll);
            _emptyTitle = Res.Label(Lang.T("Busca iconos para tu juego"), 17, "PanelFg");
            _emptyTitle.TextAlignment = TextAlignment.Center; _emptyTitle.TextWrapping = TextWrapping.Wrap;
            _emptyHint = Res.Label(Lang.T("Prueba con el título original, otra edición o una abreviatura."), 12, "PanelSub");
            _emptyHint.TextAlignment = TextAlignment.Center; _emptyHint.TextWrapping = TextWrapping.Wrap; _emptyHint.Margin = new Thickness(0, 9, 0, 0);
            _empty = new StackPanel { Margin = new Thickness(25), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            _empty.Children.Add(_emptyTitle); _empty.Children.Add(_emptyHint); gallery.Children.Add(_empty);
            Grid.SetRow(gallery, 4); layout.Children.Add(gallery);
            _footer = Res.Label(Lang.T("Elige un icono para aplicarlo · Esc para cerrar"), 10.5, "PanelSub");
            _footer.TextWrapping = TextWrapping.Wrap; _footer.Margin = new Thickness(1, 8, 0, 0);
            Grid.SetRow(_footer, 5); layout.Children.Add(_footer); shell.Child = layout;

            _par.Win.Closed += delegate { _closed = true; if (_request != null) _request.Cancel(); };
            _par.Win.ContentRendered += async delegate
            { if (_started) return; _started = true; _query.Focus(); _query.SelectAll(); await SearchAsync(true); };
        }

        Ficha FichaDe(SgdIcono icon)
        {
            var image = new Image { Width = 112, Height = 112, Stretch = Stretch.Uniform, IsHitTestVisible = false };
            double radius = Math.Min(56, _dock.Lay.TileRadius(112));
            var clip = new RectangleGeometry(new Rect(0, 0, 112, 112), radius, radius); clip.Freeze(); image.Clip = clip;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);
            var placeholder = Res.Label("···", 22, "PanelSub"); placeholder.HorizontalAlignment = HorizontalAlignment.Center; placeholder.VerticalAlignment = VerticalAlignment.Center;
            var content = new Grid(); content.Children.Add(image); content.Children.Add(placeholder);
            var preview = new Border { Width = 112, Height = 112, CornerRadius = new CornerRadius(radius), Background = PreviewBackground, Child = content };
            var dimensions = Res.Label(icon.Width + " × " + icon.Height, 10.5, "PanelSub");
            dimensions.HorizontalAlignment = HorizontalAlignment.Center; dimensions.Margin = new Thickness(0, 5, 0, 0);
            var card = new StackPanel(); card.Children.Add(preview); card.Children.Add(dimensions);
            var button = new RadioButton { Style = Res.Style("IconCard"), Content = card, GroupName = "community-icon", IsEnabled = false,
                ToolTip = Lang.F("{0} × {1} · Puntuación de la comunidad: {2}", icon.Width, icon.Height, icon.Score) };
            AutomationProperties.SetName(button, Lang.F("Icono de {0} × {1}", icon.Width, icon.Height));
            var tile = new Ficha { Btn = button, Preview = preview, Image = image, Placeholder = placeholder, Url = icon.Url,
                Thumb = string.IsNullOrEmpty(icon.Thumb) ? icon.Url : icon.Thumb };
            button.Click += delegate { if (_closed) return; _elegida = tile.Url; _par.Win.Close(); };
            return tile;
        }

        void UpdateBackground() { foreach (var card in _fichas) card.Preview.Background = PreviewBackground; }
        Brush PreviewBackground { get { return _light != null && _light.IsChecked == true ? LightBackground : DarkBackground; } }
        static readonly Brush DarkBackground = Checker("#FF27343F", "#FF33414C");
        static readonly Brush LightBackground = Checker("#FFF0F1F4", "#FFDEE2E8");
        static Brush Checker(string first, string second)
        {
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(Themes.Br(first), null, new RectangleGeometry(new Rect(0, 0, 12, 12))));
            group.Children.Add(new GeometryDrawing(Themes.Br(second), null, new RectangleGeometry(new Rect(0, 0, 6, 6))));
            group.Children.Add(new GeometryDrawing(Themes.Br(second), null, new RectangleGeometry(new Rect(6, 6, 6, 6))));
            var brush = new DrawingBrush(group) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 12, 12), ViewportUnits = BrushMappingMode.Absolute };
            brush.Freeze(); return brush;
        }
    }
}
