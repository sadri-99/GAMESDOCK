using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;

namespace GamesDock
{
    // Logon task: direct GUI executable, interactive user, no password, no elevation or delay.
    public static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static string Sid { get { return WindowsIdentity.GetCurrent().User.Value; } }
        public static string TaskName { get { return "GamesDock-" + Sid; } }
        static string LegacyLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "GamesDock.lnk"); } }
        static string LegacyVbs { get { return Path.Combine(Paths.Root, "GamesDock.lanzador.vbs"); } }
        static string Escape(string text) { return SecurityElement.Escape(text); }

        internal static string Definition(string exe, string sid)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Description>GamesDock: iniciar al entrar en Windows</Description></RegistrationInfo>" +
                "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + Escape(sid) + "</UserId></LogonTrigger></Triggers>" +
                "<Principals><Principal id=\"User\"><UserId>" + Escape(sid) + "</UserId>" +
                "<LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
                "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
                "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
                "<AllowHardTerminate>false</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable>" +
                "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>" +
                "<IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>" +
                "<AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden>" +
                "<RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun>" +
                "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>6</Priority></Settings>" +
                "<Actions Context=\"User\"><Exec><Command>" + Escape(exe) + "</Command>" +
                "<Arguments>--startup</Arguments><WorkingDirectory>" + Escape(Path.GetDirectoryName(exe)) +
                "</WorkingDirectory></Exec></Actions></Task>";
        }

        static object Call(object target, string method, params object[] args)
        {
            return target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);
        }
        static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }

        static void WithScheduler(Action<object> action)
        {
            object service = null, folder = null;
            try
            {
                service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
                Call(service, "Connect");
                folder = Call(service, "GetFolder", @"\");
                action(folder);
            }
            finally { Release(folder); Release(service); }
        }

        static bool MissingTask(Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            return ex.HResult == unchecked((int)0x80070002);
        }

        static void RemoveTask(object folder)
        {
            try { Call(folder, "DeleteTask", TaskName, 0); }
            catch (Exception ex) { if (!MissingTask(ex)) throw; }
        }

        static void RemoveRun()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (key != null) key.DeleteValue("GamesDock", false);
        }
        static void RemoveLegacy()
        {
            if (File.Exists(LegacyLink)) File.Delete(LegacyLink);
            if (File.Exists(LegacyVbs)) File.Delete(LegacyVbs);
        }

        public static bool Set(bool on)
        {
            try
            {
                if (!on)
                {
                    // Remove every known registration. Keep errors visible rather than claim success.
                    WithScheduler(RemoveTask);
                    RemoveRun();
                    RemoveLegacy();
                    Log.Write("autostart: desactivado");
                    return true;
                }
                string exe = Update.Exe;
                try
                {
                    WithScheduler(folder =>
                    {
                        object task = Call(folder, "RegisterTask", TaskName, Definition(exe, Sid), 6, Sid, null, 3, null);
                        Release(task);
                    });
                    RemoveRun();
                    Log.Write("autostart: tarea de inicio de sesion -> " + exe);
                }
                catch (Exception ex)
                {
                    // Some managed PCs deny task creation. A direct Run entry remains usable.
                    // Delete any old task first, so fallback cannot leave two launch paths.
                    WithScheduler(RemoveTask);
                    using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                        key.SetValue("GamesDock", "\"" + exe + "\" --startup", RegistryValueKind.String);
                    Log.Write("autostart: registro directo (tarea no disponible): " + ex.GetBaseException().Message);
                }
                RemoveLegacy();
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("autostart fallida: " + ex.GetBaseException().Message);
                return false;
            }
        }

        public static void Repair(Config cfg)
        {
            // Re-register enabled startup after a portable move or an upgrade. Respect explicit off.
            if (cfg.Autostart) Set(true);
        }
    }
}
