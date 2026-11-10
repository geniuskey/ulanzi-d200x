using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UlanziLite {
    public static class RuntimeContext {
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr GetProcessWindowStation();
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr GetThreadDesktop(uint thread);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
        [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder text,int length,out int required);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll")] static extern bool IsTokenRestricted(IntPtr token);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        static string Name(IntPtr handle) {if(handle==IntPtr.Zero)return "unavailable ("+Marshal.GetLastWin32Error()+")";var text=new StringBuilder(256);int required;return GetUserObjectInformation(handle,2,text,512,out required)?text.ToString():"unavailable ("+Marshal.GetLastWin32Error()+")";}
        public static bool IsIsolatedDesktop(string name) {return name!=null && name.StartsWith("CodexSandboxDesktop-",StringComparison.OrdinalIgnoreCase);}
        public static string StartupProblem() {
            string desktop=Name(GetThreadDesktop(GetCurrentThreadId()));
            return IsIsolatedDesktop(desktop)?"App launching and foreground detection are limited on isolated test desktops. No test process was started. Run UlanziLite.exe directly from File Explorer.":null;
        }
        public static string Describe() {
            string current=Name(GetThreadDesktop(GetCurrentThreadId()));string station=Name(GetProcessWindowStation());
            IntPtr input=OpenInputDesktop(0,false,1);string active=Name(input);if(input!=IntPtr.Zero)CloseDesktop(input);
            IntPtr token;string restricted="unknown";if(OpenProcessToken(GetCurrentProcess(),8,out token)){restricted=IsTokenRestricted(token).ToString();CloseHandle(token);}
            return "Window station: "+station+"; thread desktop: "+current+"; input desktop: "+active+"; restricted token: "+restricted;
        }
    }
}
