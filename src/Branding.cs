using System.Drawing;
using System.Reflection;
using System;
using System.Linq;

[assembly: AssemblyTitle("Ulanzi Lite")]
[assembly: AssemblyDescription("Lightweight Windows controller for Ulanzi D200X")]
[assembly: AssemblyProduct("Ulanzi Lite for D200X")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]

namespace UlanziLite {
    static class Typography {
        public static readonly string UiFontName=Choose("Noto Sans KR","Malgun Gothic");
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
    }
}
