using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UlanziLite {
    public sealed class MainForm : Form {
        readonly LocalUsageReader usageReader=new LocalUsageReader();
        readonly Deck deck=new Deck(); readonly AudioController audio=new AudioController();
        readonly bool previewMode; Configuration config; readonly Button[] keys=new Button[13]; readonly Label[] levels=new Label[3]; readonly Label[] details=new Label[3];
        readonly Color background=Color.FromArgb(15,19,27),surface=Color.FromArgb(24,30,41),accent=Color.FromArgb(115,221,185);
        Label status,feedback,editorTitle,targetCaption,targetHint,validation,pagePosition,selectionSummary; TextBox label,target,image; ComboBox kind,apps,pages; NumericUpDown step,brightness;
        readonly Dictionary<int,KeyBinding> drafts=new Dictionary<int,KeyBinding>();
        readonly ToolTip tips=new ToolTip {AutoPopDelay=8000};
        Button saveButton,undoButton,fileButton,folderButton; Label editState; bool loadingEditor; PictureBox editorPreview; Panel editorPanel;
        NotifyIcon tray; System.Windows.Forms.Timer timer; int selected=0,ticks; bool exiting,syncing,pending; string lastEvent="";
        readonly WideScreenState wideState=new WideScreenState();
        readonly Stopwatch uptime=Stopwatch.StartNew();
        readonly SemaphoreSlim displayWriter=new SemaphoreSlim(1,1);
        readonly Artwork.DisplayImages displayImages=new Artwork.DisplayImages();
        PictureBox widePreview;bool updatingWide,updatingUsage;int usageRevision=-1;long usageMinute=-1;long nextWideAttempt;
        bool updatingWebsiteIcons;long websiteEditAt;
        public MainForm(Configuration configuration) : this(configuration,false) { }
        public MainForm(Configuration configuration,bool preview) {
            previewMode=preview;config=configuration;wideState.HoldMilliseconds=config.OverlaySeconds*1000; Text="Ulanzi Lite · D200X"; BackColor=background; ForeColor=Color.FromArgb(232,238,245);
            Font=new Font(Typography.UiFontName,9); ClientSize=new Size(1180,860); MinimumSize=new Size(900,680); AutoScaleMode=AutoScaleMode.Dpi;
            AutoScroll=true;AutoScrollMinSize=new Size(1120,860);
            DoubleBuffered=true; Icon=Branding.CreateIcon(); StartPosition=FormStartPosition.CenterScreen;
            BuildUI(); if(!preview)BuildTray(); PaintKeys(); LoadKey();
            label.TextChanged+=(s,e)=>EditorChanged();target.TextChanged+=(s,e)=>EditorChanged();image.TextChanged+=(s,e)=>EditorChanged();kind.SelectedIndexChanged+=(s,e)=>EditorChanged();
            deck.Status+=(ok,text)=>OnUI(()=>{status.Text=(ok?"● ":"○ ")+text;tips.SetToolTip(status,text); status.ForeColor=ok?accent:Color.FromArgb(244,186,112); Program.Log(text);});
            deck.Input+=(index,state)=>OnUI(()=>HandleInput(index,state));
            deck.Connected+=()=>OnUI(async()=>await SyncDeck());
            if(!preview)Shown+=(s,e)=>{deck.Start(); RefreshAudio();};
            timer=new System.Windows.Forms.Timer {Interval=250}; timer.Tick+=async(s,e)=>{audio.TrackForeground();if(++ticks%4==0 && Visible){RefreshAudio();if(!wideState.WantsAudio(uptime.ElapsedMilliseconds))PreviewClock();}await PumpWebsiteIcons();await PumpWideDisplay();await PumpUsageDisplay();}; if(!preview)timer.Start();
            FormClosing+=(s,e)=>{if(!exiting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}else{if(!FinishEditing()){e.Cancel=true;exiting=false;return;}timer.Stop();deck.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}}};
        }
        void OnUI(Action action) {if(!IsDisposed && IsHandleCreated)try{BeginInvoke(action);}catch(InvalidOperationException){}}
        Label TextAt(Control parent,string text,int x,int y,int w,int h,float size,Color color) {
            var l=new Label {Text=text,Location=new Point(x,y),Size=new Size(w,h),ForeColor=color,Font=new Font(Typography.UiFontName,size),BackColor=Color.Transparent}; l.AutoEllipsis=true; parent.Controls.Add(l);return l;
        }
        Button ButtonAt(Control parent,string text,int x,int y,int w,int h,Action action) {
            var b=new ThemeButton {Text=text,Location=new Point(x,y),Size=new Size(w,h),FlatStyle=FlatStyle.Flat,BackColor=surface,ForeColor=ForeColor,Cursor=Cursors.Hand,TabStop=true};
            b.AccessibleName=text; b.FlatAppearance.BorderColor=Color.FromArgb(52,65,81); b.FlatAppearance.MouseOverBackColor=Color.FromArgb(43,55,70);
            b.Click+=(s,e)=>action(); parent.Controls.Add(b); return b;
        }
        TextBox InputAt(Control parent,string caption,int y) {
            TextAt(parent,caption,20,y,286,22,9,Color.FromArgb(172,187,205));
            var t=new TextBox {Location=new Point(20,y+26),Size=new Size(286,28),BackColor=background,ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle,AccessibleName=caption};parent.Controls.Add(t);return t;
        }
        ComboBox SelectAt(Control parent,int x,int y,int w) {
            var c=new ThemeComboBox {Location=new Point(x,y),Size=new Size(w,28),DropDownStyle=ComboBoxStyle.DropDownList,BackColor=background,ForeColor=ForeColor,FlatStyle=FlatStyle.Flat,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=25};
            c.DrawItem+=(s,e)=>{if(e.Index<0)return;using(var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(44,66,70):background))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,c.Items[e.Index].ToString(),Font,e.Bounds,ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.Left);};
            parent.Controls.Add(c);return c;
        }
        Panel Card(Control parent,int x,int y,int w,int h) {
            var panel=new Panel {Location=new Point(x,y),Size=new Size(w,h),BackColor=surface};parent.Controls.Add(panel);return panel;
        }
        NumericUpDown NumberAt(Control parent,string caption,int x,int y,int min,int max,int value) {
            TextAt(parent,caption,x,y,135,22,9,Color.FromArgb(172,187,205));
            var input=new NumericUpDown {Location=new Point(x,y+26),Size=new Size(106,28),Minimum=min,Maximum=max,Value=value,BackColor=background,ForeColor=ForeColor,AccessibleName=caption};parent.Controls.Add(input);return input;
        }
        void BuildUI() {
            SuspendLayout();
            var canvas=new Panel {Location=Point.Empty,Size=ClientSize,MinimumSize=new Size(1120,860),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};Controls.Add(canvas);
            TextAt(canvas,"ULANZI LITE",26,18,400,43,25,ForeColor);
            TextAt(canvas,"D200X  /  Your desk, under control",29,65,460,26,10,Color.FromArgb(172,187,205));
            status=TextAt(canvas,previewMode?"○ Device offline · Preview":"○ Connecting to D200X…",680,26,470,27,10,accent);status.Anchor=AnchorStyles.Top|AnchorStyles.Right;status.TextAlign=ContentAlignment.MiddleRight;
            var help=ButtonAt(canvas,"Quick guide",1050,63,104,30,()=>MessageBox.Show(this,"Ulanzi Lite 0.3.0 · D200X\n\nSelect one of the top 10 buttons and edit its action.\nPress Ctrl+S to save and apply it to the device.\n\nThe bottom three buttons show AI usage; click to open usage pages.\nDrafts remain available when switching buttons or pages.\nEsc: cancel the selected draft\nClose window: hide to system tray\nTray menu > Exit: quit completely\n\nYou can edit and save without a connected device.\nThe saved current page is applied automatically on connection.","Quick guide",MessageBoxButtons.OK,MessageBoxIcon.Information));help.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            var folder=ButtonAt(canvas,"Data folder",934,63,104,30,()=>{try{Process.Start(new ProcessStartInfo(AppDomain.CurrentDomain.BaseDirectory){UseShellExecute=true});}catch(Exception ex){Feedback(ex.Message,true);}});folder.Anchor=AnchorStyles.Top|AnchorStyles.Right;

            var deckPanel=Card(canvas,26,114,776,440);deckPanel.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            TextAt(deckPanel,"Your buttons",18,16,130,26,12,ForeColor);
            TextAt(deckPanel,"Select a button to edit it on the right",18,46,470,22,9,Color.FromArgb(172,187,205));
            pages=SelectAt(deckPanel,380,17,250);pages.Anchor=AnchorStyles.Top|AnchorStyles.Right;pages.AccessibleName="Select page";foreach(var page in config.Pages)pages.Items.Add(page.Name);pages.SelectedIndex=config.Page;
            pages.SelectedIndexChanged+=async(s,e)=>{config.Page=pages.SelectedIndex;PaintKeys();LoadKey();try{SaveConfiguration();await SyncDeck();}catch(Exception ex){Feedback(ex.Message,true);}};
            var previous=ButtonAt(deckPanel,"‹",640,17,36,32,()=>ChangePage(-1));previous.AccessibleName="Previous page";previous.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            var next=ButtonAt(deckPanel,"›",682,17,36,32,()=>ChangePage(1));next.AccessibleName="Next page";next.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            pagePosition=TextAt(deckPanel,"",722,24,42,25,9,accent);pagePosition.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            tips.SetToolTip(previous,"Previous page");tips.SetToolTip(next,"Next page");
            var grid=new TableLayoutPanel {Location=new Point(14,79),Size=new Size(748,218),ColumnCount=5,RowCount=2,BackColor=surface,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};deckPanel.Controls.Add(grid);
            for(int i=0;i<5;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
            for(int i=0;i<2;i++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            for(int i=0;i<13;i++) {
                int k=i;var b=ButtonAt(i<10?(Control)grid:deckPanel,"",i<10?0:35+(i-10)*149,i<10?0:321,99,99,()=>{if(k>=10){TryLaunch(UsageMeters.Binding(k-10));return;}selected=k;PaintSelection();LoadKey();});keys[i]=b;
                if(i<10){b.Anchor=AnchorStyles.None;b.Margin=new Padding(5);grid.SetCellPosition(b,new TableLayoutPanelCellPosition(i%5,i/5));}
                b.BackgroundImageLayout=ImageLayout.Zoom;b.AccessibleName="Button "+(i+1);tips.SetToolTip(b,k>=10?UsageMeters.Names[k-10]+" usage · Click to open usage page":"Click to edit · Save to use on your device");
            }
            grid.Layout+=(s,e)=>{int side=Math.Max(1,Math.Min(grid.ClientSize.Width/5,grid.ClientSize.Height/2)-10);for(int i=0;i<10;i++)keys[i].Size=new Size(side,side);};
            TextAt(deckPanel,"AI usage meters",18,299,220,20,8,Color.FromArgb(172,187,205));
            TextAt(deckPanel,"Device wide display",470,299,260,20,8,Color.FromArgb(172,187,205));
            widePreview=new PictureBox {Location=new Point(470,321),Size=new Size(288,101),BackColor=background,SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="Wide display: clock / three audio knobs",Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};deckPanel.Controls.Add(widePreview);PreviewClock();

            var audioPanel=Card(canvas,26,570,776,160);audioPanel.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            TextAt(audioPanel,"Audio controls",18,13,200,26,12,ForeColor);
            TextAt(audioPanel,"Turn to adjust volume · Press to mute",290,18,465,22,9,Color.FromArgb(172,187,205));
            var audioGrid=new TableLayoutPanel {Location=new Point(12,49),Size=new Size(752,101),ColumnCount=3,RowCount=1,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};audioPanel.Controls.Add(audioGrid);
            audioGrid.SuspendLayout();for(int i=0;i<3;i++)audioGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
            string[] titles={"01  Microphone","02  Active app","03  Master output"};
            for(int i=0;i<3;i++) {
                int k=i;
                var panel=new Panel {Size=new Size(238,101),Dock=DockStyle.Fill,Margin=new Padding(6,0,6,0),BackColor=background};audioGrid.Controls.Add(panel,i,0);
                var audioTitle=TextAt(panel,titles[i],10,7,140,22,9,accent);audioTitle.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;levels[i]=TextAt(panel,"—",155,4,72,30,17,ForeColor);levels[i].Anchor=AnchorStyles.Top|AnchorStyles.Right;levels[i].TextAlign=ContentAlignment.MiddleRight;
                details[i]=TextAt(panel,previewMode?"Audio preview":"Checking…",10,35,217,19,8,Color.FromArgb(172,187,205));details[i].Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
                var minus=ButtonAt(panel,"−",10,62,42,28,()=>ControlAudio(k,-(int)step.Value,false));minus.AccessibleName=titles[i]+" volume down";
                var mute=ButtonAt(panel,"Mute",59,62,112,28,()=>ControlAudio(k,0,true));mute.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;mute.AccessibleName=titles[i]+" toggle mute";
                var plus=ButtonAt(panel,"+",178,62,42,28,()=>ControlAudio(k,(int)step.Value,false));plus.Anchor=AnchorStyles.Top|AnchorStyles.Right;plus.AccessibleName=titles[i]+" volume up";
            }
            audioGrid.ResumeLayout(true);
            var settings=Card(canvas,26,746,776,72);settings.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            TextAt(settings,"Device settings",18,16,112,25,11,ForeColor);
            TextAt(settings,"Saved automatically",18,43,120,20,8,Color.FromArgb(172,187,205));
            step=NumberAt(settings,"Volume step (%)",155,8,1,10,config.Step);
            brightness=NumberAt(settings,"Brightness (%)",345,8,0,100,config.Brightness);
            var overlaySeconds=NumberAt(settings,"Overlay time (sec)",535,8,1,30,config.OverlaySeconds);
            step.ValueChanged+=(s,e)=>{config.Step=(int)step.Value;try{SaveConfiguration();}catch(Exception ex){Feedback(ex.Message,true);}};
            brightness.ValueChanged+=async(s,e)=>{config.Brightness=(int)brightness.Value;try{SaveConfiguration();if(deck.IsConnected)await deck.Send(10,Encoding.ASCII.GetBytes(config.Brightness.ToString()),false);}catch(Exception ex){Feedback(ex.Message,true);}};
            overlaySeconds.ValueChanged+=(s,e)=>{config.OverlaySeconds=(int)overlaySeconds.Value;wideState.HoldMilliseconds=config.OverlaySeconds*1000;try{SaveConfiguration();}catch(Exception ex){Feedback(ex.Message,true);}};

            editorPanel=Card(canvas,820,114,334,704);editorPanel.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            editorTitle=TextAt(editorPanel,"Button 01",20,16,177,32,17,ForeColor);
            editState=TextAt(editorPanel,"Saved",211,24,105,24,9,accent);editState.TextAlign=ContentAlignment.TopRight;
            editorPreview=new PictureBox {Location=new Point(20,61),Size=new Size(62,62),BackColor=background,SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="Button draft preview"};editorPanel.Controls.Add(editorPreview);
            TextAt(editorPanel,"Button preview",96,65,220,24,10,ForeColor);
            selectionSummary=TextAt(editorPanel,"",96,94,220,23,8,Color.FromArgb(172,187,205));
            label=InputAt(editorPanel,"Display name",139);label.MaxLength=40;
            TextAt(editorPanel,"Action",20,205,286,23,9,Color.FromArgb(172,187,205));kind=SelectAt(editorPanel,20,231,286);kind.AccessibleName="Button action";kind.Items.AddRange(new object[]{"Open website","Open app / file / folder"});
            targetCaption=TextAt(editorPanel,"URL or launch path",20,273,286,22,9,Color.FromArgb(172,187,205));
            target=new TextBox {Location=new Point(20,299),Size=new Size(286,28),BackColor=background,ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle,AccessibleName="URL or launch path"};editorPanel.Controls.Add(target);
            targetHint=TextAt(editorPanel,"",20,329,286,22,8,Color.FromArgb(172,187,205));
            fileButton=ButtonAt(editorPanel,"File…",20,357,88,29,()=>BrowseTarget());
            folderButton=ButtonAt(editorPanel,"Folder…",116,357,88,29,()=>{using(var dialog=new FolderBrowserDialog {Description="Choose a folder to open."})if(dialog.ShowDialog(this)==DialogResult.OK){target.Text=dialog.SelectedPath;kind.SelectedIndex=1;}});
            ButtonAt(editorPanel,"Test action",212,357,94,29,()=>TryLaunch(CurrentEditor()));
            fileButton.AccessibleName="Browse file";folderButton.AccessibleName="Browse folder";
            TextAt(editorPanel,"Choose an installed app",20,402,286,23,9,Color.FromArgb(172,187,205));apps=SelectAt(editorPanel,20,428,286);apps.AccessibleName="Select installed app";apps.Items.Add("Choose an app…");
            foreach(var path in Configuration.InstalledApps())apps.Items.Add(new AppItem(path));apps.SelectedIndex=0;
            apps.SelectedIndexChanged+=(s,e)=>{var item=apps.SelectedItem as AppItem;if(item==null)return;target.Text=item.Path;label.Text=Path.GetFileNameWithoutExtension(item.Path);kind.SelectedIndex=1;};
            image=InputAt(editorPanel,"Icon image (optional)",470);
            ButtonAt(editorPanel,"Choose image",20,532,138,29,()=>{using(var dialog=new OpenFileDialog {Filter="image|*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK)image.Text=dialog.FileName;});
            ButtonAt(editorPanel,"Clear image",168,532,138,29,()=>image.Text="");
            validation=TextAt(editorPanel,"",20,568,286,36,8,Color.FromArgb(250,174,132));validation.AccessibleName="Editor validation error";
            saveButton=ButtonAt(editorPanel,"Save and apply to device",20,609,286,42,()=>SaveKey());saveButton.BackColor=Color.FromArgb(37,91,76);saveButton.ForeColor=Color.White;
            undoButton=ButtonAt(editorPanel,"Cancel edit",20,661,88,29,()=>CancelEdit());
            ButtonAt(editorPanel,"Reset button",116,661,88,29,()=>{label.Text="Empty";target.Text="";image.Text="";kind.SelectedIndex=0;EditorChanged();});
            ButtonAt(editorPanel,"Run saved",212,661,94,29,()=>TryLaunch(config.Pages[config.Page].Keys[selected]));
            feedback=TextAt(canvas,"Select a button → Edit its action → Save and apply to device",26,830,770,25,9,Color.FromArgb(172,187,205));feedback.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var shortcuts=TextAt(canvas,"Ctrl+S Save  ·  Esc Cancel edit",820,830,334,25,9,Color.FromArgb(172,187,205));shortcuts.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            tips.SetToolTip(target,"Web URLs must start with https:// or http://. Use full paths for files and folders. Leave blank for an empty button.");
            tips.SetToolTip(saveButton,"Save the selected button and apply it to the device (Ctrl+S).");
            // Keep the tab order consistent with the visual workflow in each container.
            SetTabOrder(canvas);ResumeLayout(true);
        }
        static void SetTabOrder(Control parent) {
            int index=0;foreach(var child in parent.Controls.Cast<Control>().OrderBy(c=>c.Top).ThenBy(c=>c.Left)){child.TabIndex=index++;if(child.HasChildren)SetTabOrder(child);}
        }
        void BuildTray() {
            var menu=new ContextMenuStrip(); menu.Items.Add("Open settings",null,(s,e)=>ShowEditor());menu.Items.Add("Exit",null,(s,e)=>{exiting=true;Close();});
            tray=new NotifyIcon {Icon=Icon,Text="Ulanzi Lite · D200X",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=(s,e)=>ShowEditor();
        }
        void ShowEditor() {Show();WindowState=FormWindowState.Normal;Activate();}
        void ChangePage(int direction) {pages.SelectedIndex=(config.Page+direction+config.Pages.Count)%config.Pages.Count;}
        void PaintKeys() {
            for(int i=0;i<keys.Length;i++)RenderKey(i);
            PaintSelection();
        }
        void RenderKey(int index) {
            KeyBinding key;if(!drafts.TryGetValue(config.Page*13+index,out key))key=config.Pages[config.Page].Keys[index];
            var old=keys[index].BackgroundImage;keys[index].BackgroundImage=index>=10?UsageMeters.Render(index-10):Artwork.Render(key);if(old!=null)old.Dispose();
        }
        void PaintSelection() {if(editState!=null)UpdateEditState();for(int i=0;i<13;i++){keys[i].FlatAppearance.BorderSize=i==selected?3:1;keys[i].FlatAppearance.BorderColor=i==selected?accent:Color.FromArgb(52,65,81);}}
        void SaveConfiguration() {if(!previewMode)config.Save();}
        int DraftId {get{return config.Page*13+selected;}}
        void LoadKey() {
            loadingEditor=true;
            try {KeyBinding k;if(!drafts.TryGetValue(DraftId,out k))k=config.Pages[config.Page].Keys[selected];editorTitle.Text="Button "+(selected+1).ToString("00");label.Text=k.Label;target.Text=k.Target;image.Text=k.Image;kind.SelectedIndex=k.Kind=="application"?1:0;apps.SelectedIndex=0;}
            finally {loadingEditor=false;}validation.Text="";UpdateEditorContext();UpdateEditState();
        }
        void EditorChanged() {
            if(loadingEditor)return;
            websiteEditAt=uptime.ElapsedMilliseconds;
            var key=CurrentEditor();var saved=config.Pages[config.Page].Keys[selected];
            if(Configuration.Json.Serialize(key)==Configuration.Json.Serialize(saved))drafts.Remove(DraftId);else drafts[DraftId]=key;
            validation.Text="";UpdateEditorContext();RenderKey(selected);UpdateEditState();
        }
        void UpdateEditorContext() {
            bool application=kind.SelectedIndex==1;targetCaption.Text=application?"File or folder path":"Website URL";targetHint.Text=application?"Enter a full path or browse below":"https://example.com · Leave blank for empty";
            fileButton.Enabled=folderButton.Enabled=application;
        }
        void UpdateEditState() {
            bool dirty=drafts.ContainsKey(DraftId);editState.Text=dirty?"● Unsaved":"Saved";editState.ForeColor=dirty?Color.FromArgb(244,186,112):accent;
            saveButton.Text=dirty?"Save changes and apply":"Save and apply to device";
            Text="Ulanzi Lite · D200X"+(drafts.Count>0?" · Editing "+drafts.Count:"");
            undoButton.Enabled=dirty;pagePosition.Text=(config.Page+1)+" / "+config.Pages.Count;
            KeyBinding preview;if(!drafts.TryGetValue(DraftId,out preview))preview=config.Pages[config.Page].Keys[selected];
            var old=editorPreview.Image;editorPreview.Image=Artwork.Render(preview);if(old!=null)old.Dispose();
            selectionSummary.Text=config.Pages[config.Page].Name+" · "+(dirty?"Save to apply to device":"Saved button");tips.SetToolTip(selectionSummary,selectionSummary.Text);
            for(int i=0;i<keys.Length;i++){KeyBinding key;if(!drafts.TryGetValue(config.Page*13+i,out key))key=config.Pages[config.Page].Keys[i];keys[i].AccessibleName="Button "+(i+1)+" · "+(i>=10?UsageMeters.Names[i-10]+" usage":key.Label)+(drafts.ContainsKey(config.Page*13+i)?" · Unsaved":"");keys[i].Text=drafts.ContainsKey(config.Page*13+i)?"●":"";keys[i].TextAlign=ContentAlignment.TopRight;keys[i].ForeColor=Color.FromArgb(244,186,112);}
        }
        bool FinishEditing() {
            if(drafts.Count==0)return true;
            var answer=MessageBox.Show(this,"Unsaved buttons: "+drafts.Count+". Save before exiting?","Save changes",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            if(answer==DialogResult.Cancel)return false;if(answer==DialogResult.No)return true;
            var originals=new Dictionary<int,KeyBinding>();
            try {
                foreach(var item in drafts)item.Value.Validate();
                foreach(var item in drafts){originals[item.Key]=config.Pages[item.Key/13].Keys[item.Key%13];config.Pages[item.Key/13].Keys[item.Key%13]=item.Value;}
                SaveConfiguration();drafts.Clear();return true;
            }catch(Exception ex){foreach(var item in originals)config.Pages[item.Key/13].Keys[item.Key%13]=item.Value;MessageBox.Show(this,ex.Message,"Could not save",MessageBoxButtons.OK,MessageBoxIcon.Warning);return false;}
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
            if(keyData==(Keys.Control|Keys.S)){SaveKey();return true;}
            if(keyData==Keys.Escape && drafts.ContainsKey(DraftId)){CancelEdit();return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        void CancelEdit() {if(!drafts.Remove(DraftId))return;LoadKey();RenderKey(selected);Feedback("Canceled edits for this button.",false);}
        KeyBinding CurrentEditor() {return new KeyBinding {Label=label.Text.Trim(),Target=target.Text.Trim(),Kind=kind.SelectedIndex==1?"application":"website",Image=image.Text.Trim(),Color=config.Pages[config.Page].Keys[selected].Color};}
        async void SaveKey() {
            try {
                var key=CurrentEditor();
                try {key.Validate();}catch(ArgumentException ex){
                    validation.Text=ex.Message;tips.SetToolTip(validation,ex.Message);
                    TextBox invalid=ex.Message.Contains("Name")?label:ex.Message.Contains("image") || ex.Message.Contains("Icon")?image:target;
                    ActiveControl=invalid;invalid.Focus();invalid.SelectAll();Feedback("Check your input. "+ex.Message,true);return;
                }
                validation.Text="";var page=config.Pages[config.Page];var old=page.Keys[selected];page.Keys[selected]=key;
                try{SaveConfiguration();}catch{page.Keys[selected]=old;throw;}
                drafts.Remove(DraftId);PaintKeys();UpdateEditState();Feedback("Saved. Applying to device…",false);await SyncDeck();
            }catch(Exception ex){Feedback(ex.Message,true);}
        }
        void BrowseTarget() {using(var dialog=new OpenFileDialog {Filter="Apps / shortcuts|*.exe;*.lnk|All files|*.*"})if(dialog.ShowDialog()==DialogResult.OK){target.Text=dialog.FileName;kind.SelectedIndex=1;if(label.Text=="Empty")label.Text=Path.GetFileNameWithoutExtension(dialog.FileName);}}
        void TryLaunch(KeyBinding key) {try{key.Launch();string message=String.IsNullOrEmpty(key.Target)?"This button is empty.":key.Label+" launch requested";Feedback(message,false);Program.Log(message);}catch(Exception ex){Program.Log("Launch failed: "+ex);Feedback(ex.Message,true);}}
        void HandleInput(int index,int state) {
            lastEvent="Input "+index+":"+state;Program.Log(lastEvent);
            if(index>=17 && index<=19) {if(state==1)ControlAudio(index-17,0,true);else if(state==2 || state==3)ControlAudio(index-17,(state==2?-1:1)*config.Step,false);}
            else if(state==1){if(index>=0 && index<13)TryLaunch(index>=10?UsageMeters.Binding(index-10):config.Pages[config.Page].Keys[index]);else if(index==15 || index==16)ChangePage(index==15?-1:1);}
        }
        void ControlAudio(int knob,int delta,bool toggle) {var result=audio.Control(knob,delta,toggle);levels[knob].Text=result.ToString();details[knob].Text=result.Detail;tips.SetToolTip(details[knob],result.Detail);string message=(knob==0?"Mic":knob==1?(audio.ForegroundName??"Active app"):"Master")+" · "+result.ToString();Program.Log("Audio knob="+knob+" delta="+delta+" toggle="+toggle+" available="+result.Available+" "+message+" · "+result.Detail);Feedback(message,!result.Available);wideState.Touch(uptime.ElapsedMilliseconds,knob);}
        void PreviewClock() {SetWidePreview(WideArtwork.Clock(DateTime.Now));}
        void SetWidePreview(Bitmap image) {var old=widePreview.Image;widePreview.Image=image;if(old!=null)old.Dispose();}
        byte[] WindowMode(int mode) {return Encoding.ASCII.GetBytes(mode+"|0|0|"+DateTime.Now.ToString("HH:mm:ss")+"|0");}
        async Task PumpWideDisplay() {
            long now=uptime.ElapsedMilliseconds;DateTime clockTime=DateTime.Now;long clockSecond=clockTime.Ticks/TimeSpan.TicksPerSecond;
            if(updatingWide || syncing || !deck.IsConnected || now<nextWideAttempt || !wideState.NeedsUpdate(now,clockSecond))return;
            updatingWide=true;
            await displayWriter.WaitAsync();
            try {
                bool showAudio=wideState.WantsAudio(uptime.ElapsedMilliseconds);int version=wideState.Version;
                byte[] png;
                if(showAudio) {
                    var states=new AudioState[3];for(int i=0;i<3;i++)states[i]=audio.Control(i,0,false);
                    int active=wideState.ActiveKnob;string appName=audio.ForegroundName;
                    png=await Task.Run(()=>{using(var wide=WideArtwork.Audio(states,active,appName))return WideArtwork.Encode(wide);});
                    // A slow render must not revive an overlay that has already expired.
                    if(!wideState.WantsAudio(uptime.ElapsedMilliseconds))return;
                    SetWidePreview(WideArtwork.Audio(states,active,appName));
                }else {png=await Task.Run(()=>{using(var clock=WideArtwork.Clock(clockTime))return WideArtwork.Encode(clock);});SetWidePreview(WideArtwork.Clock(clockTime));}
                byte[] bundle=await Task.Run(()=>displayImages.Wide(png));
                await deck.Send(1,bundle,true);
                await deck.Send(6,WindowMode(2),false);
                bool wasAudio=wideState.SentAudio;wideState.MarkSent(showAudio,version,clockSecond);
                if(showAudio || wasAudio)Program.Log("Wide display: "+(showAudio?"audio":"clock image")+" ("+bundle.Length+" bytes)");
            }catch(Exception ex){wideState.MarkSent(true,-1);nextWideAttempt=uptime.ElapsedMilliseconds+1000;Program.Log("Wide display update failed: "+ex.Message);}
            finally {displayWriter.Release();updatingWide=false;}
        }
        async Task PumpUsageDisplay() {
            await usageReader.RefreshAsync();
            if(IsDisposed)return;
            int revision=UsageMeters.Revision;long minute=DateTime.UtcNow.Ticks/TimeSpan.TicksPerMinute;
            if(updatingUsage || syncing || updatingWide || (usageRevision==revision && usageMinute==minute))return;
            updatingUsage=true;
            try {
                for(int i=10;i<13;i++){var old=keys[i].BackgroundImage;keys[i].BackgroundImage=UsageMeters.Render(i-10);if(old!=null)old.Dispose();}
                if(deck.IsConnected){
                    await displayWriter.WaitAsync();
                    try {var bundle=await Task.Run(()=>displayImages.Usage());await deck.Send(1,bundle,true);await deck.Send(6,WindowMode(2),false);Program.Log("Usage display: full page ("+bundle.Length+" bytes)");}
                    finally {displayWriter.Release();}
                }
                usageRevision=revision;usageMinute=minute;
            }catch(Exception ex){Program.Log("Usage display update failed: "+ex.Message);}
            finally {updatingUsage=false;}
        }
        void RefreshAudio() {audio.TrackForeground();for(int i=0;i<3;i++){var result=audio.Control(i,0,false);levels[i].Text=result.Available?result.ToString():"—";details[i].Text=result.Detail;tips.SetToolTip(details[i],result.Detail);}}
        async Task PumpWebsiteIcons() {
            if(updatingWebsiteIcons || uptime.ElapsedMilliseconds-websiteEditAt<700)return;
            updatingWebsiteIcons=true;
            try {
                var saved=config.Pages[config.Page].Keys.Take(10).ToArray();
                var visible=saved.Concat(drafts.Where(d=>d.Key/13==config.Page && d.Key%13<10).Select(d=>d.Value)).ToArray();
                if(await WebsiteIconCache.Shared.RefreshAsync(visible)) {
                    if(IsDisposed)return;
                    PaintKeys();UpdateEditState();
                    // SyncDeck always snapshots saved bindings, so a downloaded draft cannot be applied early.
                    if(deck.IsConnected)await SyncDeck();
                }
            }catch(Exception ex){Program.Log("Website icon update failed: "+ex.Message);}
            finally {updatingWebsiteIcons=false;}
        }
        void Feedback(string text,bool error) {feedback.Text=text;feedback.ForeColor=error?Color.FromArgb(250,174,132):accent;if(error)Program.Log(text);}
        async Task SyncDeck() {
            if(syncing){pending=true;return;}if(!deck.IsConnected){Feedback("Settings saved. They will apply when D200X connects.",false);return;}
            syncing=true;
            await displayWriter.WaitAsync();
            try {
                do {
                    pending=false;
                    // Snapshot on the UI thread; rendering and ZIP compression stay off it.
                    var snapshot=Configuration.Json.Deserialize<DeckPage>(Configuration.Json.Serialize(config.Pages[config.Page]));
                    DateTime initialClock=DateTime.Now;
                    byte[] bundle=await Task.Run(()=>{using(var clock=WideArtwork.Clock(initialClock))return displayImages.Reset(snapshot,WideArtwork.Encode(clock));});
                    await deck.Send(11,Encoding.UTF8.GetBytes("{\"Align\":\"bottom\",\"Color\":16777215,\"FontName\":\"Roboto\",\"ShowTitle\":false,\"Size\":10,\"Weight\":80}"),false);
                    await deck.Send(1,bundle,true);
                    await deck.Send(6,WindowMode(2),false);wideState.Reset();wideState.MarkSent(false,wideState.Version,initialClock.Ticks/TimeSpan.TicksPerSecond);PreviewClock();
                    await deck.Send(10,Encoding.ASCII.GetBytes(config.Brightness.ToString()),false);
                    Feedback("Applied to device · "+config.Pages[config.Page].Name,false);Program.Log("Bundle sent: "+bundle.Length+" bytes");
                }while(pending && deck.IsConnected);
            }catch(Exception ex){Feedback("Settings saved · Device update failed: "+ex.Message,true);}finally{displayWriter.Release();syncing=false;}
        }
        protected override void Dispose(bool disposing) {if(disposing){if(timer!=null)timer.Dispose();tips.Dispose();deck.Dispose();if(tray!=null)tray.Dispose();if(Icon!=null)Icon.Dispose();foreach(var k in keys)if(k!=null && k.BackgroundImage!=null)k.BackgroundImage.Dispose();if(widePreview!=null && widePreview.Image!=null)widePreview.Image.Dispose();if(editorPreview!=null && editorPreview.Image!=null)editorPreview.Image.Dispose();}base.Dispose(disposing);}
        sealed class AppItem {public readonly string Path;public AppItem(string path){Path=path;}public override string ToString(){return System.IO.Path.GetFileNameWithoutExtension(Path);}}
    }
}
