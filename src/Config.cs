using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace UlanziLite {
    public class KeyBinding {
        public string Label="빈 버튼", Kind="website", Target="", Image="", Color="#273349";
        public void Validate(bool validateImage=true) {
            if(Label==null || Label.Length>40)throw new ArgumentException("이름은 40자까지 입력해 주세요.");
            if(Kind!="website" && Kind!="application")throw new ArgumentException("지원하지 않는 버튼 종류입니다.");
            try { System.Drawing.ColorTranslator.FromHtml(Color); }catch {throw new ArgumentException("버튼 색상 형식이 올바르지 않습니다.");}
            if(validateImage && !String.IsNullOrWhiteSpace(Image)) {
                if(!File.Exists(Image))throw new ArgumentException("아이콘 이미지 파일을 찾을 수 없습니다. 다시 선택하거나 비워 주세요.");
                try {using(var img=System.Drawing.Image.FromFile(Image)) { }}catch {throw new ArgumentException("이미지를 읽을 수 없습니다. PNG, JPG 또는 BMP 파일을 선택해 주세요.");}
            }
            if(String.IsNullOrWhiteSpace(Target))return;
            if(Kind=="website") { Uri uri; if(!Uri.TryCreate(Target,UriKind.Absolute,out uri) || (uri.Scheme!="https" && uri.Scheme!="http") || String.IsNullOrEmpty(uri.Host))throw new ArgumentException("http:// 또는 https:// 주소를 입력해 주세요."); }
            else if(Kind=="application") { if(!File.Exists(Target) && !Directory.Exists(Target))throw new ArgumentException("앱 또는 파일 경로를 찾을 수 없습니다."); }
        }
        public void Launch() { Validate(false); if(!String.IsNullOrWhiteSpace(Target))Process.Start(new ProcessStartInfo(Target) {UseShellExecute=true}); }
    }
    public class DeckPage { public string Name; public List<KeyBinding> Keys=new List<KeyBinding>(); }
    public class Configuration {
        public int Version=1, Step=2, Brightness=65, Page=0, OverlaySeconds=3;
        public List<DeckPage> Pages=new List<DeckPage>();
        public static readonly string PathName=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.json");
        public static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
        public static List<string> InstalledApps() {
            var result=new List<string>();
            var folders=new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),Path.Combine(Environment.GetEnvironmentVariable("APPDATA")??"","Microsoft","Windows","Start Menu"),Path.Combine(Environment.GetEnvironmentVariable("PROGRAMDATA")??"C:\\ProgramData","Microsoft","Windows","Start Menu")};
            foreach(string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase)) {
                if(String.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))continue;
                ScanApps(folder,result);
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(System.IO.Path.GetFileNameWithoutExtension).ToList();
        }
        static void ScanApps(string folder,List<string> result) {
            try { result.AddRange(Directory.GetFiles(folder,"*.lnk",SearchOption.TopDirectoryOnly)); }catch(UnauthorizedAccessException) { }catch(IOException) { }
            string[] children;try{children=Directory.GetDirectories(folder);}catch(UnauthorizedAccessException){return;}catch(IOException){return;}
            foreach(string child in children)ScanApps(child,result);
        }
        public static Configuration Defaults() {
            var config=new Configuration();
            for(int p=0;p<3;p++) { var page=new DeckPage {Name=new[]{"자주 쓰는 것","작업","미디어"}[p]}; for(int i=0;i<13;i++)page.Keys.Add(new KeyBinding()); config.Pages.Add(page); }
            var apps=InstalledApps(); string[] names={"Google Chrome","Notion","Discord","Visual Studio 2022","Obsidian","File Explorer"};
            string[] colors={"#264E69","#353742","#47488D","#5A3A83","#514079","#71602C"};
            for(int i=0;i<names.Length;i++) { string path=apps.FirstOrDefault(a=>String.Equals(System.IO.Path.GetFileNameWithoutExtension(a),names[i],StringComparison.OrdinalIgnoreCase)); if(path!=null)config.Pages[0].Keys[i]=new KeyBinding {Label=names[i],Kind="application",Target=path,Color=colors[i]}; }
            string[] labels={"ChatGPT","GitHub","YouTube","Gmail"}; string[] urls={"https://chatgpt.com","https://github.com","https://youtube.com","https://mail.google.com"};
            for(int i=0;i<4;i++)config.Pages[0].Keys[i+6]=new KeyBinding {Label=labels[i],Target=urls[i],Color=new[]{"#23574C","#363C4D","#743A46","#6C4835"}[i]};
            return config;
        }
        public static Configuration Load() {
            if(!File.Exists(PathName))return Defaults();
            return Parse(File.ReadAllText(PathName,Encoding.UTF8));
        }
        public static Configuration Parse(string text) {
            Configuration c;
            try {c=Json.Deserialize<Configuration>(text);}catch(Exception ex){throw new InvalidDataException("설정 JSON을 읽을 수 없습니다. settings.json.bak으로 복구할 수 있습니다.",ex);}
            if(c==null || c.Version!=1 || c.Pages==null || c.Pages.Count==0 || c.Pages.Count>20)throw new InvalidDataException("설정 파일 형식이 올바르지 않습니다.");
            foreach(var p in c.Pages) { if(p==null || p.Keys==null || p.Keys.Count!=13 || p.Keys.Any(k=>k==null))throw new InvalidDataException("페이지에 버튼 13개가 필요합니다."); if(String.IsNullOrWhiteSpace(p.Name))p.Name="페이지 "+(c.Pages.IndexOf(p)+1); foreach(var k in p.Keys) { k.Label=k.Label??""; k.Target=k.Target??""; k.Image=k.Image??""; if(k.Color==null)k.Color="#273349"; if(k.Kind!="website" && k.Kind!="application")throw new InvalidDataException("지원하지 않는 버튼 종류입니다."); } }
            c.Page=Math.Max(0,Math.Min(c.Pages.Count-1,c.Page)); c.Step=Math.Max(1,Math.Min(10,c.Step)); c.Brightness=Math.Max(0,Math.Min(100,c.Brightness));c.OverlaySeconds=Math.Max(1,Math.Min(30,c.OverlaySeconds)); return c;
        }
        public void Save() {
            SaveTo(PathName);
        }
        public void SaveTo(string path) {
            string temp=path+".tmp";
            try {
                File.WriteAllText(temp,Json.Serialize(this),new UTF8Encoding(false));
                if(File.Exists(path))File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
            } finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
    public static class Artwork {
        public static Bitmap Render(KeyBinding key,WebsiteIconCache websiteIcons=null) {
            var bitmap=new Bitmap(196,196,PixelFormat.Format24bppRgb);
            using(var g=Graphics.FromImage(bitmap)) {
                g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                Color color; try {color=ColorTranslator.FromHtml(key.Color);}catch {color=Color.FromArgb(39,51,73);}
                g.Clear(color);
                g.SmoothingMode=SmoothingMode.None;
                using(var safeEdge=new SolidBrush(System.Drawing.Color.FromArgb(11,16,24)))g.FillRectangle(safeEdge,0,WideArtwork.SafeHeight,196,196-WideArtwork.SafeHeight);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.SetClip(new Rectangle(0,0,196,WideArtwork.SafeHeight));
                bool drawn=false;
                if(!String.IsNullOrWhiteSpace(key.Image))try {using(var img=System.Drawing.Image.FromFile(key.Image)) {float scale=Math.Min(96f/img.Width,96f/img.Height);float w=img.Width*scale,h=img.Height*scale;g.DrawImage(img,50+(96-w)/2,25+(96-h)/2,w,h); drawn=true;}}catch { }
                if(!drawn && key.Kind=="website")using(var icon=(websiteIcons??WebsiteIconCache.Shared).GetIcon(key))if(icon!=null){g.DrawImage(icon,new Rectangle(50,25,96,96));drawn=true;}
                if(!drawn && key.Kind=="application" && (File.Exists(key.Target) || Directory.Exists(key.Target)))try {using(var icon=ApplicationIcons.Load(key.Target))if(icon!=null) { g.DrawIcon(icon,new Rectangle(50,25,96,96)); drawn=true; }}catch { }
                if(!drawn)using(var font=new Font("Segoe UI",46,FontStyle.Bold)) {
                    string mark=String.IsNullOrWhiteSpace(key.Target)?"+":(String.IsNullOrWhiteSpace(key.Label)?"?":System.Globalization.StringInfo.GetNextTextElement(key.Label));
                    g.DrawString(mark,font,Brushes.White,new RectangleF(0,17,196,105),new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center});
                }
                using(var font=new Font(Typography.UiFontName,17,FontStyle.Bold))g.DrawString(key.Label??"",font,Brushes.White,new RectangleF(10,121,176,59),new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter});
            } return bitmap;
        }
        static Dictionary<int,byte[]> PageIcons(DeckPage page,byte[] wideImage) {
            var icons=new Dictionary<int,byte[]>();
            for(int i=0;i<14;i++) {if(i==13 && wideImage!=null){icons[i]=wideImage;continue;}using(var image=i==13?new Bitmap(196,196):i>=10?UsageMeters.Render(i-10):Render(page.Keys[i]))using(var ms=new MemoryStream()) {image.Save(ms,ImageFormat.Png);icons[i]=ms.ToArray();}}
            return icons;
        }
        public static byte[] Bundle(DeckPage page,byte[] wideImage=null) {return BundleForIcons(PageIcons(page,wideImage));}
        // Every upload must be self-contained. Partial ZIPs can invalidate images
        // referenced by the previous manifest on the device. Cache unchanged PNGs
        // so the ticking clock does not redraw applications or reload their icons.
        public sealed class DisplayImages {
            Dictionary<int,byte[]> icons;
            public byte[] Reset(DeckPage page,byte[] wideImage) {
                icons=PageIcons(page,wideImage);return BundleForIcons(icons);
            }
            public byte[] Usage() {
                if(icons==null)throw new InvalidOperationException("Display page has not been initialized.");
                for(int i=0;i<3;i++)using(var image=UsageMeters.Render(i))using(var ms=new MemoryStream()){image.Save(ms,ImageFormat.Png);icons[i+10]=ms.ToArray();}
                return BundleForIcons(icons);
            }
            public byte[] Wide(byte[] png) {
                if(icons==null)throw new InvalidOperationException("Display page has not been initialized.");
                icons[13]=png;return BundleForIcons(icons);
            }
        }
        static byte[] BundleForIcons(Dictionary<int,byte[]> icons) {
            var files=new Dictionary<string,byte[]>(); var manifest=new Dictionary<string,object>();
            using(var sha=SHA256.Create())foreach(var icon in icons) {
                int i=icon.Key;byte[] png=icon.Value;
                string name="icons/"+BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").Substring(0,20)+".png"; files[name]=png;
                manifest[(i%5)+"_"+(i/5)]=new {State=0,ViewParam=new[]{new {Text="",Icon=name}}};
            }
            byte[] json=Encoding.UTF8.GetBytes(Configuration.Json.Serialize(manifest));
            var random=new Random();
            for(int attempt=0;attempt<256;attempt++) {
                using(var memory=new MemoryStream()) {
                    using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
                        byte[] padding=new byte[16+attempt*11]; random.NextBytes(padding); WriteEntry(zip,"padding.bin",padding);
                        foreach(var file in files)WriteEntry(zip,file.Key,file.Value); WriteEntry(zip,"manifest.json",json);
                    }
                    byte[] data=memory.ToArray(); bool clean=true;
                    for(int pos=1016;pos<data.Length;pos+=1024)if(data[pos]==0 || data[pos]==0x7c) {clean=false;break;}
                    if(clean)return data;
                }
            }
            throw new IOException("장치용 이미지 묶음을 만들지 못했습니다. 다시 저장해 주세요.");
        }
        static void WriteEntry(ZipArchive zip,string name,byte[] bytes) {var entry=zip.CreateEntry(name,CompressionLevel.Optimal); using(var s=entry.Open())s.Write(bytes,0,bytes.Length);}
    }
}
