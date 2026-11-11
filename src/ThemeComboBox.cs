using System;
using System.Drawing;
using System.Windows.Forms;

namespace UlanziLite {
    // The closed field is fully owner-painted; the native popup list keeps its keyboard behavior.
    class ThemeComboBox : ComboBox {
        bool hover;

        public ThemeComboBox() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            DropDownStyle=ComboBoxStyle.DropDownList;FlatStyle=FlatStyle.Flat;DrawMode=DrawMode.OwnerDrawFixed;
            BackColor=Theme.Raised;ForeColor=Theme.Text;Font=Theme.Ui(10f);ItemHeight=Theme.S(26);
        }

        public virtual void DrawContent(Graphics graphics,Rectangle bounds,int index,Color color) {
            TextRenderer.DrawText(graphics,index<0 || index>=Items.Count?"":Items[index].ToString(),Font,bounds,color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }

        protected override void OnDrawItem(DrawItemEventArgs e) {
            bool selected=(e.State&DrawItemState.Selected)!=0 && (e.State&DrawItemState.ComboBoxEdit)==0;
            using(var b=new SolidBrush(selected?Theme.AccentSoft:Theme.Raised))e.Graphics.FillRectangle(b,e.Bounds);
            if(e.Index>=0)DrawContent(e.Graphics,new Rectangle(e.Bounds.X+Theme.S(6),e.Bounds.Y,Math.Max(0,e.Bounds.Width-Theme.S(6)),e.Bounds.Height),e.Index,Theme.Text);
        }

        protected override void OnMouseEnter(EventArgs e) {hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e) {hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnDropDown(EventArgs e) {Invalidate();base.OnDropDown(e);}
        protected override void OnDropDownClosed(EventArgs e) {Invalidate();base.OnDropDownClosed(e);}
        protected override void OnGotFocus(EventArgs e) {Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e) {Invalidate();base.OnLostFocus(e);}
        protected override void OnSelectedIndexChanged(EventArgs e) {Invalidate();base.OnSelectedIndexChanged(e);}

        protected override void OnPaintBackground(PaintEventArgs e) {}
        protected override void OnPaint(PaintEventArgs e) {PaintField(e.Graphics);}

        void PaintField(Graphics g) {
            var client=ClientRectangle;
            if(client.Width<=0 || client.Height<=0)return;
            Theme.Prepare(g);
            bool open=DroppedDown;
            int r=Theme.S(8),chevron=Theme.S(32);
            using(var b=new SolidBrush(Theme.ParentBack(this)))g.FillRectangle(b,client);
            Theme.FillRound(g,client,r,!Enabled?Theme.Blend(Theme.Surface,Theme.Raised,0.5f):hover || open?Theme.RaisedHover:Theme.Raised);
            Theme.DrawBorder(g,client,r,Focused || open?Theme.Accent:hover?Theme.BorderStrong:Theme.Border);
            var content=new Rectangle(Theme.S(10),1,Math.Max(0,client.Width-Theme.S(10)-chevron),client.Height-2);
            DrawContent(g,content,SelectedIndex,Enabled?Theme.Text:Theme.Subtle);
            var arrow=new Rectangle(client.Width-chevron,0,chevron,client.Height);
            if(Theme.IconFont!=null)Theme.DrawGlyph(g,Theme.ChevronDown,8f,Theme.Muted,arrow);
            else TextRenderer.DrawText(g,"▾",Font,arrow,Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        }
    }
}
