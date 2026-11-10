using System.Drawing;
using System.Windows.Forms;

namespace UlanziLite {
    // Native disabled text is nearly black on a dark surface.
    sealed class ThemeButton : Button {
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            if(Enabled)return;
            using(var fill=new SolidBrush(BackColor))e.Graphics.FillRectangle(fill,ClientRectangle);
            using(var border=new Pen(FlatAppearance.BorderColor))e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Color.FromArgb(126,143,163),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
    }
}
