using System;
using System.Drawing;
using System.Windows.Forms;

namespace UlanziLite {
    sealed class StartupCheckBox : CheckBox {
        public StartupCheckBox() {SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=false;Size=new Size(430,30);}
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(BackColor);int box=Font.Height;
            var bounds=new Rectangle(1,(Height-box)/2,box,box);
            using(var fill=new SolidBrush(Checked?Theme.AccentSoft:Theme.Raised))e.Graphics.FillRectangle(fill,bounds);
            using(var pen=new Pen(Enabled?Theme.Muted:Theme.Subtle))e.Graphics.DrawRectangle(pen,bounds);
            if(Checked)using(var pen=new Pen(Enabled?Theme.Accent:Theme.Muted,2))e.Graphics.DrawLines(pen,new[]{new Point(bounds.Left+3,bounds.Top+box/2),new Point(bounds.Left+box/2-1,bounds.Bottom-4),new Point(bounds.Right-3,bounds.Top+4)});
            var text=new Rectangle(box+10,0,Width-box-10,Height);
            TextRenderer.DrawText(e.Graphics,Text,Font,text,Enabled?Theme.Text:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
            if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,text,Theme.Text,BackColor);
        }
    }
    sealed class StartupSettingsForm : Form {
        internal readonly CheckBox Autostart,StartInTray;
        internal readonly ThemeButton ApplyButton;
        public StartupSettingsForm(Configuration config,bool preview,Action save) {
            Text="Startup settings";Icon=Branding.CreateIcon();BackColor=Theme.Background;ForeColor=Theme.Text;
            Font=Theme.Ui(10);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(480,260);
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            Autostart=new StartupCheckBox {Text="Start when I sign in to Windows",AccessibleName="Start with Windows",Location=new Point(24,24)};
            StartInTray=new StartupCheckBox {Text="Start in the system tray",AccessibleName="Start in tray",Location=new Point(24,66),Checked=config.StartInTray};
            var detail=new Label {Text="Double-click the tray icon to open the editor.\nKeep this app in its current folder after enabling startup.\nQuit Ulanzi Studio to avoid USB conflicts.",Location=new Point(24,105),Size=new Size(430,72),ForeColor=Theme.Muted};
            var error=new Label {Location=new Point(24,178),Size=new Size(430,28),ForeColor=Theme.Danger,AutoEllipsis=true};
            ApplyButton=new ThemeButton {Text="Apply",AccessibleName="Apply startup settings",Kind=ButtonKind.Primary,Location=new Point(336,214),Size=new Size(120,32),Enabled=!preview};
            var cancel=new ThemeButton {Text="Cancel",DialogResult=DialogResult.Cancel,Location=new Point(204,214),Size=new Size(120,32)};
            Controls.AddRange(new Control[]{Autostart,StartInTray,detail,error,ApplyButton,cancel});AcceptButton=ApplyButton;CancelButton=cancel;
            try {Autostart.Checked=!preview && StartupRegistration.Enabled;}
            catch(Exception ex){Autostart.Enabled=false;ApplyButton.Enabled=false;error.Text="Cannot read Windows startup settings.";Program.Log(ex.Message);}
            if(preview){Autostart.Enabled=StartInTray.Enabled=false;error.Text="Startup settings are disabled in preview mode.";}
            ApplyButton.Click+=(s,e)=>{
                bool previous=config.StartInTray;
                try {
                    config.StartInTray=StartInTray.Checked;
                    StartupRegistration.Apply(Autostart.Checked,save);
                    DialogResult=DialogResult.OK;Close();
                }catch(Exception ex){config.StartInTray=previous;error.Text="Could not save startup settings.";MessageBox.Show(this,ex.Message,"Startup settings",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            };
        }
    }
}
