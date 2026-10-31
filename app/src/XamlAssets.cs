using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace GamesDock
{
    public static class XamlAssets
    {
        static string ResourceName(string key) { return "GamesDock." + key + ".xaml"; }

        public static ResourceDictionary LoadDictionary(string key)
        {
            using (var s = typeof(XamlAssets).Assembly.GetManifestResourceStream(ResourceName(key)))
            {
                if (s == null) throw new IOException("recurso empotrado ausente: " + ResourceName(key));
                return (ResourceDictionary)XamlReader.Load(s);
            }
        }

        public static T Load<T>(string key)
        {
            using (var s = typeof(XamlAssets).Assembly.GetManifestResourceStream(ResourceName(key)))
            {
                if (s == null) throw new IOException("recurso empotrado ausente: " + ResourceName(key));
                return (T)XamlReader.Load(s);
            }
        }
    }
}
