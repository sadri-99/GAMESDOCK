using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GamesDock
{
    public partial class IconPicker
    {
        const int DownloadLanes = 4, CacheLimit = 64;
        readonly Dictionary<string, ImageSource> _imageCache = new Dictionary<string, ImageSource>(StringComparer.Ordinal);
        readonly Queue<string> _imageKeys = new Queue<string>();

        async Task LoadThumbnailsAsync(List<Ficha> cards, int revision, CancellationToken token)
        {
            int next = 0, finished = 0, failed = 0;
            int pixels = Math.Max(224, (int)Math.Ceiling(112 * Dock.DipScale(_par.Win)));
            Func<Task> worker = async delegate
            {
                while (Current(revision, token))
                {
                    int index = next++; if (index >= cards.Count) break;
                    var card = cards[index]; ImageSource image = null;
                    try
                    {
                        string cacheKey = card.Thumb + "@" + pixels;
                        if (!_imageCache.TryGetValue(cacheKey, out image))
                        {
                            var result = await SteamGrid.DescargarBytesAsync(card.Thumb, token);
                            if (!Current(revision, token)) return;
                            if (result.EsOk) image = DecodeThumbnail(result.Datos, pixels);
                            if (image == null && card.Thumb != card.Url)
                            {
                                var original = await SteamGrid.DescargarBytesAsync(card.Url, token);
                                if (!Current(revision, token)) return;
                                if (original.EsOk) image = DecodeThumbnail(original.Datos, pixels);
                            }
                            if (image != null)
                            {
                                while (_imageKeys.Count >= CacheLimit) _imageCache.Remove(_imageKeys.Dequeue());
                                _imageCache[cacheKey] = image; _imageKeys.Enqueue(cacheKey);
                            }
                        }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex) { Log.Write("Vista previa de icono: " + ex.Message); }
                    if (!Current(revision, token)) return;
                    if (image != null)
                    { card.Image.Source = image; card.Placeholder.Visibility = Visibility.Collapsed; card.Btn.IsEnabled = true; }
                    else
                    {
                        failed++; card.Placeholder.Text = "—";
                        card.Btn.ToolTip = Lang.T("Vista previa no disponible. Vuelve a buscar para reintentar.");
                    }
                    finished++;
                    _footer.Text = Lang.F("Miniaturas: {0}/{1} · Esc para cerrar", finished, cards.Count);
                }
            };
            var tasks = new List<Task>();
            for (int lane = 0; lane < DownloadLanes; lane++) tasks.Add(worker());
            await Task.WhenAll(tasks);
            if (Current(revision, token)) _footer.Text = failed > 0
                ? Lang.T("Algunas vistas previas no se cargaron. Vuelve a buscar para reintentar.")
                : Lang.T("Elige un icono para aplicarlo · Esc para cerrar");
        }

        static ImageSource DecodeThumbnail(byte[] data, int pixels)
        {
            try
            {
                var image = new BitmapImage();
                using (var stream = new MemoryStream(data))
                {
                    image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = pixels; image.StreamSource = stream; image.EndInit();
                }
                image.Freeze(); return image;
            }
            catch (Exception) { return null; }
        }
    }
}
