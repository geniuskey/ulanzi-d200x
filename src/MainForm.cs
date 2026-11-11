using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UlanziLite {
    public sealed class MainForm : Form {
        const string WindowTitle="Ulanzi Lite · D200X";
        readonly LocalUsageReader usageReader=new LocalUsageReader();
        readonly Deck deck=new Deck(); readonly AudioController audio=new AudioController();
        readonly bool previewMode; Configuration config; readonly KeyButton[] keys=new KeyButton[13];
        readonly AudioTile[] tiles=new AudioTile[3]; readonly ThemeButton[] muteButtons=new ThemeButton[3],minusButtons=new ThemeButton[3],plusButtons=new ThemeButton[3];
        readonly Dictionary<int,KeyBinding> drafts=new Dictionary<int,KeyBinding>();
        readonly ToolTip tips=new ToolTip {AutoPopDelay=8000};
        Panel canvas,statusBar,bezel,headerRule,editorRule,footerRule; Card deckPanel,audioPanel,editorPanel;
        PictureBox appMark; Label appTitle,appSubtitle,brightnessIcon,brightnessValue,deckTitle,deckSubtitle,audioTitle,audioSubtitle,stepCaption,overlayCaption;
        Label feedback,shortcuts,editorTitle,selectionSummary,actionCaption,nameCaption,targetCaption,targetHint,appsCaption,imageCaption,validation,pagePosition;
        StatusPill status,editState; FieldBox labelField,targetField,imageField; TextBox label,target,image; SegmentedControl kind; ComboBox apps,pages;
        Stepper step,overlaySeconds; Slider brightness; bool brightnessDirty;
        ThemeButton guideButton,dataButton,previousPage,nextPage,saveButton,undoButton,resetButton,testButton,fileButton,folderButton,imageBrowse,imageClear;
        bool loadingEditor; RoundedPictureBox editorPreview,widePreview;
        NotifyIcon tray; System.Windows.Forms.Timer timer; int selected=0,ticks; bool exiting,syncing,pending; string lastEvent="";
        readonly WideScreenState wideState=new WideScreenState();
        readonly Stopwatch uptime=Stopwatch.StartNew();
        readonly SemaphoreSlim displayWriter=new SemaphoreSlim(1,1);
        readonly Artwork.DisplayImages displayImages=new Artwork.DisplayImages();
        bool updatingWide,updatingUsage;int usageRevision=-1;long usageMinute=-1;long nextWideAttempt;
        bool updatingWebsiteIcons;long websiteEditAt;
        public MainForm(Configuration configuration) : this(configuration,false) { }
        public MainForm(Configuration configuration,bool preview) {
            previewMode=preview;config=configuration;wideState.HoldMilliseconds=config.OverlaySeconds*1000;
            using(var g=CreateGraphics())Theme.Scale=Math.Max(1f,g.DpiX/96f);
            Text=WindowTitle;BackColor=Theme.Background;ForeColor=Theme.Text;Font=new Font(Typography.UiFontName,9);AutoScaleMode=AutoScaleMode.None;
            ClientSize=new Size(S(1200),S(860));MinimumSize=new Size(S(920),S(700));AutoScroll=true;
            DoubleBuffered=true;Icon=Branding.CreateIcon();StartPosition=FormStartPosition.CenterScreen;
            BuildUI(); if(!preview)BuildTray(); PaintKeys(); LoadKey();
            label.TextChanged+=(s,e)=>EditorChanged();target.TextChanged+=(s,e)=>EditorChanged();image.TextChanged+=(s,e)=>EditorChanged();kind.SelectedIndexChanged+=(s,e)=>EditorChanged();
            deck.Status+=(ok,text)=>OnUI(()=>{status.Text=text;status.Tone=ok?Tone.Positive:Tone.Warning;tips.SetToolTip(status,text);LayoutHeader();Program.Log(text);});
            deck.Input+=(index,state)=>OnUI(()=>HandleInput(index,state));
            deck.Connected+=()=>OnUI(async()=>await SyncDeck());
            if(!preview)Shown+=(s,e)=>{deck.Start(); RefreshAudio();};
            timer=new System.Windows.Forms.Timer {Interval=250}; timer.Tick+=async(s,e)=>{audio.TrackForeground();await ApplyBrightness();if(++ticks%4==0 && Visible){RefreshAudio();if(!wideState.WantsAudio(uptime.ElapsedMilliseconds))PreviewClock();}await PumpWebsiteIcons();await PumpWideDisplay();await PumpUsageDisplay();}; if(!preview)timer.Start();
            FormClosing+=(s,e)=>{if(!exiting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}else{if(!FinishEditing()){e.Cancel=true;exiting=false;return;}timer.Stop();deck.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}}};
        }
        static int S(int value) {return Theme.S(value);}
        void OnUI(Action action) {if(!IsDisposed && IsHandleCreated)try{BeginInvoke(action);}catch(InvalidOperationException){}}

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            // Dark title bar on Windows 10 20H1+ and a matching caption color on Windows 11.
            try {int dark=1;DwmSetWindowAttribute(Handle,20,ref dark,4);int caption=ColorTranslator.ToWin32(Theme.Background);DwmSetWindowAttribute(Handle,35,ref caption,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }

        static Font UiFont(float size,bool semibold=false) {return semibold?new Font("Segoe UI Semibold",size):new Font(Typography.UiFontName,size);}
        Label TextAt(Control parent,string text,float size,Color color,bool semibold=false) {
            var l=new Label {Text=text,ForeColor=color,Font=UiFont(size,semibold),BackColor=Color.Transparent,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};parent.Controls.Add(l);return l;
        }
        Label Caption(Control parent,string text) {return TextAt(parent,text,9,Theme.Muted);}
        ThemeButton ButtonAt(Control parent,string text,string glyph,ButtonKind buttonKind,Action action,string accessibleName=null) {
            var b=new ThemeButton {Text=text,Glyph=glyph,Kind=buttonKind,Font=UiFont(9),TabStop=true};
            b.AccessibleName=accessibleName??text;b.Click+=(s,e)=>action();parent.Controls.Add(b);return b;
        }
        FieldBox FieldAt(Control parent,string accessibleName) {var f=new FieldBox(accessibleName);parent.Controls.Add(f);return f;}
        ThemeComboBox SelectAt(Control parent,string accessibleName,bool appIcons=false) {
            ThemeComboBox c=appIcons?new AppComboBox():new ThemeComboBox();c.ItemHeight=S(appIcons?28:26);c.AccessibleName=accessibleName;parent.Controls.Add(c);return c;
        }
        Panel Rule(Control parent) {var p=new Panel {BackColor=Theme.Border,Height=1};parent.Controls.Add(p);return p;}

        void BuildUI() {
            SuspendLayout();
            canvas=new Panel {Location=Point.Empty,BackColor=Theme.Background};Controls.Add(canvas);
            BuildHeader();BuildDeck();BuildAudio();BuildEditor();
            statusBar=new Panel {BackColor=Theme.Surface};canvas.Controls.Add(statusBar);
            statusBar.Paint+=(s,e)=>{using(var pen=new Pen(Theme.Border))e.Graphics.DrawLine(pen,0,0,statusBar.Width,0);};
            feedback=TextAt(statusBar,"Select a key, edit its action, then save to apply it to the device.",9,Theme.Muted);
            shortcuts=TextAt(statusBar,"Ctrl+S  Save    Esc  Discard draft    ‹ ›  Pages",8.5f,Theme.Subtle);shortcuts.TextAlign=ContentAlignment.MiddleRight;
            Resize+=(s,e)=>ResizeCanvas();canvas.Resize+=(s,e)=>DoLayout();
            ResizeCanvas();DoLayout();
            SetTabOrder(canvas);ResumeLayout(true);
        }
        void BuildHeader() {
            appMark=new PictureBox {Image=Branding.CreateMark(S(72)),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent};canvas.Controls.Add(appMark);
            appTitle=TextAt(canvas,"Ulanzi Lite",15,Theme.Text,true);
            appSubtitle=TextAt(canvas,"Desk controller for Ulanzi D200X",9,Theme.Muted);
            status=new StatusPill {Text=previewMode?"Preview · Device offline":"Connecting to D200X…",Tone=previewMode?Tone.Neutral:Tone.Warning,Font=UiFont(9)};canvas.Controls.Add(status);
            brightnessIcon=TextAt(canvas,Theme.IconFont==null?"☀":Theme.Brightness,11,Theme.Muted);brightnessIcon.TextAlign=ContentAlignment.MiddleCenter;if(Theme.IconFont!=null)brightnessIcon.Font=new Font(Theme.IconFont,11);
            brightness=new Slider {Minimum=0,Maximum=100,Value=config.Brightness,AccessibleName="Brightness"};canvas.Controls.Add(brightness);
            brightnessValue=TextAt(canvas,config.Brightness+"%",9,Theme.Text);
            brightness.ValueChanged+=(s,e)=>{config.Brightness=brightness.Value;brightnessValue.Text=brightness.Value+"%";brightnessDirty=true;};
            brightness.ValueCommitted+=async(s,e)=>await ApplyBrightness();
            tips.SetToolTip(brightness,"Device brightness");tips.SetToolTip(brightnessIcon,"Device brightness");
            dataButton=ButtonAt(canvas,"Data folder",Theme.Folder,ButtonKind.Ghost,()=>{try{Process.Start(new ProcessStartInfo(AppDomain.CurrentDomain.BaseDirectory){UseShellExecute=true});}catch(Exception ex){Feedback(ex.Message,true);}});
            guideButton=ButtonAt(canvas,"Guide",Theme.Help,ButtonKind.Ghost,ShowGuide,"Quick guide");
            headerRule=Rule(canvas);
        }
        void BuildDeck() {
            deckPanel=new Card();canvas.Controls.Add(deckPanel);
            deckTitle=TextAt(deckPanel,"Deck",13,Theme.Text,true);
            deckSubtitle=TextAt(deckPanel,"Click a key to edit it · Bottom row mirrors the device",9,Theme.Muted);
            pagePosition=TextAt(deckPanel,"",9,Theme.Muted);pagePosition.TextAlign=ContentAlignment.MiddleRight;
            previousPage=ButtonAt(deckPanel,"",Theme.ChevronLeft,ButtonKind.Secondary,()=>ChangePage(-1),"Previous page");
            pages=SelectAt(deckPanel,"Select page");foreach(var page in config.Pages)pages.Items.Add(page.Name);pages.SelectedIndex=config.Page;
            pages.SelectedIndexChanged+=async(s,e)=>{config.Page=pages.SelectedIndex;PaintKeys();LoadKey();try{SaveConfiguration();await SyncDeck();}catch(Exception ex){Feedback(ex.Message,true);}};
            nextPage=ButtonAt(deckPanel,"",Theme.ChevronRight,ButtonKind.Secondary,()=>ChangePage(1),"Next page");
            tips.SetToolTip(previousPage,"Previous page");tips.SetToolTip(nextPage,"Next page");
            bezel=new Card {BackColor=Theme.Background,BorderColor=Theme.Border,Radius=S(20)};deckPanel.Controls.Add(bezel);
            for(int i=0;i<13;i++) {
                int k=i;var b=new KeyButton {TabStop=true,AccessibleName="Button "+(i+1)};bezel.Controls.Add(b);keys[i]=b;
                b.Click+=(s,e)=>{if(k>=10){TryLaunch(UsageMeters.Binding(k-10));return;}selected=k;PaintSelection();LoadKey();};
                tips.SetToolTip(b,k>=10?UsageMeters.Names[k-10]+" usage · Click to open the usage page":"Click to edit");
            }
            widePreview=new RoundedPictureBox {Radius=S(14),AccessibleName="Wide display: clock / three audio knobs"};bezel.Controls.Add(widePreview);PreviewClock();
            tips.SetToolTip(widePreview,"Wide display · Shows the clock, or the dials while you turn them");
        }
        void BuildAudio() {
            audioPanel=new Card();canvas.Controls.Add(audioPanel);
            audioTitle=TextAt(audioPanel,"Audio dials",13,Theme.Text,true);
            audioSubtitle=TextAt(audioPanel,"Turn a dial to adjust · Press to mute",9,Theme.Muted);
            stepCaption=Caption(audioPanel,"Step");stepCaption.TextAlign=ContentAlignment.MiddleRight;
            step=new Stepper {Minimum=1,Maximum=10,Value=config.Step,Suffix="%",AccessibleName="Volume step (%)"};audioPanel.Controls.Add(step);
            overlayCaption=Caption(audioPanel,"Overlay");overlayCaption.TextAlign=ContentAlignment.MiddleRight;
            overlaySeconds=new Stepper {Minimum=1,Maximum=30,Value=config.OverlaySeconds,Suffix=" s",AccessibleName="Overlay time (sec)"};audioPanel.Controls.Add(overlaySeconds);
            tips.SetToolTip(step,"Volume change per dial notch");tips.SetToolTip(overlaySeconds,"How long the wide display shows volumes after a dial turn");
            step.ValueChanged+=(s,e)=>{config.Step=step.Value;try{SaveConfiguration();}catch(Exception ex){Feedback(ex.Message,true);}};
            overlaySeconds.ValueChanged+=(s,e)=>{config.OverlaySeconds=overlaySeconds.Value;wideState.HoldMilliseconds=config.OverlaySeconds*1000;try{SaveConfiguration();}catch(Exception ex){Feedback(ex.Message,true);}};
            string[] titles={"Microphone","Active app","Master output"};string[] glyphs={Theme.Microphone,Theme.App,Theme.Volume};
            for(int i=0;i<3;i++) {
                int k=i;
                var tile=new AudioTile {Title=titles[i],Glyph=glyphs[i],Detail=previewMode?"Audio preview":"Checking…"};audioPanel.Controls.Add(tile);tiles[i]=tile;
                minusButtons[i]=ButtonAt(tile,"",Theme.Remove,ButtonKind.Secondary,()=>ControlAudio(k,-step.Value,false),titles[i]+" volume down");
                muteButtons[i]=ButtonAt(tile,"Mute",Theme.Mute,ButtonKind.Secondary,()=>ControlAudio(k,0,true),titles[i]+" toggle mute");
                plusButtons[i]=ButtonAt(tile,"",Theme.Add,ButtonKind.Secondary,()=>ControlAudio(k,step.Value,false),titles[i]+" volume up");
            }
        }
        void BuildEditor() {
            editorPanel=new Card();canvas.Controls.Add(editorPanel);
            editorPreview=new RoundedPictureBox {Radius=S(12),AccessibleName="Button draft preview"};editorPanel.Controls.Add(editorPreview);
            editorTitle=TextAt(editorPanel,"Button 01",15,Theme.Text,true);
            selectionSummary=TextAt(editorPanel,"",9,Theme.Muted);
            editState=new StatusPill {Text="Saved",Tone=Tone.Positive,Font=UiFont(8.5f)};editorPanel.Controls.Add(editState);
            editorRule=Rule(editorPanel);
            actionCaption=Caption(editorPanel,"Action");
            kind=new SegmentedControl("Website","App / file / folder") {Glyphs=new[]{Theme.Globe,Theme.App},AccessibleName="Button action"};editorPanel.Controls.Add(kind);
            nameCaption=Caption(editorPanel,"Display name");
            labelField=FieldAt(editorPanel,"Display name");label=labelField.Input;label.MaxLength=40;
            targetCaption=Caption(editorPanel,"Website URL");
            fileButton=ButtonAt(editorPanel,"File…",Theme.Document,ButtonKind.Ghost,BrowseTarget,"Browse file");
            folderButton=ButtonAt(editorPanel,"Folder…",Theme.Folder,ButtonKind.Ghost,()=>{using(var dialog=new FolderBrowserDialog {Description="Choose a folder to open."})if(dialog.ShowDialog(this)==DialogResult.OK){target.Text=dialog.SelectedPath;kind.SelectedIndex=1;}},"Browse folder");
            targetField=FieldAt(editorPanel,"URL or launch path");target=targetField.Input;
            targetHint=TextAt(editorPanel,"",8.5f,Theme.Subtle);
            appsCaption=Caption(editorPanel,"Or pick an installed app");
            apps=SelectAt(editorPanel,"Select installed app",true);apps.Items.Add("Choose an app…");
            foreach(var path in Configuration.InstalledApps())apps.Items.Add(new AppItem(path));apps.SelectedIndex=0;
            apps.SelectedIndexChanged+=(s,e)=>{var item=apps.SelectedItem as AppItem;if(item==null)return;target.Text=item.Path;label.Text=Path.GetFileNameWithoutExtension(item.Path);kind.SelectedIndex=1;};
            imageCaption=Caption(editorPanel,"Custom icon (optional)");
            imageField=FieldAt(editorPanel,"Icon image (optional)");image=imageField.Input;
            imageBrowse=ButtonAt(editorPanel,"",Theme.Image,ButtonKind.Secondary,()=>{using(var dialog=new OpenFileDialog {Filter="Images|*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK)image.Text=dialog.FileName;},"Choose image");
            imageClear=ButtonAt(editorPanel,"",Theme.Clear,ButtonKind.Secondary,()=>image.Text="","Clear image");
            tips.SetToolTip(imageBrowse,"Choose image");tips.SetToolTip(imageClear,"Clear image");
            validation=TextAt(editorPanel,"",8.5f,Theme.Danger);validation.AccessibleName="Editor validation error";validation.TextAlign=ContentAlignment.TopLeft;
            footerRule=Rule(editorPanel);
            saveButton=ButtonAt(editorPanel,"Save and apply to device",Theme.Save,ButtonKind.Primary,SaveKey);saveButton.Font=UiFont(10);
            undoButton=ButtonAt(editorPanel,"Discard",Theme.Undo,ButtonKind.Secondary,CancelEdit,"Cancel edit");
            resetButton=ButtonAt(editorPanel,"Clear",Theme.Reset,ButtonKind.Secondary,()=>{label.Text="Empty";target.Text="";image.Text="";kind.SelectedIndex=0;EditorChanged();},"Reset button");
            testButton=ButtonAt(editorPanel,"Test",Theme.Play,ButtonKind.Secondary,()=>TryLaunch(CurrentEditor()),"Test action");
            tips.SetToolTip(target,"Web URLs must start with https:// or http://. Use full paths for files and folders. Leave blank for an empty key.");
            tips.SetToolTip(saveButton,"Save this key and apply it to the device (Ctrl+S)");tips.SetToolTip(undoButton,"Discard unsaved changes to this key (Esc)");
            tips.SetToolTip(resetButton,"Make this key empty (applies after saving)");tips.SetToolTip(testButton,"Run the action as currently edited");
        }

        void ResizeCanvas() {
            if(canvas==null)return;
            var size=new Size(Math.Max(ClientSize.Width,S(1100)),Math.Max(ClientSize.Height,S(800)));
            AutoScrollMinSize=size.Width>ClientSize.Width || size.Height>ClientSize.Height?size:Size.Empty;
            canvas.Bounds=new Rectangle(AutoScrollPosition,size);
        }
        void DoLayout() {
            if(canvas==null || statusBar==null)return;
            int width=canvas.ClientSize.Width,height=canvas.ClientSize.Height,pad=S(24),gap=S(16);
            LayoutHeader();
            headerRule.Bounds=new Rectangle(0,S(76),width,1);
            statusBar.Bounds=new Rectangle(0,height-S(36),width,S(36));
            feedback.Bounds=new Rectangle(pad,1,width-pad*2-S(340),S(35));shortcuts.Bounds=new Rectangle(width-pad-S(330),1,S(330),S(35));
            int top=S(76)+gap,bottom=height-S(36)-gap,editorWidth=S(360),leftWidth=width-pad*2-gap-editorWidth,audioHeight=S(202);
            editorPanel.Bounds=new Rectangle(width-pad-editorWidth,top,editorWidth,bottom-top);
            audioPanel.Bounds=new Rectangle(pad,bottom-audioHeight,leftWidth,audioHeight);
            deckPanel.Bounds=new Rectangle(pad,top,leftWidth,bottom-audioHeight-gap-top);
            LayoutDeck();LayoutAudio();LayoutEditor();
        }
        void LayoutHeader() {
            if(canvas==null)return;
            int width=canvas.ClientSize.Width,pad=S(24),y=S(21),h=S(34);
            appMark.Bounds=new Rectangle(pad,S(20),S(36),S(36));
            appTitle.Bounds=new Rectangle(pad+S(48),S(14),S(280),S(30));appSubtitle.Bounds=new Rectangle(pad+S(49),S(42),S(300),S(20));
            int x=width-pad;
            guideButton.Bounds=new Rectangle(x-=S(84),y,S(84),h);
            dataButton.Bounds=new Rectangle(x-=S(118)+S(4),y,S(118),h);
            x-=S(20);brightnessValue.Bounds=new Rectangle(x-=S(40),y,S(40),h);
            brightness.Bounds=new Rectangle(x-=S(120)+S(4),y,S(120),h);
            brightnessIcon.Bounds=new Rectangle(x-=S(26)+S(2),y,S(26),h);
            int pill=Math.Min(status.PreferredWidth(),S(320));status.Bounds=new Rectangle(x-S(20)-pill,S(23),pill,S(30));
        }
        void LayoutDeck() {
            int w=deckPanel.Width,h=deckPanel.Height,y=S(20),hh=S(34);
            deckTitle.Bounds=new Rectangle(S(20),S(14),S(200),S(30));
            int x=w-S(20);
            nextPage.Bounds=new Rectangle(x-=hh,y,hh,hh);
            pages.Width=S(180);pages.Location=new Point(x-=S(6)+pages.Width,y+(hh-pages.Height)/2);
            previousPage.Bounds=new Rectangle(x-=S(6)+hh,y,hh,hh);
            pagePosition.Bounds=new Rectangle(x-=S(8)+S(52),y,S(52),hh);
            deckSubtitle.Bounds=new Rectangle(S(20),S(44),Math.Max(0,x-S(36)),S(22));
            int availableWidth=w-S(40),availableHeight=h-S(80)-S(20),inner=S(14);
            int cell=Math.Max(S(40),Math.Min((availableWidth-inner*2)/5,(availableHeight-inner*2)/3));
            int bw=cell*5+inner*2,bh=cell*3+inner*2;
            bezel.Bounds=new Rectangle((w-bw)/2,S(80)+Math.Max(0,(availableHeight-bh)/2),bw,bh);
            for(int i=0;i<13;i++){int column=i<10?i%5:i-10,row=i<10?i/5:2;keys[i].Bounds=new Rectangle(inner+column*cell,inner+row*cell,cell,cell);}
            int inset=S(4);widePreview.Bounds=new Rectangle(inner+3*cell+inset,inner+2*cell+inset,2*cell-inset*2,cell-inset*2);
        }
        void LayoutAudio() {
            int w=audioPanel.Width,y=S(18),hh=S(34);
            audioTitle.Bounds=new Rectangle(S(20),S(14),S(200),S(30));
            int x=w-S(20);
            overlaySeconds.Bounds=new Rectangle(x-=S(112),y,S(112),hh);overlayCaption.Bounds=new Rectangle(x-=S(8)+S(60),y,S(60),hh);
            x-=S(12);step.Bounds=new Rectangle(x-=S(104),y,S(104),hh);stepCaption.Bounds=new Rectangle(x-=S(8)+S(40),y,S(40),hh);
            audioSubtitle.Bounds=new Rectangle(S(20),S(44),Math.Max(0,x-S(28)),S(20));
            int tileGap=S(12),tileWidth=(w-S(40)-tileGap*2)/3,tileHeight=S(122);
            for(int i=0;i<3;i++) {
                tiles[i].Bounds=new Rectangle(S(20)+i*(tileWidth+tileGap),S(66),tileWidth,tileHeight);
                int bw=S(38),bh=S(30),by=S(80),p=S(14),g=S(8);
                minusButtons[i].Bounds=new Rectangle(p,by,bw,bh);plusButtons[i].Bounds=new Rectangle(tileWidth-p-bw,by,bw,bh);
                muteButtons[i].Bounds=new Rectangle(p+bw+g,by,Math.Max(S(40),tileWidth-p*2-bw*2-g*2),bh);
            }
        }
        void LayoutEditor() {
            int w=editorPanel.Width,h=editorPanel.Height,p=S(20),fw=w-p*2,fh=S(38);
            editorPreview.Bounds=new Rectangle(p,p,S(64),S(64));
            int pill=Math.Max(S(76),editState.PreferredWidth());editState.Bounds=new Rectangle(w-p-pill,S(24),pill,S(26));
            editorTitle.Bounds=new Rectangle(S(98),S(18),Math.Max(0,w-S(98)-p-pill-S(8)),S(34));
            selectionSummary.Bounds=new Rectangle(S(99),S(54),w-S(99)-p,S(22));
            editorRule.Bounds=new Rectangle(p,S(102),fw,1);
            actionCaption.Bounds=new Rectangle(p,S(116),fw,S(22));kind.Bounds=new Rectangle(p,S(140),fw,fh);
            nameCaption.Bounds=new Rectangle(p,S(192),fw,S(22));labelField.Bounds=new Rectangle(p,S(216),fw,fh);
            folderButton.Bounds=new Rectangle(w-p-S(84),S(266),S(84),S(28));fileButton.Bounds=new Rectangle(folderButton.Left-S(4)-S(72),S(266),S(72),S(28));
            targetCaption.Bounds=new Rectangle(p,S(268),Math.Max(0,fileButton.Left-p-S(8)),S(22));
            targetField.Bounds=new Rectangle(p,S(296),fw,fh);targetHint.Bounds=new Rectangle(p,S(336),fw,S(20));
            appsCaption.Bounds=new Rectangle(p,S(368),fw,S(22));apps.Width=fw;apps.Location=new Point(p,S(392));
            imageCaption.Bounds=new Rectangle(p,S(446),fw,S(22));
            imageClear.Bounds=new Rectangle(w-p-fh,S(470),fh,fh);imageBrowse.Bounds=new Rectangle(imageClear.Left-S(6)-fh,S(470),fh,fh);
            imageField.Bounds=new Rectangle(p,S(470),imageBrowse.Left-S(8)-p,fh);
            int row=h-p-S(36),save=row-S(10)-S(44);
            validation.Bounds=new Rectangle(p,S(516),fw,Math.Max(S(20),Math.Min(S(44),save-S(16)-S(516))));
            footerRule.Bounds=new Rectangle(p,save-S(16),fw,1);
            saveButton.Bounds=new Rectangle(p,save,fw,S(44));
            int third=(fw-S(16))/3;
            undoButton.Bounds=new Rectangle(p,row,third,S(36));resetButton.Bounds=new Rectangle(p+third+S(8),row,third,S(36));testButton.Bounds=new Rectangle(p+(third+S(8))*2,row,fw-(third+S(8))*2,S(36));
        }
        static void SetTabOrder(Control parent) {
            int index=0;foreach(var child in parent.Controls.Cast<Control>().OrderBy(c=>c.Top).ThenBy(c=>c.Left)){child.TabIndex=index++;if(child.HasChildren)SetTabOrder(child);}
        }
        void ShowGuide() {
            MessageBox.Show(this,"Ulanzi Lite 0.3.0 · D200X\n\n1. Click one of the top 10 keys.\n2. Choose an action, name and optional icon.\n3. Press Save (Ctrl+S) to apply it to the device.\n\nThe bottom row shows AI usage; click a meter to open its usage page.\nDrafts are kept while you switch keys or pages. Esc discards the selected draft.\n\nClosing the window keeps Ulanzi Lite running in the system tray.\nUse Tray menu > Exit to quit completely.\n\nYou can edit and save without a connected device.\nThe saved page is applied automatically when the D200X connects.","Quick guide",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        void BuildTray() {
            var menu=new ContextMenuStrip(); menu.Items.Add("Open settings",null,(s,e)=>ShowEditor());menu.Items.Add("Exit",null,(s,e)=>{exiting=true;Close();});
            tray=new NotifyIcon {Icon=Icon,Text=WindowTitle,Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=(s,e)=>ShowEditor();
        }
        void ShowEditor() {Show();WindowState=FormWindowState.Normal;FitToScreen(false);Activate();}
        protected override void OnLoad(EventArgs e) {base.OnLoad(e);FitToScreen(true);}
        // Keep the whole window on one monitor, e.g. a portrait screen narrower than the default size.
        void FitToScreen(bool center) {
            var area=Screen.FromPoint(center?Cursor.Position:Location+new Size(Width/2,Height/2)).WorkingArea;
            MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));
            Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
            if(center || !area.Contains(Bounds))Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);
        }
        void ChangePage(int direction) {pages.SelectedIndex=(config.Page+direction+config.Pages.Count)%config.Pages.Count;}
        void PaintKeys() {
            for(int i=0;i<keys.Length;i++)RenderKey(i);
            PaintSelection();
        }
        void RenderKey(int index) {
            KeyBinding key;if(!drafts.TryGetValue(config.Page*13+index,out key))key=config.Pages[config.Page].Keys[index];
            var old=keys[index].BackgroundImage;keys[index].BackgroundImage=index>=10?UsageMeters.Render(index-10):Artwork.Render(key);if(old!=null)old.Dispose();keys[index].Invalidate();
        }
        void PaintSelection() {if(editState!=null)UpdateEditState();for(int i=0;i<13;i++)keys[i].Selected=i==selected;}
        void SaveConfiguration() {if(!previewMode)config.Save();}
        int DraftId {get{return config.Page*13+selected;}}
        void LoadKey() {
            loadingEditor=true;
            try {KeyBinding k;if(!drafts.TryGetValue(DraftId,out k))k=config.Pages[config.Page].Keys[selected];editorTitle.Text="Key "+(selected+1).ToString("00");label.Text=k.Label;target.Text=k.Target;image.Text=k.Image;kind.SelectedIndex=k.Kind=="application"?1:0;apps.SelectedIndex=0;}
            finally {loadingEditor=false;}ClearValidation();UpdateEditorContext();UpdateEditState();
        }
        void ClearValidation() {validation.Text="";labelField.Invalid=targetField.Invalid=imageField.Invalid=false;}
        void EditorChanged() {
            if(loadingEditor)return;
            websiteEditAt=uptime.ElapsedMilliseconds;
            var key=CurrentEditor();var saved=config.Pages[config.Page].Keys[selected];
            if(Configuration.Json.Serialize(key)==Configuration.Json.Serialize(saved))drafts.Remove(DraftId);else drafts[DraftId]=key;
            ClearValidation();UpdateEditorContext();RenderKey(selected);UpdateEditState();
        }
        void UpdateEditorContext() {
            bool application=kind.SelectedIndex==1;targetCaption.Text=application?"File or folder path":"Website URL";
            targetHint.Text=application?"Full path to an app, shortcut, file or folder":"Starts with https:// · Leave blank for an empty key";
            fileButton.Enabled=folderButton.Enabled=fileButton.Visible=folderButton.Visible=application;
        }
        void UpdateEditState() {
            bool dirty=drafts.ContainsKey(DraftId);editState.Text=dirty?"Unsaved":"Saved";editState.Tone=dirty?Tone.Warning:Tone.Positive;
            saveButton.Text=dirty?"Save changes and apply":"Save and apply to device";
            Text=WindowTitle+(drafts.Count>0?" · Editing "+drafts.Count:"");
            undoButton.Enabled=dirty;pagePosition.Text=(config.Page+1)+" / "+config.Pages.Count;
            KeyBinding preview;if(!drafts.TryGetValue(DraftId,out preview))preview=config.Pages[config.Page].Keys[selected];
            var old=editorPreview.Image;editorPreview.Image=Artwork.Render(preview);if(old!=null)old.Dispose();
            string kindName=preview.Kind=="application"?"App / file / folder":"Website";
            selectionSummary.Text=config.Pages[config.Page].Name+" · "+(String.IsNullOrEmpty(preview.Target)?"Empty key":kindName);tips.SetToolTip(selectionSummary,selectionSummary.Text);
            for(int i=0;i<keys.Length;i++){KeyBinding key;if(!drafts.TryGetValue(config.Page*13+i,out key))key=config.Pages[config.Page].Keys[i];bool draft=drafts.ContainsKey(config.Page*13+i);keys[i].AccessibleName="Button "+(i+1)+" · "+(i>=10?UsageMeters.Names[i-10]+" usage":key.Label)+(draft?" · Unsaved":"");keys[i].Text=draft?"●":"";}
            if(editorPanel!=null)LayoutEditor();
        }
        bool FinishEditing() {
            if(drafts.Count==0)return true;
            var answer=MessageBox.Show(this,"Unsaved keys: "+drafts.Count+". Save before exiting?","Save changes",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
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
            if((keyData==Keys.PageUp || keyData==Keys.PageDown) && !(ActiveControl is TextBox)){ChangePage(keyData==Keys.PageUp?-1:1);return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        void CancelEdit() {if(!drafts.Remove(DraftId))return;LoadKey();RenderKey(selected);Feedback("Discarded changes to this key.",false);}
        KeyBinding CurrentEditor() {return new KeyBinding {Label=label.Text.Trim(),Target=target.Text.Trim(),Kind=kind.SelectedIndex==1?"application":"website",Image=image.Text.Trim(),Color=config.Pages[config.Page].Keys[selected].Color};}
        async void SaveKey() {
            try {
                var key=CurrentEditor();
                try {key.Validate();}catch(ArgumentException ex){
                    validation.Text=ex.Message;tips.SetToolTip(validation,ex.Message);
                    FieldBox invalid=ex.Message.Contains("Name")?labelField:ex.Message.Contains("image") || ex.Message.Contains("Icon")?imageField:targetField;
                    invalid.Invalid=true;ActiveControl=invalid.Input;invalid.Input.Focus();invalid.Input.SelectAll();Feedback("Check your input. "+ex.Message,true);return;
                }
                ClearValidation();var page=config.Pages[config.Page];var old=page.Keys[selected];page.Keys[selected]=key;
                try{SaveConfiguration();}catch{page.Keys[selected]=old;throw;}
                drafts.Remove(DraftId);PaintKeys();UpdateEditState();Feedback("Saved. Applying to device…",false);await SyncDeck();
            }catch(Exception ex){Feedback(ex.Message,true);}
        }
        void BrowseTarget() {using(var dialog=new OpenFileDialog {Filter="Apps / shortcuts|*.exe;*.lnk|All files|*.*"})if(dialog.ShowDialog()==DialogResult.OK){target.Text=dialog.FileName;kind.SelectedIndex=1;if(label.Text=="Empty")label.Text=Path.GetFileNameWithoutExtension(dialog.FileName);}}
        void TryLaunch(KeyBinding key) {try{key.Launch();string message=String.IsNullOrEmpty(key.Target)?"This key is empty.":"Launched "+key.Label;Feedback(message,false);Program.Log(message);}catch(Exception ex){Program.Log("Launch failed: "+ex);Feedback(ex.Message,true);}}
        void HandleInput(int index,int state) {
            lastEvent="Input "+index+":"+state;Program.Log(lastEvent);
            if(index>=17 && index<=19) {if(state==1)ControlAudio(index-17,0,true);else if(state==2 || state==3)ControlAudio(index-17,(state==2?-1:1)*config.Step,false);}
            else if(state==1){if(index>=0 && index<13)TryLaunch(index>=10?UsageMeters.Binding(index-10):config.Pages[config.Page].Keys[index]);else if(index==15 || index==16)ChangePage(index==15?-1:1);}
        }
        void ShowAudio(int knob,AudioState result) {
            var tile=tiles[knob];tile.Available=result.Available;tile.Level=result.Level;tile.Muted=result.Muted;tile.Detail=result.Detail;tips.SetToolTip(tile,result.Detail);
            muteButtons[knob].Text=result.Available && result.Muted?"Unmute":"Mute";
        }
        void ControlAudio(int knob,int delta,bool toggle) {var result=audio.Control(knob,delta,toggle);ShowAudio(knob,result);string message=(knob==0?"Mic":knob==1?(audio.ForegroundName??"Active app"):"Master")+" · "+result.ToString();Program.Log("Audio knob="+knob+" delta="+delta+" toggle="+toggle+" available="+result.Available+" "+message+" · "+result.Detail);Feedback(message,!result.Available);wideState.Touch(uptime.ElapsedMilliseconds,knob);}
        async Task ApplyBrightness() {
            if(!brightnessDirty || brightness.Capture)return;
            brightnessDirty=false;
            try{SaveConfiguration();if(deck.IsConnected)await deck.Send(10,Encoding.ASCII.GetBytes(config.Brightness.ToString()),false);}catch(Exception ex){Feedback(ex.Message,true);}
        }
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
                for(int i=10;i<13;i++)RenderKey(i);
                if(deck.IsConnected){
                    await displayWriter.WaitAsync();
                    try {var bundle=await Task.Run(()=>displayImages.Usage());await deck.Send(1,bundle,true);await deck.Send(6,WindowMode(2),false);Program.Log("Usage display: full page ("+bundle.Length+" bytes)");}
                    finally {displayWriter.Release();}
                }
                usageRevision=revision;usageMinute=minute;
            }catch(Exception ex){Program.Log("Usage display update failed: "+ex.Message);}
            finally {updatingUsage=false;}
        }
        void RefreshAudio() {audio.TrackForeground();for(int i=0;i<3;i++)ShowAudio(i,audio.Control(i,0,false));}
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
        void Feedback(string text,bool error) {feedback.Text=text;feedback.ForeColor=error?Theme.Danger:Theme.Accent;if(error)Program.Log(text);}
        async Task SyncDeck() {
            if(syncing){pending=true;return;}if(!deck.IsConnected){Feedback("Saved. Changes apply when the D200X connects.",false);return;}
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
            }catch(Exception ex){Feedback("Saved · Device update failed: "+ex.Message,true);}finally{displayWriter.Release();syncing=false;}
        }
        protected override void Dispose(bool disposing) {if(disposing){if(timer!=null)timer.Dispose();tips.Dispose();deck.Dispose();if(tray!=null)tray.Dispose();if(Icon!=null)Icon.Dispose();foreach(var k in keys)if(k!=null && k.BackgroundImage!=null)k.BackgroundImage.Dispose();if(widePreview!=null && widePreview.Image!=null)widePreview.Image.Dispose();if(editorPreview!=null && editorPreview.Image!=null)editorPreview.Image.Dispose();if(appMark!=null && appMark.Image!=null)appMark.Image.Dispose();}base.Dispose(disposing);}
    }
}
