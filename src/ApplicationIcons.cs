using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace UlanziLite {
    static class ApplicationIcons {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct FileInfo {
            public IntPtr Icon;
            public int Index;
            public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string TypeName;
        }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string path,uint attributes,out FileInfo info,uint size,uint flags);
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr icon);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
        static extern uint ExtractIconEx(string path,int index,out IntPtr large,IntPtr small,uint count);

        public static Icon Load(string path) {
            if(String.Equals(Path.GetExtension(path),".lnk",StringComparison.OrdinalIgnoreCase)) {
                object shell=null,shortcut=null;
                try {
                    shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
                    string location=(string)shortcut.GetType().InvokeMember("IconLocation",BindingFlags.GetProperty,null,shortcut,null);
                    int comma=location.LastIndexOf(','),index;
                    if(comma>=0 && Int32.TryParse(location.Substring(comma+1).Trim(),out index)) {
                        string resource=Environment.ExpandEnvironmentVariables(location.Substring(0,comma).Trim().Trim('"'));
                        if(File.Exists(resource)) {
                            IntPtr handle;ExtractIconEx(resource,index,out handle,IntPtr.Zero,1);
                            if(handle!=IntPtr.Zero)return CopyIcon(handle);
                        }
                    }
                    string target=(string)shortcut.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null);
                    if(!String.IsNullOrWhiteSpace(target))path=Environment.ExpandEnvironmentVariables(target);
                }finally {
                    if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);
                    if(shell!=null)Marshal.FinalReleaseComObject(shell);
                }
            }
            FileInfo info;
            // Request the large shell icon without link or other overlays.
            SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(FileInfo)),0x100);
            if(info.Icon==IntPtr.Zero)return null;
            return CopyIcon(info.Icon);
        }
        static Icon CopyIcon(IntPtr handle) {
            try {using(var icon=Icon.FromHandle(handle))return (Icon)icon.Clone();}
            finally {DestroyIcon(handle);}
        }
    }
}
