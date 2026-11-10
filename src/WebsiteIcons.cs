using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UlanziLite {
    // Rendering only reads memory. Network and disk work run outside the UI thread.
    public sealed class WebsiteIconCache {
        public static readonly WebsiteIconCache Shared=new WebsiteIconCache(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"website-icons"),Download);
        public sealed class Resource {public byte[] Bytes;public Uri Address;}
        sealed class Entry {public byte[] Png;public DateTime RetryAt;public Task<bool> Pending;}
        readonly object gate=new object();
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        readonly SemaphoreSlim downloads=new SemaphoreSlim(3,3);
        readonly string folder;
        readonly Func<Uri,Resource> fetch;
        public WebsiteIconCache(string cacheFolder,Func<Uri,Resource> downloader) {folder=cacheFolder;fetch=downloader;}
        static Uri Origin(KeyBinding key) {
            Uri uri;
            if(key.Kind!="website" || !String.IsNullOrWhiteSpace(key.Image) || !Uri.TryCreate(key.Target,UriKind.Absolute,out uri) || !IsWeb(uri))return null;
            return new Uri(uri.GetLeftPart(UriPartial.Authority)+"/");
        }
        static bool IsWeb(Uri uri) {return uri!=null && (uri.Scheme=="http" || uri.Scheme=="https") && !String.IsNullOrEmpty(uri.Host);}
        public Bitmap GetIcon(KeyBinding key) {
            var origin=Origin(key);if(origin==null)return null;
            byte[] png;lock(gate){Entry entry;if(!entries.TryGetValue(origin.AbsoluteUri,out entry) || entry.Png==null)return null;png=entry.Png;}
            using(var stream=new MemoryStream(png))using(var image=Image.FromStream(stream))return new Bitmap(image);
        }
        public Task<bool> RefreshAsync(IEnumerable<KeyBinding> keys) {
            var tasks=keys.Select(Origin).Where(u=>u!=null).Select(u=>u.AbsoluteUri).Distinct().Select(RefreshOrigin).ToArray();
            return AnyChanged(tasks);
        }
        static async Task<bool> AnyChanged(Task<bool>[] tasks) {return (await Task.WhenAll(tasks)).Any(changed=>changed);}
        Task<bool> RefreshOrigin(string origin) {
            lock(gate) {
                Entry entry;if(!entries.TryGetValue(origin,out entry)){entry=new Entry();entries.Add(origin,entry);}
                if(entry.Pending!=null && !entry.Pending.IsCompleted)return entry.Pending;
                if(DateTime.UtcNow<entry.RetryAt)return Task.FromResult(false);
                entry.Pending=Task.Run(async()=>{
                    await downloads.WaitAsync().ConfigureAwait(false);
                    try {return Load(new Uri(origin),entry);}finally{downloads.Release();}
                });
                return entry.Pending;
            }
        }
        string CachePath(Uri origin) {
            using(var sha=SHA256.Create())return Path.Combine(folder,BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(origin.AbsoluteUri))).Replace("-","")+".png");
        }
        bool Load(Uri origin,Entry entry) {
            string path=CachePath(origin);byte[] png=null;
            // A valid disk cache also works offline and avoids repeat requests on startup.
            try {if(File.Exists(path) && new FileInfo(path).Length<=1024*1024)png=Normalize(File.ReadAllBytes(path));}catch { }
            bool fresh=png!=null;
            try {if(png==null)png=FindIcon(origin);}catch { }
            if(png!=null && !fresh)try {Directory.CreateDirectory(folder);File.WriteAllBytes(path,png);}catch { }
            lock(gate) {
                bool changed=png!=null && (entry.Png==null || !png.SequenceEqual(entry.Png));
                if(png!=null)entry.Png=png;
                entry.RetryAt=png==null?DateTime.UtcNow.AddMinutes(5):DateTime.MaxValue;
                return changed;
            }
        }
        byte[] FindIcon(Uri origin) {
            var candidates=new List<Uri>();
            try {
                var page=fetch(origin);
                string html=Encoding.UTF8.GetString(page.Bytes);
                var address=IsWeb(page.Address)?page.Address:origin;
                // Honor relative links and a document base URL after redirects.
                var baseTag=Regex.Match(html,@"<base\b[^>]*>",RegexOptions.IgnoreCase);
                Uri baseUri;if(baseTag.Success && Uri.TryCreate(address,Attribute(baseTag.Value,"href"),out baseUri) && IsWeb(baseUri))address=baseUri;
                foreach(Match link in Regex.Matches(html,@"<link\b[^>]*>",RegexOptions.IgnoreCase)) {
                    string rel=Attribute(link.Value,"rel");
                    if(!Regex.IsMatch(rel,@"(^|\s)(icon|apple-touch-icon|apple-touch-icon-precomposed)(\s|$)",RegexOptions.IgnoreCase))continue;
                    Uri icon;if(Uri.TryCreate(address,Attribute(link.Value,"href"),out icon) && IsWeb(icon) && !icon.AbsolutePath.EndsWith(".svg",StringComparison.OrdinalIgnoreCase))candidates.Add(icon);
                }
                candidates.Add(new Uri(page.Address??origin,"/favicon.ico"));
            }catch { }
            candidates.Add(new Uri(origin,"favicon.ico"));
            foreach(var candidate in candidates.Where(IsWeb).Distinct().Take(8)) {
                try {var png=Normalize(fetch(candidate).Bytes);if(png!=null)return png;}catch { }
            }
            return null;
        }
        static string Attribute(string tag,string name) {
            var match=Regex.Match(tag,@"(?:\s)"+name+@"\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))",RegexOptions.IgnoreCase);
            return WebUtility.HtmlDecode(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Success?match.Groups[2].Value:match.Groups[3].Value);
        }
        static byte[] Normalize(byte[] bytes) {
            if(bytes==null || bytes.Length==0 || bytes.Length>1024*1024)return null;
            try {
                using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream)) {
                    if(image.Width>2048 || image.Height>2048)return null;
                    using(var bitmap=new Bitmap(96,96)) {
                        using(var g=Graphics.FromImage(bitmap)) {
                            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            float scale=Math.Min(96f/image.Width,96f/image.Height);float w=image.Width*scale,h=image.Height*scale;
                            g.DrawImage(image,(96-w)/2,(96-h)/2,w,h);
                        }
                        using(var output=new MemoryStream()){bitmap.Save(output,ImageFormat.Png);return output.ToArray();}
                    }
                }
            }catch {return null;}
        }
        static Resource Download(Uri address) {
            if(!IsWeb(address))throw new InvalidDataException("Unsupported icon URL");
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(address);
            request.Timeout=4000;request.ReadWriteTimeout=4000;request.MaximumAutomaticRedirections=4;
            request.UserAgent="UlanziLite/0.3";request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
            using(var response=(HttpWebResponse)request.GetResponse())using(var stream=response.GetResponseStream())using(var output=new MemoryStream()) {
                if(response.ContentLength>1024*1024)throw new InvalidDataException("Icon response too large");
                var buffer=new byte[8192];int count;
                while((count=stream.Read(buffer,0,buffer.Length))>0){if(output.Length+count>1024*1024)throw new InvalidDataException("Icon response too large");output.Write(buffer,0,count);}
                return new Resource {Bytes=output.ToArray(),Address=response.ResponseUri};
            }
        }
    }
}
