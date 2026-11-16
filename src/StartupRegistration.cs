using System;
using System.IO;
using Microsoft.Win32;

namespace UlanziLite {
    static class StartupRegistration {
        const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        const string ValueName="UlanziLite";
        internal static string Command(string executable) {return "\""+Path.GetFullPath(executable)+"\" --startup";}
        public static bool Enabled {
            get {using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return IsEnabled(key,System.Windows.Forms.Application.ExecutablePath);}
        }
        internal static bool IsEnabled(RegistryKey key,string executable) {
            string value=key==null?null:key.GetValue(ValueName) as string;
            // Also recognize the command written by the original setup script.
            return String.Equals(value,Command(executable),StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value,"\""+Path.GetFullPath(executable)+"\"",StringComparison.OrdinalIgnoreCase);
        }
        public static void Apply(bool enabled,Action save) {
            using(var key=Registry.CurrentUser.CreateSubKey(RunKey))Apply(key,System.Windows.Forms.Application.ExecutablePath,enabled,save);
        }
        internal static void Apply(RegistryKey key,string executable,bool enabled,Action save) {
            object previous=key.GetValue(ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
            RegistryValueKind kind=previous==null?RegistryValueKind.String:key.GetValueKind(ValueName);
            try {
                if(enabled)key.SetValue(ValueName,Command(executable),RegistryValueKind.String);
                else key.DeleteValue(ValueName,false);
                if(IsEnabled(key,executable)!=enabled)throw new IOException("Windows startup registration could not be verified.");
                save();
            }catch {
                try {if(previous==null)key.DeleteValue(ValueName,false);else key.SetValue(ValueName,previous,kind);}
                catch(Exception ex){Program.Log("Could not restore startup registration: "+ex.Message);}
                throw;
            }
        }
    }
}
