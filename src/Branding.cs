using System.Drawing;
using System.Reflection;
using System;
using System.Linq;

[assembly: AssemblyTitle("Ulanzi Lite")]
[assembly: AssemblyDescription("Lightweight Windows controller for Ulanzi D200X")]
[assembly: AssemblyProduct("Ulanzi Lite for D200X")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]

namespace UlanziLite {
    static class Typography {
        public static readonly string UiFontName=Choose("Segoe UI","Arial");
        public static readonly string NumberFontName=Choose("Bahnschrift","Segoe UI");
        static string Choose(string preferred,string fallback) {
            return FontFamily.Families.Any(f=>String.Equals(f.Name,preferred,StringComparison.OrdinalIgnoreCase))?preferred:fallback;
        }
    }
    static class Branding {
        public static Icon CreateIcon() {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("UlanziLite.Icon"))
            using(var icon=new Icon(stream))return (Icon)icon.Clone();
        }
        // GDI+ misreads PNG-compressed icon frames, so decode the largest frame directly.
        public static Bitmap CreateMark(int size) {
            byte[] data;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("UlanziLite.Icon"))using(var copy=new System.IO.MemoryStream()){stream.CopyTo(copy);data=copy.ToArray();}
            int count=BitConverter.ToUInt16(data,4),best=-1,bestSize=0;
            for(int i=0;i<count;i++){int entry=6+16*i,side=data[entry]==0?256:data[entry];if(side>bestSize){bestSize=side;best=entry;}}
            var mark=new Bitmap(size,size);
            using(var frame=new System.IO.MemoryStream(data,BitConverter.ToInt32(data,best+12),BitConverter.ToInt32(data,best+8)))
            using(var image=Image.FromStream(frame))using(var g=Graphics.FromImage(mark)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(image,0,0,size,size);}
            return mark;
        }
    }
}
