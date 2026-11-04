using System;
using System.IO;
using System.Reflection;
using GamesDock;

static class CompactMarginValidation
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var baseline = Assembly.LoadFile(Path.GetFullPath(args[0]));
            var configType = baseline.GetType("GamesDock.Config");
            var layoutType = baseline.GetType("GamesDock.Layout");
            int checks = 0;
            foreach (var theme in Themes.All)
            foreach (int size in new[] { 32, 64, 96 })
            foreach (double scale in new[] { 1.0, 1.25, 2.0 })
            {
                var oldConfig = Activator.CreateInstance(configType);
                configType.GetField("IconSize").SetValue(oldConfig, size);
                configType.GetField("Theme").SetValue(oldConfig, theme.Key);
                var oldLayout = Activator.CreateInstance(layoutType, oldConfig);
                layoutType.GetField("Scale").SetValue(oldLayout, scale);
                var current = new Layout(new Config { Theme = theme.Key, IconSize = size }) { Scale = scale };
                double oldInset = (double)layoutType.GetProperty("RowInset").GetValue(oldLayout, null);
                double oldLength = (double)layoutType.GetMethod("RowLen").Invoke(oldLayout, new object[] { 8 });
                Check(current.RowInset, oldInset, theme.Key, ref checks);
                Check(current.RowLen(8) + 2 * current.RowInset, oldLength + 2 * oldInset, theme.Key, ref checks);
                foreach (int icon in new[] { 0, 7 })
                {
                    double oldCenter = (double)layoutType.GetMethod("TileCenter").Invoke(oldLayout, new object[] { icon, 8, 0.0 });
                    Check(current.TileCenter(icon, 8, 0), oldCenter, theme.Key, ref checks);
                }
            }
            Console.WriteLine("PASS: " + checks + " compact margin checks against the released baseline; same inset, width and end-icon positions.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Check(double actual, double expected, string theme, ref int checks)
    {
        if (Math.Abs(actual - expected) > .001) throw new Exception("Original compact spacing changed: " + theme);
        checks++;
    }
}
