using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GamesDock
{
    [DataContract]
    public class PinDto
    {
        [DataMember(Name = "key")] public string Key;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "fav")] public bool Fav;
        [DataMember(Name = "hidden")] public bool Hidden;
        [DataMember(Name = "link")] public string Link;
        [DataMember(Name = "target")] public string Target;
        [DataMember(Name = "args")] public string Args;
        [DataMember(Name = "work")] public string Work;
        [DataMember(Name = "origin")] public string Origin;
        [DataMember(Name = "kind")] public string Kind;
        [DataMember(Name = "icon")] public string Icon;
        [DataMember(Name = "added")] public string Added;
    }

    [DataContract]
    public class UsageDto
    {
        [DataMember(Name = "n")] public int N;
        [DataMember(Name = "d")] public string D;
    }

    // Un grupo es solo una lista de claves de fijados con un titulo: los miembros siguen siendo pins
    // normales, asi que desarmar el grupo no pierde nada y un config sin "groups" se lee igual.
    [DataContract]
    public class GroupDto
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "members")] public List<string> Members = new List<string>();
    }

    // El esquema de json es el de las versiones anteriores: el exe tiene que leer la config que ya
    // existe en la maquina sin migrarla. Los campos que no estan en el json conservan su valor por defecto.
    [DataContract]
    public class Config
    {
        [DataMember(Name = "version")] public int Version = 1;
        [DataMember(Name = "position")] public string Position = "bottom";
        [DataMember(Name = "left")] public double? Left;
        [DataMember(Name = "top")] public double? Top;
        [DataMember(Name = "iconSize")] public int IconSize = 46;
        [DataMember(Name = "pillPad")] public int PillPad = 2;
        [DataMember(Name = "theme")] public string Theme = "noche";
        [DataMember(Name = "magnify")] public bool Magnify = true;
        [DataMember(Name = "idleOpacity")] public int IdleOpacity = 45;
        [DataMember(Name = "minimizeAll")] public bool MinimizeAll = true;
        [DataMember(Name = "hideOnLaunch")] public bool HideOnLaunch = true;
        [DataMember(Name = "hotkey")] public bool Hotkey = true;
        [DataMember(Name = "desktopLevel")] public bool DesktopLevel = true;
        [DataMember(Name = "autostart")] public bool Autostart = false;
        [DataMember(Name = "section")] public string Section = "games";
        // "auto" toma la cultura de interfaz de Windows; "es" y "en" fuerzan el idioma
        [DataMember(Name = "language")] public string Language = "auto";
        [DataMember(Name = "order")] public List<string> Order = new List<string>();
        [DataMember(Name = "pins")] public List<PinDto> Pins = new List<PinDto>();
        [DataMember(Name = "groups")] public List<GroupDto> Groups = new List<GroupDto>();
        [DataMember(Name = "usage")] public Dictionary<string, UsageDto> Usage = new Dictionary<string, UsageDto>();
        [DataMember(Name = "desktopMoved")] public Dictionary<string, string> DesktopMoved = new Dictionary<string, string>();

        public static Config Load()
        {
            var cfg = new Config();
            if (!File.Exists(Paths.CfgFile)) return cfg;
            try
            {
                byte[] raw = File.ReadAllBytes(Paths.CfgFile);
                int off = 0;
                if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) off = 3;
                // ReadObject(Stream) usa las cuotas por defecto: basta con saltar el BOM que escribieron
                // las versiones anteriores
                using (var ms = new MemoryStream(raw, off, raw.Length - off, false))
                {
                    var dcs = new DataContractJsonSerializer(typeof(Config));
                    cfg = (Config)dcs.ReadObject(ms) ?? new Config();
                }
            }
            catch (Exception ex)
            {
                Log.Write("Lectura de config fallida: " + ex.Message);
                cfg = new Config();
            }
            if (cfg.Order == null) cfg.Order = new List<string>();
            if (cfg.Pins == null) cfg.Pins = new List<PinDto>();
            if (cfg.Groups == null) cfg.Groups = new List<GroupDto>();
            if (cfg.Usage == null) cfg.Usage = new Dictionary<string, UsageDto>();
            if (cfg.DesktopMoved == null) cfg.DesktopMoved = new Dictionary<string, string>();
            int fus = MergePins(cfg);
            Log.Write(string.Format("Config leida: {0} fijados, {1} usos, {2} grupos{3}", cfg.Pins.Count, cfg.Usage.Count,
                cfg.Groups.Count, fus > 0 ? ", " + fus + " duplicados fusionados" : "."));
return cfg;
        }

        // las versiones anteriores guardaban los fijados en un hashtable por clave, asi que volver a
        // fijar sobrescribia el antiguo. Aqui es una lista: si no se fusiona quedan dos entradas para
        // la misma clave y la primera (la oculta al soltar el tile fuera) tapa al tile nuevo.
        static int MergePins(Config cfg)
        {
            var pos = new Dictionary<string, int>();
            var keep = new List<PinDto>();
            int fus = 0;
            foreach (var p in cfg.Pins)
            {
                string k = p.Key ?? string.Empty;
                int i;
                if (k.Length > 0 && pos.TryGetValue(k, out i)) { keep[i] = p; fus++; }
                else { if (k.Length > 0) pos[k] = keep.Count; keep.Add(p); }
            }
            if (fus > 0) cfg.Pins = keep;
            return fus;
        }

        public void Save()
        {
            try
            {
                var dcs = new DataContractJsonSerializer(typeof(Config));
                var ms = new MemoryStream();
                dcs.WriteObject(ms, this);
                string json = Encoding.UTF8.GetString(ms.ToArray());
                string tmp = Paths.CfgFile + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(true));
                File.Copy(tmp, Paths.CfgFile, true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                Log.Write("Escritura de config fallida: " + ex.Message);
            }
        }
    }
}
