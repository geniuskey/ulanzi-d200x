using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UlanziLite {
    static class Program {
        static readonly object logLock=new object();
        public static void Log(string text) {
            lock(logLock)try {
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ulanzi-lite.log");
                if(File.Exists(path) && new FileInfo(path).Length>512*1024){if(File.Exists(path+".old"))File.Delete(path+".old");File.Move(path,path+".old");}
                File.AppendAllText(path,DateTime.Now.ToString("s")+" "+text+Environment.NewLine,Encoding.UTF8);
            }catch { }
        }
        [STAThread] static int Main(string[] args) {
            if(args.Contains("--usage-test"))return LocalUsageReader.Diagnose();
            if(args.Contains("--render")) {
                if(args.Contains("--live-usage"))new LocalUsageReader().RefreshAsync().GetAwaiter().GetResult();
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                using(var form=new MainForm(Configuration.Defaults(),true)) {
                    form.Show();Application.DoEvents();using(var bitmap=new System.Drawing.Bitmap(form.Width,form.Height)) {
                        form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height));
                        bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));
                    }
                }return 0;
            }
            if(args.Contains("--diagnose")) {
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics.txt");
                try {
                    var result=new StringBuilder();result.AppendLine(RuntimeContext.Describe());result.AppendLine("Installed apps: "+Configuration.InstalledApps().Count);result.AppendLine("HID devices:");foreach(var info in HidNative.Enumerate())result.AppendLine(info.ToString());
                    var audio=new AudioController();audio.TrackForeground();result.AppendLine("Microphone: "+audio.Endpoint(true,0,false));result.AppendLine("Output: "+audio.Endpoint(false,0,false));result.AppendLine("Foreground: "+audio.ForegroundName+" / "+audio.ApplicationVolume(0,false));
                    File.WriteAllText(path,result.ToString(),Encoding.UTF8);return 0;
                }catch(Exception ex){File.WriteAllText(path,ex.ToString());return 1;}
            }
            if(args.Contains("--self-test"))return SelfTest.Run();
            if(args.Contains("--audio-test"))return AudioSmokeTest.Run();
            string startupProblem=RuntimeContext.StartupProblem();
            if(startupProblem!=null){Log(startupProblem+" "+RuntimeContext.Describe());return 2;}
            bool first;using(var mutex=new Mutex(true,"Local\\Geniuskey.UlanziLite.D200X",out first)) {
                if(!first){MessageBox.Show("Already running. Open Ulanzi Lite from the system tray.","Ulanzi Lite");return 0;}
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException+=(s,e)=>{Log(e.Exception.ToString());MessageBox.Show(e.Exception.Message,"Ulanzi Lite");};
                try {Log("Starting interactive controller. "+RuntimeContext.Describe());var configuration=Configuration.Load();Application.Run(new MainForm(configuration));return 0;}
                catch(Exception ex){Log(ex.ToString());MessageBox.Show(ex.Message+"\nYour settings are preserved. Check ulanzi-lite.log.","Ulanzi Lite startup failed");return 1;}
            }
        }
    }
}
