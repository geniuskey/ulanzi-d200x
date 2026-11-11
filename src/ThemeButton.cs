using System.Drawing;
using System.Windows.Forms;

namespace UlanziLite {
    enum ButtonKind {Secondary,Primary,Ghost}

    class ThemeButton : Button {
        ButtonKind kind;string glyph;int radius;bool hover,pressed;

        public ThemeButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            radius=Theme.S(8);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;
            BackColor=Theme.Raised;ForeColor=Theme.Text;Cursor=Cursors.Hand;
        }

        public ButtonKind Kind {get {return kind;} set {kind=value;Invalidate();}}
        public string Glyph {get {return glyph;} set {glyph=value;Invalidate();}}
        public int Radius {get {return radius;} set {radius=value;Invalidate();}}

        protected override void OnMouseEnter(System.EventArgs e) {hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(System.EventArgs e) {hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e) {if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e) {pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnEnabledChanged(System.EventArgs e) {hover=pressed=false;Invalidate();base.OnEnabledChanged(e);}
        protected override void OnGotFocus(System.EventArgs e) {Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(System.EventArgs e) {Invalidate();base.OnLostFocus(e);}

        // ButtonBase is opaque and never raises OnPaintBackground, so clear the buffer here.
        protected override void OnPaintBackground(PaintEventArgs e) {}

        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;Theme.PaintParent(this,g);Theme.Prepare(g);
            bool on=Enabled,over=on && hover,down=over && pressed;
            Color fill=Color.Empty,border=Color.Empty,text;
            var font=Font;
            if(kind==ButtonKind.Primary) {
                fill=!on?Theme.AccentSoft:down?Theme.Blend(Theme.Accent,Color.Black,0.14f):over?Theme.AccentHover:Theme.Accent;
                text=on?Theme.AccentText:Theme.Blend(Theme.Subtle,Theme.Accent,0.25f);
                font=Theme.Semibold(Font.SizeInPoints);
            } else if(kind==ButtonKind.Ghost) {
                if(down)fill=Theme.RaisedHover;else if(over)fill=Theme.Raised;
                text=!on?Theme.Subtle:over?Theme.Text:Theme.Muted;
            } else {
                fill=!on?Theme.Blend(Theme.Surface,Theme.Raised,0.5f):down?Theme.Border:over?Theme.RaisedHover:Theme.Raised;
                border=!on?Theme.Border:over?Theme.Blend(Theme.BorderStrong,Theme.Muted,0.35f):Theme.BorderStrong;
                text=on?Theme.Text:Theme.Subtle;
            }
            var bounds=new Rectangle(0,0,Width,Height);
            if(!fill.IsEmpty)Theme.FillRound(g,bounds,radius,fill);
            if(!border.IsEmpty)Theme.DrawBorder(g,bounds,radius,border);
            Theme.DrawLabel(g,glyph,Font.SizeInPoints,Text,font,text,text,new Rectangle(Theme.S(8),0,Width-Theme.S(16),Height));
            if(Focused && ShowFocusCues)Theme.StrokeRound(g,new RectangleF(2,2,Width-4,Height-4),radius-2,Theme.Accent,2);
        }
    }
}
