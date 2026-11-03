using System;
using System.Windows;
using System.Windows.Controls;

namespace GamesDock
{
    public partial class Dock
    {
        void UpdateTilePose(Button tile, double scale, int index)
        {
            double amount = Cfg.Motion && Cfg.Magnify ? (scale - 1) / (Lay.Peak - 1) : 0;
            double offset = Lay.Vert ? StripScroll.VerticalOffset : StripScroll.HorizontalOffset;
            double center = Lay.TileCenter(index, Tiles.Count, offset);
            SagaTileMotion.Hover(tile, _theme.Material, amount, (_ptrLive - center) / Lay.Pitch,
                Lay.Vert, Lay.Outward, Cfg.IconSize);
        }

        void UpdateMaterialFocus()
        {
            if (!_ptrInside || Tiles.Count == 0)
            {
                Material.SetFocus(Rect.Empty, 0, 0); return;
            }
            Button target = null; double distance = double.MaxValue;
            foreach (var tile in Tiles)
            {
                Point center = tile.TranslatePoint(new Point(tile.ActualWidth / 2, tile.ActualHeight / 2), RootGrid);
                double current = Math.Abs((Lay.Vert ? center.Y : center.X) - _ptrLive);
                if (current < distance) { distance = current; target = tile; }
            }
            var artwork = target == null ? null : target.Content as FrameworkElement;
            if (artwork == null || artwork.ActualWidth < 1) { Material.SetFocus(Rect.Empty, 0, 0); return; }
            Rect bounds = artwork.TransformToAncestor(RootGrid).TransformBounds(new Rect(artwork.RenderSize));
            double scale = target.RenderTransform.Value.M11;
            Material.SetFocus(bounds, Math.Min(Cfg.IconSize / 2.0, Lay.TileRadius(Cfg.IconSize)) * scale,
                Cfg.Magnify ? Math.Max(0, Math.Min(1, (scale - 1) / (Lay.Peak - 1))) : 0);
        }
    }
}
