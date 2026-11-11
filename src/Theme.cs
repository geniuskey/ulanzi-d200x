using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace UlanziLite {
    static class Theme {
        static readonly HashSet<string> families=new HashSet<string>(FontFamily.Families.Select(f=>f.Name),StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
        static readonly string semiboldName=families.Contains("Segoe UI Semibold")?"Segoe UI Semibold":null;

        public static float Scale=1f;
        public static int S(int v) {return (int)Math.Round(v*Scale);}

        public static readonly Color Background=Color.FromArgb(11,15,22),Surface=Color.FromArgb(19,25,35),Raised=Color.FromArgb(27,34,47),RaisedHover=Color.FromArgb(35,44,60);
        public static readonly Color Border=Color.FromArgb(37,47,63),BorderStrong=Color.FromArgb(55,68,88),Text=Color.FromArgb(232,238,245),Muted=Color.FromArgb(141,155,176),Subtle=Color.FromArgb(98,112,133);
        public static readonly Color Accent=Color.FromArgb(115,221,185),AccentHover=Color.FromArgb(146,234,205),AccentText=Color.FromArgb(6,30,23),AccentSoft=Color.FromArgb(25,55,50);
        public static readonly Color Warning=Color.FromArgb(244,186,112),Danger=Color.FromArgb(250,140,130);

        public static readonly string IconFont=families.Contains("Segoe Fluent Icons")?"Segoe Fluent Icons":families.Contains("Segoe MDL2 Assets")?"Segoe MDL2 Assets":null;
        public const string ChevronLeft="",ChevronRight="",ChevronDown="",Folder="",Document="",Help="",Play="",Undo="",Reset="";
        public const string Microphone="",Volume="",Mute="",App="",Globe="",Image="",Clear="",Save="",Add="",Remove="",Brightness="",Timer="";

        public static Font GetFont(string family,float points,FontStyle style) {
            string key=family+"|"+points.ToString(CultureInfo.InvariantCulture)+"|"+(int)style;
            Font font;
            if(!fonts.TryGetValue(key,out font)) {font=new Font(family,points,style,GraphicsUnit.Point);fonts[key]=font;}
            return font;
        }
        public static Font Ui(float points) {return GetFont(Typography.UiFontName,points,FontStyle.Regular);}
        public static Font Semibold(float points) {return semiboldName!=null?GetFont(semiboldName,points,FontStyle.Regular):GetFont(Typography.UiFontName,points,FontStyle.Bold);}
        public static Font Number(float points) {return GetFont(Typography.NumberFontName,points,FontStyle.Regular);}

        public static GraphicsPath RoundRect(RectangleF bounds,float radius) {
            var path=new GraphicsPath();
            if(bounds.Width<=0 || bounds.Height<=0)return path;
            radius=Math.Min(radius,Math.Min(bounds.Width,bounds.Height)/2);
            if(radius<=0) {path.AddRectangle(bounds);return path;}
            float d=radius*2;
            path.AddArc(bounds.X,bounds.Y,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Y,d,d,270,90);
            path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.X,bounds.Bottom-d,d,d,90,90);
            path.CloseFigure();return path;
        }

        public static void Prepare(Graphics g) {g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.Half;}
        public static Color ParentBack(Control control) {
            for(var p=control.Parent;p!=null;p=p.Parent)if(p.BackColor.A==255)return p.BackColor;
            return Background;
        }
        public static Color Blend(Color a,Color b,float t) {
            return Color.FromArgb((int)Math.Round(a.R+(b.R-a.R)*t),(int)Math.Round(a.G+(b.G-a.G)*t),(int)Math.Round(a.B+(b.B-a.B)*t));
        }
        public static void FillRound(Graphics g,RectangleF bounds,float radius,Color color) {
            using(var path=RoundRect(bounds,radius))using(var brush=new SolidBrush(color))g.FillPath(brush,path);
        }
        public static void StrokeRound(Graphics g,RectangleF bounds,float radius,Color color,float width) {
            using(var path=RoundRect(bounds,radius))using(var pen=new Pen(color,width))g.DrawPath(pen,path);
        }
        public static void DrawBorder(Graphics g,RectangleF bounds,float radius,Color color) {
            StrokeRound(g,new RectangleF(bounds.X+0.5f,bounds.Y+0.5f,bounds.Width-1,bounds.Height-1),radius-0.5f,color,1);
        }
        public static void FillCircle(Graphics g,float cx,float cy,float diameter,Color color) {
            using(var brush=new SolidBrush(color))g.FillEllipse(brush,cx-diameter/2,cy-diameter/2,diameter,diameter);
        }

        public static void DrawGlyph(Graphics g,string glyph,float points,Color color,Rectangle bounds) {
            if(IconFont==null || String.IsNullOrEmpty(glyph))return;
            TextRenderer.DrawText(g,glyph,GetFont(IconFont,points,FontStyle.Regular),bounds,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
        }

        public static void DrawLabel(Graphics g,string glyph,float glyphPoints,string text,Font font,Color glyphColor,Color textColor,Rectangle bounds) {
            const TextFormatFlags flags=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter;
            bool hasGlyph=IconFont!=null && !String.IsNullOrEmpty(glyph),hasText=!String.IsNullOrEmpty(text);
            if(!hasGlyph) {if(hasText)TextRenderer.DrawText(g,text,font,bounds,textColor,flags|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);return;}
            if(!hasText) {DrawGlyph(g,glyph,glyphPoints+2,glyphColor,bounds);return;}
            var glyphFont=GetFont(IconFont,glyphPoints,FontStyle.Regular);
            int gw=TextRenderer.MeasureText(g,glyph,glyphFont,Size.Empty,flags).Width,gap=S(6);
            int tw=Math.Min(TextRenderer.MeasureText(g,text,font,Size.Empty,flags).Width,Math.Max(0,bounds.Width-gw-gap));
            int x=bounds.X+Math.Max(0,(bounds.Width-gw-gap-tw)/2);
            TextRenderer.DrawText(g,glyph,glyphFont,new Rectangle(x,bounds.Y,gw,bounds.Height),glyphColor,flags|TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g,text,font,new Rectangle(x+gw+gap,bounds.Y,tw,bounds.Height),textColor,flags|TextFormatFlags.EndEllipsis);
        }

        public static void PaintParent(Control control,Graphics g) {
            using(var b=new SolidBrush(ParentBack(control)))g.FillRectangle(b,control.ClientRectangle);
        }
    }

    enum Tone {Neutral,Positive,Warning,Danger}

    class KeyButton : Button {
        bool selected,hover,pressed;Bitmap cache;Image cacheSource;

        public KeyButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;TabStop=true;
        }

        public bool Selected {get {return selected;} set {if(selected==value)return;selected=value;Invalidate();}}

        protected override void OnMouseEnter(EventArgs e) {hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e) {hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e) {if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e) {pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnGotFocus(EventArgs e) {Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e) {Invalidate();base.OnLostFocus(e);}
        protected override void OnBackgroundImageChanged(EventArgs e) {DropCache();Invalidate();base.OnBackgroundImageChanged(e);}

        void DropCache() {if(cache!=null)cache.Dispose();cache=null;cacheSource=null;}

        Bitmap Scaled(Image source,Size size) {
            if(cache!=null && cacheSource==source && cache.Size==size)return cache;
            DropCache();
            cache=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(cache))using(var attributes=new ImageAttributes()) {
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source,new Rectangle(Point.Empty,size),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
            }
            cacheSource=source;return cache;
        }

        // ButtonBase is opaque and never raises OnPaintBackground, so clear the buffer here.
        protected override void OnPaintBackground(PaintEventArgs e) {}

        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintParent(this,g);Theme.Prepare(g);
            int inset=Theme.S(4);
            var image=Rectangle.Inflate(ClientRectangle,-inset,-inset);
            if(image.Width<=0 || image.Height<=0)return;
            float radius=Theme.S(14);
            using(var path=Theme.RoundRect(image,radius)) {
                var source=BackgroundImage;
                if(source==null)using(var b=new SolidBrush(Theme.Raised))g.FillPath(b,path);
                else using(var brush=new TextureBrush(Scaled(source,image.Size),WrapMode.Clamp)) {brush.TranslateTransform(image.X,image.Y);g.FillPath(brush,path);}
                if(pressed && hover)using(var b=new SolidBrush(Color.FromArgb(40,0,0,0)))g.FillPath(b,path);
            }
            var ring=RectangleF.Inflate(image,Theme.S(2),Theme.S(2));
            if(selected)Theme.StrokeRound(g,ring,radius+2,Theme.Accent,2.5f*Theme.Scale);
            else {
                if(hover)Theme.StrokeRound(g,ring,radius+2,Color.FromArgb(90,108,132),1.5f*Theme.Scale);
                if(Focused && ShowFocusCues)
                    using(var path=Theme.RoundRect(ring,radius+2))using(var pen=new Pen(Color.FromArgb(128,Theme.Accent),1.5f*Theme.Scale) {DashStyle=DashStyle.Dash})g.DrawPath(pen,path);
            }
            if(Text=="●") {
                float d=Theme.S(12),cx=image.Right-Theme.S(10),cy=image.Top+Theme.S(10);
                Theme.FillCircle(g,cx,cy,d+4*Theme.Scale,Theme.ParentBack(this));
                Theme.FillCircle(g,cx,cy,d,Theme.Warning);
            }
        }

        protected override void Dispose(bool disposing) {if(disposing)DropCache();base.Dispose(disposing);}
    }

    class Card : Panel {
        int radius;Color borderColor;bool flat;

        public Card() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Surface;radius=Theme.S(14);borderColor=Theme.Border;
        }

        public int Radius {get {return radius;} set {radius=value;Invalidate();}}
        public Color BorderColor {get {return borderColor;} set {borderColor=value;Invalidate();}}
        public bool Flat {get {return flat;} set {flat=value;Invalidate();}}

        protected override void OnPaintBackground(PaintEventArgs e) {
            var g=e.Graphics;
            Theme.PaintParent(this,g);
            Theme.Prepare(g);
            var bounds=new RectangleF(0,0,Width,Height);
            Theme.FillRound(g,bounds,radius,BackColor);
            if(!flat)Theme.DrawBorder(g,bounds,radius,borderColor);
        }
    }

    class AudioTile : Card {
        string title,glyph,detail;float level;bool muted,available;

        public AudioTile() {BackColor=Theme.Raised;Radius=Theme.S(12);BorderColor=Theme.Border;}

        public string Title {get {return title;} set {title=value;Invalidate();}}
        public string Glyph {get {return glyph;} set {glyph=value;Invalidate();}}
        public float Level {get {return level;} set {level=Math.Max(0f,Math.Min(1f,value));Invalidate();}}
        public bool Muted {get {return muted;} set {muted=value;Invalidate();}}
        public bool Available {get {return available;} set {available=value;Invalidate();}}
        public string Detail {get {return detail;} set {detail=value;Invalidate();}}

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            var g=e.Graphics;Theme.Prepare(g);
            int p=Theme.S(14),w=Width;
            const TextFormatFlags line=TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis;
            Theme.DrawGlyph(g,glyph,11f,muted?Theme.Danger:Theme.Accent,new Rectangle(p,p,Theme.S(18),Theme.S(18)));
            TextRenderer.DrawText(g,title,Theme.Semibold(10f),new Rectangle(Theme.S(40),Theme.S(12),Math.Max(0,w-Theme.S(40)-Theme.S(90)),Theme.S(22)),Theme.Text,line);
            var value=new Rectangle(w-p-Theme.S(86),Theme.S(8),Theme.S(86),Theme.S(30));
            if(!available)TextRenderer.DrawText(g,"—",Theme.Number(17f),value,Theme.Subtle,line|TextFormatFlags.Right);
            else if(muted)TextRenderer.DrawText(g,"Muted",Theme.Semibold(10f),value,Theme.Danger,line|TextFormatFlags.Right);
            else TextRenderer.DrawText(g,Math.Round(level*100)+"%",Theme.Number(17f),value,Theme.Text,line|TextFormatFlags.Right);
            TextRenderer.DrawText(g,detail,Theme.Ui(8.5f),new Rectangle(p,Theme.S(38),Math.Max(0,w-2*p),Theme.S(18)),Theme.Muted,line);
            int bar=Theme.S(6),barWidth=Math.Max(0,w-2*p);
            var track=new RectangleF(p,Theme.S(62),barWidth,bar);
            Theme.FillRound(g,track,bar/2f,Theme.Background);
            float filled=available?level*barWidth:0;
            if(filled>0)Theme.FillRound(g,new RectangleF(p,track.Y,filled,bar),bar/2f,!available?Theme.Subtle:muted?Theme.Danger:Theme.Accent);
        }
    }

    class FieldBox : Panel {
        public readonly TextBox Input;
        bool invalid,hover;

        public FieldBox(string accessibleName) {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            BackColor=Theme.Raised;Cursor=Cursors.IBeam;
            Input=new TextBox {BorderStyle=BorderStyle.None,BackColor=Theme.Raised,ForeColor=Theme.Text,AccessibleName=accessibleName,Font=Theme.Ui(10f)};
            Input.GotFocus+=(s,e)=>Invalidate();Input.LostFocus+=(s,e)=>Invalidate();
            Input.MouseEnter+=(s,e)=>UpdateHover();Input.MouseLeave+=(s,e)=>UpdateHover();
            Controls.Add(Input);
        }

        public bool Invalid {get {return invalid;} set {if(invalid==value)return;invalid=value;Invalidate();}}

        void UpdateHover() {
            bool now=IsHandleCreated && ClientRectangle.Contains(PointToClient(Cursor.Position));
            if(now!=hover) {hover=now;Invalidate();}
        }

        protected override void OnMouseEnter(EventArgs e) {UpdateHover();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e) {UpdateHover();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e) {Input.Focus();base.OnMouseDown(e);}

        protected override void OnLayout(LayoutEventArgs levent) {
            base.OnLayout(levent);
            Input.SetBounds(Theme.S(10),Math.Max(0,(Height-Input.Height)/2),Math.Max(0,Width-Theme.S(20)),Input.Height);
        }

        protected override void OnPaintBackground(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintParent(this,g);Theme.Prepare(g);
            var bounds=new RectangleF(0,0,Width,Height);
            int r=Theme.S(8);
            Theme.FillRound(g,bounds,r,Theme.Raised);
            Theme.DrawBorder(g,bounds,r,invalid?Theme.Danger:Input.Focused?Theme.Accent:hover?Theme.BorderStrong:Theme.Border);
        }
    }

    class SegmentedControl : Control {
        readonly string[] items;string[] glyphs;int selectedIndex=-1,hoverIndex=-1;
        public event EventHandler SelectedIndexChanged;

        public SegmentedControl(params string[] items) {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
            this.items=items??new string[0];TabStop=true;Cursor=Cursors.Hand;Font=Theme.Ui(9.5f);
        }

        public string[] Items {get {return items;}}
        public string[] Glyphs {get {return glyphs;} set {glyphs=value;Invalidate();}}
        public int SelectedIndex {
            get {return selectedIndex;}
            set {
                int v=Math.Max(-1,Math.Min(items.Length-1,value));
                if(v==selectedIndex)return;
                selectedIndex=v;Invalidate();
                if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);
            }
        }

        int IndexAt(int x) {
            if(items.Length==0 || Width<=0)return -1;
            return Math.Max(0,Math.Min(items.Length-1,x*items.Length/Width));
        }

        protected override bool IsInputKey(Keys keyData) {
            var k=keyData&Keys.KeyCode;
            return k==Keys.Left || k==Keys.Right || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if(e.KeyCode==Keys.Left && items.Length>0) {SelectedIndex=Math.Max(0,selectedIndex-1);e.Handled=true;}
            else if(e.KeyCode==Keys.Right && items.Length>0) {SelectedIndex=Math.Min(items.Length-1,selectedIndex+1);e.Handled=true;}
            base.OnKeyDown(e);
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            if(e.Button==MouseButtons.Left) {Focus();int i=IndexAt(e.X);if(i>=0)SelectedIndex=i;}
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e) {int i=IndexAt(e.X);if(i!=hoverIndex){hoverIndex=i;Invalidate();}base.OnMouseMove(e);}
        protected override void OnMouseLeave(EventArgs e) {hoverIndex=-1;Invalidate();base.OnMouseLeave(e);}
        protected override void OnGotFocus(EventArgs e) {Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e) {Invalidate();base.OnLostFocus(e);}

        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintParent(this,g);Theme.Prepare(g);
            var bounds=new RectangleF(0,0,Width,Height);
            int r=Theme.S(8),inset=Theme.S(3);
            Theme.FillRound(g,bounds,r,Theme.Background);
            Theme.DrawBorder(g,bounds,r,Theme.Border);
            if(Focused)Theme.StrokeRound(g,new RectangleF(0.75f,0.75f,Width-1.5f,Height-1.5f),r-0.75f,Theme.Accent,1.5f);
            int n=items.Length;
            for(int i=0;i<n;i++) {
                int x0=i*Width/n,x1=(i+1)*Width/n;
                var segment=new Rectangle(x0,0,x1-x0,Height);
                bool on=i==selectedIndex;
                if(on) {
                    var pill=new RectangleF(x0+inset,inset,x1-x0-2*inset,Height-2*inset);
                    Theme.FillRound(g,pill,Theme.S(6),Theme.Raised);
                    Theme.DrawBorder(g,pill,Theme.S(6),Theme.BorderStrong);
                }
                var color=on || i==hoverIndex?Theme.Text:Theme.Muted;
                string glyph=glyphs!=null && i<glyphs.Length?glyphs[i]:null;
                Theme.DrawLabel(g,glyph,Font.SizeInPoints,items[i],on?Theme.Semibold(Font.SizeInPoints):Font,color,color,Rectangle.Inflate(segment,-Theme.S(6),0));
            }
        }
    }

    class Stepper : Control {
        int minimum,maximum=100,value,hoverArea=-1,repeatStep;string suffix="";
        readonly System.Windows.Forms.Timer repeat=new System.Windows.Forms.Timer();
        public event EventHandler ValueChanged;

        public Stepper() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
            TabStop=true;Font=Theme.Ui(10f);
            repeat.Tick+=(s,e)=>{repeat.Interval=70;Step(repeatStep);};
        }

        public int Minimum {get {return minimum;} set {minimum=value;if(maximum<minimum)maximum=minimum;Value=this.value;Invalidate();}}
        public int Maximum {get {return maximum;} set {maximum=value;if(minimum>maximum)minimum=maximum;Value=this.value;Invalidate();}}
        public string Suffix {get {return suffix;} set {suffix=value??"";Invalidate();}}
        public int Value {
            get {return value;}
            set {
                int v=Math.Max(minimum,Math.Min(maximum,value));
                if(v==this.value)return;
                this.value=v;Invalidate();
                if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);
            }
        }

        void Step(int delta) {Value=value+delta;}
        int AreaAt(int x) {int b=Math.Min(Height,Width/2);return x<b?0:x>=Width-b?1:2;}
        void StopRepeat() {repeat.Stop();}

        protected override bool IsInputKey(Keys keyData) {
            var k=keyData&Keys.KeyCode;
            return k==Keys.Up || k==Keys.Down || k==Keys.Left || k==Keys.Right || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if(e.KeyCode==Keys.Up || e.KeyCode==Keys.Right) {Step(1);e.Handled=true;}
            else if(e.KeyCode==Keys.Down || e.KeyCode==Keys.Left) {Step(-1);e.Handled=true;}
            base.OnKeyDown(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e) {Step(e.Delta>0?1:e.Delta<0?-1:0);base.OnMouseWheel(e);}
        protected override void OnMouseDown(MouseEventArgs e) {
            if(e.Button==MouseButtons.Left) {
                Focus();int area=AreaAt(e.X);
                if(area!=2) {repeatStep=area==0?-1:1;Step(repeatStep);repeat.Interval=400;repeat.Start();}
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) {StopRepeat();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e) {StopRepeat();base.OnMouseCaptureChanged(e);}
        protected override void OnMouseMove(MouseEventArgs e) {int a=AreaAt(e.X);if(a!=hoverArea){hoverArea=a;Invalidate();}base.OnMouseMove(e);}
        protected override void OnMouseLeave(EventArgs e) {hoverArea=-1;Invalidate();base.OnMouseLeave(e);}
        protected override void OnGotFocus(EventArgs e) {Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e) {StopRepeat();Invalidate();base.OnLostFocus(e);}
        protected override void OnEnabledChanged(EventArgs e) {StopRepeat();Invalidate();base.OnEnabledChanged(e);}

        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintParent(this,g);Theme.Prepare(g);
            var bounds=new RectangleF(0,0,Width,Height);
            int r=Theme.S(8),b=Math.Min(Height,Width/2);
            Theme.FillRound(g,bounds,r,Theme.Raised);
            Theme.DrawBorder(g,bounds,r,Focused?Theme.Accent:hoverArea>=0?Theme.BorderStrong:Theme.Border);
            using(var pen=new Pen(Theme.Border,1)) {
                float inset=Theme.S(6);
                g.DrawLine(pen,b-0.5f,inset,b-0.5f,Height-inset);
                g.DrawLine(pen,Width-b+0.5f,inset,Width-b+0.5f,Height-inset);
            }
            DrawButton(g,new Rectangle(0,0,b,Height),Theme.Remove,"−",hoverArea==0,value<=minimum);
            DrawButton(g,new Rectangle(Width-b,0,b,Height),Theme.Add,"+",hoverArea==1,value>=maximum);
            TextRenderer.DrawText(g,value+suffix,Theme.Number(11f),new Rectangle(b,0,Math.Max(0,Width-2*b),Height),Enabled?Theme.Text:Theme.Subtle,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
        }

        void DrawButton(Graphics g,Rectangle area,string glyph,string fallback,bool over,bool atLimit) {
            var color=atLimit || !Enabled?Theme.Subtle:over?Theme.Text:Theme.Muted;
            if(Theme.IconFont!=null)Theme.DrawGlyph(g,glyph,9f,color,area);
            else TextRenderer.DrawText(g,fallback,Theme.Ui(12f),area,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing) {if(disposing)repeat.Dispose();base.Dispose(disposing);}
    }

    class Slider : Control {
        int minimum,maximum=100,value;bool hover,dragging;
        public event EventHandler ValueChanged,ValueCommitted;

        public Slider() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
            TabStop=true;Cursor=Cursors.Hand;
        }

        public int Minimum {get {return minimum;} set {minimum=value;if(maximum<minimum)maximum=minimum;Value=this.value;Invalidate();}}
        public int Maximum {get {return maximum;} set {maximum=value;if(minimum>maximum)minimum=maximum;Value=this.value;Invalidate();}}
        public int Value {
            get {return value;}
            set {
                int v=Math.Max(minimum,Math.Min(maximum,value));
                if(v==this.value)return;
                this.value=v;Invalidate();
                if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);
            }
        }

        float Radius {get {return Theme.S(16)/2f;}}
        float Fraction {get {return maximum>minimum?(float)(value-minimum)/(maximum-minimum):0;}}

        void Commit() {if(ValueCommitted!=null)ValueCommitted(this,EventArgs.Empty);}
        void Change(int v) {int old=value;Value=v;if(value!=old)Commit();}
        void SetFromX(int x) {
            float span=Width-2*Radius;
            float t=span>0?Math.Max(0,Math.Min(1,(x-Radius)/span)):0;
            Value=minimum+(int)Math.Round(t*(maximum-minimum));
        }

        protected override bool IsInputKey(Keys keyData) {
            var k=keyData&Keys.KeyCode;
            return k==Keys.Left || k==Keys.Right || k==Keys.PageUp || k==Keys.PageDown || k==Keys.Home || k==Keys.End || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            switch(e.KeyCode) {
                case Keys.Left:Change(value-1);e.Handled=true;break;
                case Keys.Right:Change(value+1);e.Handled=true;break;
                case Keys.PageDown:Change(value-10);e.Handled=true;break;
                case Keys.PageUp:Change(value+10);e.Handled=true;break;
                case Keys.Home:Change(minimum);e.Handled=true;break;
                case Keys.End:Change(maximum);e.Handled=true;break;
            }
            base.OnKeyDown(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e) {if(e.Delta!=0)Change(value+(e.Delta>0?5:-5));base.OnMouseWheel(e);}
        protected override void OnMouseDown(MouseEventArgs e) {
            if(e.Button==MouseButtons.Left) {Focus();dragging=true;Capture=true;SetFromX(e.X);Invalidate();}
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e) {if(dragging)SetFromX(e.X);base.OnMouseMove(e);}
        protected override void OnMouseUp(MouseEventArgs e) {
            if(dragging) {dragging=false;Capture=false;Invalidate();Commit();}
            base.OnMouseUp(e);
        }
        protected override void OnMouseCaptureChanged(EventArgs e) {
            if(dragging) {dragging=false;Invalidate();Commit();}
            base.OnMouseCaptureChanged(e);
        }
        protected override void OnMouseEnter(EventArgs e) {hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e) {hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnGotFocus(EventArgs e) {Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e) {Invalidate();base.OnLostFocus(e);}

        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintParent(this,g);Theme.Prepare(g);
            float rad=Radius,span=Math.Max(0,Width-2*rad),cy=Height/2f,track=Theme.S(4);
            var trackRect=new RectangleF(rad,cy-track/2,span,track);
            Theme.FillRound(g,trackRect,track/2,Theme.Border);
            float x=rad+Fraction*span;
            if(x>rad)Theme.FillRound(g,new RectangleF(rad,trackRect.Y,x-rad,track),track/2,Enabled?Theme.Accent:Theme.Subtle);
            Theme.FillCircle(g,x,cy,rad*2,Enabled?Theme.Text:Theme.Muted);
            bool active=Enabled && (hover || dragging || Focused);
            float ring=active?3:2;
            using(var pen=new Pen(active?Theme.Accent:Theme.BorderStrong,ring))g.DrawEllipse(pen,x-rad+ring/2,cy-rad+ring/2,rad*2-ring,rad*2-ring);
        }
    }

    class StatusPill : Control {
        Tone tone;

        public StatusPill() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            Font=Theme.Ui(9f);
        }

        public Tone Tone {get {return tone;} set {tone=value;Invalidate();}}

        protected override void OnTextChanged(EventArgs e) {Invalidate();base.OnTextChanged(e);}

        public int PreferredWidth() {
            return Theme.S(12)+Theme.S(8)+Theme.S(8)+TextRenderer.MeasureText(Text??"",Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width+Theme.S(14);
        }

        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintParent(this,g);Theme.Prepare(g);
            var bounds=new RectangleF(0,0,Width,Height);
            Theme.FillRound(g,bounds,Height/2f,Theme.Raised);
            Theme.DrawBorder(g,bounds,Height/2f,Theme.Border);
            var dot=tone==Tone.Positive?Theme.Accent:tone==Tone.Warning?Theme.Warning:tone==Tone.Danger?Theme.Danger:Theme.Subtle;
            float d=Theme.S(8),cx=Theme.S(12)+d/2,cy=Height/2f;
            if(tone==Tone.Positive)Theme.FillCircle(g,cx,cy,Theme.S(14),Color.FromArgb(60,dot));
            Theme.FillCircle(g,cx,cy,d,dot);
            int x=Theme.S(12)+Theme.S(8)+Theme.S(8);
            TextRenderer.DrawText(g,Text,Font,new Rectangle(x,0,Math.Max(0,Width-x-Theme.S(10)),Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
        }
    }

    class RoundedPictureBox : PictureBox {
        int radius;

        public RoundedPictureBox() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            radius=Theme.S(12);
        }

        public int Radius {get {return radius;} set {radius=value;Invalidate();}}

        protected override void OnPaintBackground(PaintEventArgs e) {}

        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintParent(this,g);Theme.Prepare(g);
            var bounds=new RectangleF(0,0,Width,Height);
            Theme.FillRound(g,bounds,radius,Theme.Background);
            var image=Image;
            if(image!=null && image.Width>0 && image.Height>0 && Width>0 && Height>0) {
                float scale=Math.Min((float)Width/image.Width,(float)Height/image.Height);
                float w=image.Width*scale,h=image.Height*scale;
                var target=new RectangleF((Width-w)/2,(Height-h)/2,w,h);
                using(var path=Theme.RoundRect(bounds,radius)) {
                    var state=g.Save();
                    g.SetClip(path);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image,target);
                    g.Restore(state);
                }
            }
            Theme.DrawBorder(g,bounds,radius,Theme.Border);
        }
    }
}
