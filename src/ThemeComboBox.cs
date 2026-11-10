using System;
using System.Drawing;
using System.Windows.Forms;

namespace UlanziLite {
    // Windows themes ignore a DropDownList's field colors. Paint that field
    // after native rendering, retaining the native popup and keyboard behavior.
    sealed class ThemeComboBox : ComboBox {
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if(m.Msg!=0x000f && m.Msg!=0x0318 && m.Msg!=0x0317)return;
            if(m.Msg!=0x000f && m.WParam==IntPtr.Zero)return;
            using(var g=m.Msg==0x000f?Graphics.FromHwnd(Handle):Graphics.FromHdc(m.WParam)) {
                var field=new Rectangle(2,2,Math.Max(0,Width-SystemInformation.VerticalScrollBarWidth-4),Height-4);
                using(var brush=new SolidBrush(BackColor))g.FillRectangle(brush,field);
                TextRenderer.DrawText(g,SelectedItem==null?"":SelectedItem.ToString(),Font,field,ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                if(Focused && !DroppedDown)ControlPaint.DrawFocusRectangle(g,field,ForeColor,BackColor);
            }
        }
    }
}
