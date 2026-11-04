using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace GamesDock
{
    public partial class Dock
    {
        public event Action PagesChanged;
        List<string> ActiveOrder { get { return PageStore.Active(Cfg).Order; } set { PageStore.Active(Cfg).Order = value; } }
        List<GroupDto> ActiveGroups { get { return PageStore.Active(Cfg).Groups; } }

        void NotifyPagesChanged()
        {
            var handler = PagesChanged;
            if (handler != null) handler();
        }

        void UpdatePagesMenu()
        {
            _verMenu.Items.Clear();
            PageStore.Normalize(Cfg);
            foreach (var page in Cfg.Pages)
            {
                string id = page.Id;
                var row = new MenuItem { Header = PageStore.Label(page), Tag = id,
                    IsCheckable = true, IsChecked = Cfg.Section == id };
                row.Click += delegate { SetSection(id); };
                _verMenu.Items.Add(row);
            }
            _verMenu.Items.Add(new Separator());
            var add = MenuRow(Lang.T("+ Añadir página"), CreatePage);
            add.IsEnabled = PageStore.CustomCount(Cfg) < PageStore.MaxCustomPages;
            if (!add.IsEnabled) add.ToolTip = Lang.T("Puedes crear hasta 5 páginas personalizadas.");
            _verMenu.Items.Add(add);
            if (!PageStore.IsFixed(Cfg.Section))
            {
                _verMenu.Items.Add(new Separator());
                _verMenu.Items.Add(MenuRow(Lang.T("Renombrar página"), RenamePage));
                _verMenu.Items.Add(MenuRow(Lang.T("Eliminar página"), DeletePage));
            }
        }

        public void CreatePage()
        {
            if (PageStore.CustomCount(Cfg) >= PageStore.MaxCustomPages)
            { Settings.Aviso(Lang.T("Puedes crear hasta 5 páginas personalizadas.")); return; }
            string name = Settings.ReadPrompt(this, Lang.T("Nueva página"),
                Lang.T("Elige un nombre de hasta 40 caracteres."), Lang.T("Nombre de la página"), "");
            if (name == null) return;
            var page = PageStore.Create(Cfg, name);
            if (page == null) { Settings.Aviso(Lang.T("Escribe un nombre distinto, de 1 a 40 caracteres.")); return; }
            SetSection(page.Id);
        }

        void RenamePage()
        {
            var page = PageStore.Active(Cfg);
            if (PageStore.IsFixed(page.Id)) return;
            string name = Settings.ReadPrompt(this, Lang.T("Renombrar página"),
                Lang.T("Elige un nombre de hasta 40 caracteres."), Lang.T("Nombre de la página"), page.Name);
            if (name == null) return;
            if (!PageStore.Rename(Cfg, page.Id, name))
            { Settings.Aviso(Lang.T("Escribe un nombre distinto, de 1 a 40 caracteres.")); return; }
            Cfg.Save(); NotifyPagesChanged();
        }

        void DeletePage()
        {
            var page = PageStore.Active(Cfg);
            if (PageStore.IsFixed(page.Id)) return;
            if (MessageBox.Show(Win, Lang.F("¿Eliminar la página «{0}»? Las demás páginas conservarán sus accesos.", page.Name),
                Lang.T("Eliminar página"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (PageStore.Delete(Cfg, page.Id)) SetSection("games");
        }

        MenuItem AddToPageMenu(ItemSpec item)
        {
            var menu = new MenuItem { Header = Lang.T("Añadir a página") };
            foreach (var page in Cfg.Pages)
            {
                if (page.Id == Cfg.Section) continue;
                string id = page.Id;
                var row = MenuRow(PageStore.Label(page), delegate
                {
                    EnsurePin(item);
                    if (!PageStore.Add(Cfg, id, item.Key)) return;
                    item.Fav = PageStore.IsFavorite(Cfg, item.Key);
                    Cfg.Save(); Refresh(false);
                });
                row.Tag = id; row.IsEnabled = !page.Members.Contains(item.Key);
                menu.Items.Add(row);
            }
            return menu;
        }
    }
}
