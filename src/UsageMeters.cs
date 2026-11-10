using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

namespace UlanziLite {
    public sealed class UsageReading {
        public string Provider;
        public double? SessionUsed, WeeklyUsed;
        public string ObservedAt,SessionResetAt,WeeklyResetAt,Source;
        public static bool WindowActive(string resets,DateTime now) {
            if(String.IsNullOrEmpty(resets))return true;
            DateTimeOffset time;return DateTimeOffset.TryParse(resets,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out time) && time.UtcDateTime>now;
        }
        public bool IsFresh(DateTime now) {
            DateTimeOffset time;
            return DateTimeOffset.TryParse(ObservedAt,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out time)
                && time.UtcDateTime<=now.AddMinutes(1) && now-time.UtcDateTime<TimeSpan.FromMinutes(15);
        }
        public void Validate() {
            if(Array.IndexOf(UsageMeters.Providers,Provider)<0)throw new ArgumentException("알 수 없는 사용량 서비스입니다.");
            foreach(var value in new[]{SessionUsed,WeeklyUsed})
                if(value.HasValue && (Double.IsNaN(value.Value) || Double.IsInfinity(value.Value) || value<0 || value>100))throw new ArgumentException("사용률은 0~100%여야 합니다.");
            DateTimeOffset time;
            if(!DateTimeOffset.TryParse(ObservedAt,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out time) || time.UtcDateTime>DateTime.UtcNow.AddMinutes(1))throw new ArgumentException("사용량 확인 시간이 올바르지 않습니다.");
        }
    }
    public static class UsageMeters {
        public static readonly string[] Providers={"chatgpt","claude","gemini"};
        public static readonly string[] Names={"ChatGPT","Claude","Gemini"};
        public static readonly string[] Urls={"https://chatgpt.com/codex/settings/usage","https://claude.ai/settings/usage","https://gemini.google.com/"};
        static readonly Color[] Accents={Color.FromArgb(115,221,185),Color.FromArgb(229,170,131),Color.FromArgb(139,178,255)};
        static readonly object gate=new object();
        static readonly Dictionary<string,UsageReading> readings=new Dictionary<string,UsageReading>();
        static readonly string[] statuses={"CLI 연결 대기","CLI 연결 대기","CLI 연결 대기"};
        static int revision;
        public static int Revision {get{lock(gate)return revision;}}
        public static UsageReading Get(int index) {
            lock(gate){UsageReading value;return readings.TryGetValue(Providers[index],out value)?Clone(value):null;}
        }
        static UsageReading Clone(UsageReading value) {return new UsageReading {Provider=value.Provider,SessionUsed=value.SessionUsed,WeeklyUsed=value.WeeklyUsed,ObservedAt=value.ObservedAt,SessionResetAt=value.SessionResetAt,WeeklyResetAt=value.WeeklyResetAt,Source=value.Source};}
        public static void SetStatus(int index,string text) {lock(gate){if(statuses[index]!=text){statuses[index]=text;revision++;}}}
        public static string Status(int index) {lock(gate)return statuses[index];}
        public static void Update(UsageReading value) {
            value.Validate();
            lock(gate){
                UsageReading previous;
                if(readings.TryGetValue(value.Provider,out previous) && DateTimeOffset.Parse(previous.ObservedAt,CultureInfo.InvariantCulture)>=DateTimeOffset.Parse(value.ObservedAt,CultureInfo.InvariantCulture))return;
                readings[value.Provider]=Clone(value);revision++;
            }
        }
        public static KeyBinding Binding(int index) {return new KeyBinding {Label=Names[index]+" 사용량",Target=Urls[index]};}
        public static Bitmap Render(int index) {return Render(index,Get(index),DateTime.UtcNow);}
        public static Bitmap Render(int index,UsageReading reading,DateTime now) {
            var bitmap=new Bitmap(196,196,PixelFormat.Format24bppRgb);
            using(var g=Graphics.FromImage(bitmap))
            using(var title=new Font("Segoe UI",21,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var label=new Font(Typography.UiFontName,13,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var number=new Font(Typography.NumberFontName,27,FontStyle.Regular,GraphicsUnit.Pixel)) {
                g.Clear(Color.FromArgb(11,16,24));g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.SetClip(new Rectangle(0,0,196,WideArtwork.SafeHeight));
                using(var brush=new SolidBrush(Accents[index])){g.FillRectangle(brush,14,14,4,23);g.DrawString(Names[index],title,brush,24,10);}
                bool fresh=reading!=null && reading.IsFresh(now);
                double? first=reading!=null && UsageReading.WindowActive(reading.SessionResetAt,now)?reading.SessionUsed:null;
                double? second=reading!=null && UsageReading.WindowActive(reading.WeeklyResetAt,now)?reading.WeeklyUsed:null;
                DrawWindow(g,label,number,index==2?"Pro":"5시간",first,49,fresh?Accents[index]:Color.FromArgb(117,133,153));
                DrawWindow(g,label,number,index==2?"Flash":"주간",second,104,fresh?Accents[index]:Color.FromArgb(117,133,153));
                string status=Status(index);
                string note=reading==null?status:!fresh?"캐시 · "+DateTimeOffset.Parse(reading.ObservedAt,CultureInfo.InvariantCulture).ToLocalTime().ToString("HH:mm"):!first.HasValue && !second.HasValue?"갱신 대기":(index==0?"Codex":index==1?"Claude":"CLI")+" · "+DateTimeOffset.Parse(reading.ObservedAt,CultureInfo.InvariantCulture).ToLocalTime().ToString("HH:mm");
                using(var brush=new SolidBrush(Color.FromArgb(155,172,192)))
                using(var format=new StringFormat {Alignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap,Trimming=StringTrimming.EllipsisCharacter})
                    g.DrawString(note,label,brush,new RectangleF(6,163,184,22),format);
            }
            return bitmap;
        }
        static void DrawWindow(Graphics g,Font label,Font number,string name,double? used,int y,Color accent) {
            using(var dim=new SolidBrush(Color.FromArgb(155,172,192)))g.DrawString(name,label,dim,14,y+6);
            using(var white=new SolidBrush(Color.White))
            using(var format=new StringFormat {Alignment=StringAlignment.Far,FormatFlags=StringFormatFlags.NoWrap})
                g.DrawString(used.HasValue?Math.Round(used.Value)+"%":"—",number,white,new RectangleF(70,y-4,112,36),format);
            using(var track=new SolidBrush(Color.FromArgb(42,53,69)))g.FillRectangle(track,14,y+32,168,6);
            if(used.HasValue)using(var fill=new SolidBrush(used>=90?Color.FromArgb(255,140,140):used>=75?Color.FromArgb(244,186,112):accent))g.FillRectangle(fill,14,y+32,(float)(168*used.Value/100),6);
        }
    }
}
