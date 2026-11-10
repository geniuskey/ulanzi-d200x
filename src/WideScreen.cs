using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace UlanziLite {
    // The timer and HID callbacks use this on the UI thread. Time is monotonic.
    public sealed class WideScreenState {
        public int HoldMilliseconds=3000;
        public int Version {get;private set;}
        public int ActiveKnob {get;private set;}
        public bool SentAudio {get;private set;}
        int sentVersion=-1; long until,lastClockSecond=-1;
        public void Touch(long now,int knob) {until=now+HoldMilliseconds;ActiveKnob=knob;Version++;}
        public bool WantsAudio(long now) {return now<until;}
        public bool NeedsUpdate(long now,long clockSecond=-1) {return WantsAudio(now)?(!SentAudio || sentVersion!=Version):(SentAudio || (clockSecond>=0 && lastClockSecond!=clockSecond));}
        public void MarkSent(bool audio,int version,long clockSecond=-1) {SentAudio=audio;sentVersion=version;if(!audio)lastClockSecond=clockSecond;}
        public void Reset() {SentAudio=false;sentVersion=-1;lastClockSecond=-1;}
    }
    public static class WideArtwork {
        // The bottom edge is obscured at the normal viewing angle: reserve
        // ceil(196 * 5%) = 10 rows on every device display.
        public const int SafeHeight=186;
        static readonly Color Background=Color.FromArgb(11,16,24);
        static readonly Color Accent=Color.FromArgb(115,221,185);
        static void Text(Graphics g,string value,Font font,Color color,Rectangle rect) {
            using(var brush=new SolidBrush(color))using(var format=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(value,font,brush,rect,format);
        }
        public static Bitmap Audio(AudioState[] states,int active,string appName) {
            if(states==null || states.Length!=3)throw new ArgumentException("Three audio states are required");
            var bitmap=new Bitmap(392,196,PixelFormat.Format24bppRgb);
            using(var g=Graphics.FromImage(bitmap))using(var label=new Font(Typography.UiFontName,15,FontStyle.Bold))using(var number=new Font(Typography.NumberFontName,31,FontStyle.Regular))using(var detail=new Font(Typography.UiFontName,10)) {
                g.Clear(Background);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.SetClip(new Rectangle(0,0,392,SafeHeight));
                string[] names={"마이크","현재 앱","전체 출력"};
                for(int i=0;i<3;i++) {
                    var state=states[i]??new AudioState();int x=i*130;Color color=state.Muted?Color.FromArgb(255,140,140):Accent;
                    if(i==active)using(var brush=new SolidBrush(Color.FromArgb(26,44,48)))g.FillRectangle(brush,x+3,7,126,174);
                    Text(g,names[i],label,i==active?Accent:Color.FromArgb(178,191,209),new Rectangle(x+2,13,128,36));
                    Text(g,state.Available?Math.Round(state.Level*100)+"%":"—",number,state.Available?Color.White:Color.Gray,new Rectangle(x+1,51,129,62));
                    using(var brush=new SolidBrush(Color.FromArgb(48,60,73)))g.FillRectangle(brush,x+15,124,102,7);
                    if(state.Available)using(var brush=new SolidBrush(color))g.FillRectangle(brush,x+15,124,(int)Math.Round(102*Math.Max(0,Math.Min(1,state.Level))),7);
                    string note=!state.Available?"연결 / 소리 대기":state.Muted?"음소거":i==1?(appName??"앱"):"";
                    Text(g,note,detail,state.Muted?color:Color.FromArgb(168,184,201),new Rectangle(x+5,142,120,33));
                }
            }return bitmap;
        }
        public static Bitmap Clock(DateTime time) {
            var bitmap=new Bitmap(392,196,PixelFormat.Format24bppRgb);
            using(var g=Graphics.FromImage(bitmap))
            using(var number=new Font(Typography.NumberFontName,104,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var seconds=new Font(Typography.NumberFontName,46,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var date=new Font(Typography.UiFontName,19,FontStyle.Regular,GraphicsUnit.Pixel)) {
                g.Clear(Background);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.SetClip(new Rectangle(0,0,392,SafeHeight));
                string timeText=time.ToString("HH:mm");
                float fittedSize=Math.Min(number.Size,number.Size*280/g.MeasureString(timeText,number).Width);
                using(var fitted=new Font(Typography.NumberFontName,fittedSize,FontStyle.Regular,GraphicsUnit.Pixel))
                    Text(g,timeText,fitted,Color.White,new Rectangle(5,18,292,126));
                Text(g,time.ToString("ss"),seconds,Accent,new Rectangle(296,53,86,63));
                Text(g,time.ToString("yyyy.MM.dd  ddd"),date,Color.FromArgb(168,184,201),new Rectangle(10,137,372,35));
            }return bitmap;
        }
        // Firmware stretches a square PNG across the two-cell wide display.
        public static byte[] Encode(Bitmap wide) {
            using(var native=new Bitmap(196,196,PixelFormat.Format24bppRgb))using(var g=Graphics.FromImage(native))using(var memory=new MemoryStream()) {
                g.Clear(Background);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(wide,new Rectangle(0,0,196,196));
                // Resampling may blend edge pixels; enforce the safe area on
                // the final firmware image as well as on the source artwork.
                using(var brush=new SolidBrush(Background))g.FillRectangle(brush,0,SafeHeight,196,196-SafeHeight);
                native.Save(memory,ImageFormat.Png);return memory.ToArray();
            }
        }
        public static byte[] Blank() {using(var image=new Bitmap(196,196,PixelFormat.Format24bppRgb))using(var memory=new MemoryStream()){image.Save(memory,ImageFormat.Png);return memory.ToArray();}}
    }
}
