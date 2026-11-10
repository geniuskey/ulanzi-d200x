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
            TextAt(canvas,"D200X  /  나만의 데스크 컨트롤러",29,65,460,26,10,Color.FromArgb(172,187,205));
            status=TextAt(canvas,previewMode?"○ 장치 미연결 · 미리보기":"○ D200X 연결 확인 중…",680,26,470,27,10,accent);status.Anchor=AnchorStyles.Top|AnchorStyles.Right;status.TextAlign=ContentAlignment.MiddleRight;
            var help=ButtonAt(canvas,"사용 안내",1050,63,104,30,()=>MessageBox.Show(this,"Ulanzi Lite 0.3.0 · D200X\n\n1. 위 10개 버튼을 선택하고 동작을 편집합니다.\n2. Ctrl+S 또는 저장 버튼으로 장치에 적용합니다.\n\n아래 3개는 AI 구독 사용량입니다. 클릭하면 사용량 페이지를 엽니다.\n편집 내용은 버튼·페이지를 이동해도 유지됩니다.\nEsc: 선택 버튼의 편집 취소\n창 닫기: 트레이로 숨기기\n트레이 우클릭 → 종료: 완전히 종료\n\n장치가 없어도 설정을 편집하고 저장할 수 있습니다.\n연결되면 저장된 현재 페이지를 자동 적용합니다.","사용 안내",MessageBoxButtons.OK,MessageBoxIcon.Information));help.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            var folder=ButtonAt(canvas,"설정 폴더",934,63,104,30,()=>{try{Process.Start(new ProcessStartInfo(AppDomain.CurrentDomain.BaseDirectory){UseShellExecute=true});}catch(Exception ex){Feedback(ex.Message,true);}});folder.Anchor=AnchorStyles.Top|AnchorStyles.Right;

            var deckPanel=Card(canvas,26,114,776,440);deckPanel.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            TextAt(deckPanel,"버튼 구성",18,16,130,26,12,ForeColor);
            TextAt(deckPanel,"버튼을 선택해 오른쪽에서 편집하세요",18,46,470,22,9,Color.FromArgb(172,187,205));
            pages=SelectAt(deckPanel,380,17,250);pages.Anchor=AnchorStyles.Top|AnchorStyles.Right;pages.AccessibleName="페이지 선택";foreach(var page in config.Pages)pages.Items.Add(page.Name);pages.SelectedIndex=config.Page;
            pages.SelectedIndexChanged+=async(s,e)=>{config.Page=pages.SelectedIndex;PaintKeys();LoadKey();try{SaveConfiguration();await SyncDeck();}catch(Exception ex){Feedback(ex.Message,true);}};
            var previous=ButtonAt(deckPanel,"‹",640,17,36,32,()=>ChangePage(-1));previous.AccessibleName="이전 페이지";previous.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            var next=ButtonAt(deckPanel,"›",682,17,36,32,()=>ChangePage(1));next.AccessibleName="다음 페이지";next.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            pagePosition=TextAt(deckPanel,"",722,24,42,25,9,accent);pagePosition.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            tips.SetToolTip(previous,"이전 페이지");tips.SetToolTip(next,"다음 페이지");
            var grid=new TableLayoutPanel {Location=new Point(14,79),Size=new Size(748,218),ColumnCount=5,RowCount=2,BackColor=surface,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};deckPanel.Controls.Add(grid);
            for(int i=0;i<5;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
            for(int i=0;i<2;i++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            for(int i=0;i<13;i++) {
                int k=i;var b=ButtonAt(i<10?(Control)grid:deckPanel,"",i<10?0:35+(i-10)*149,i<10?0:321,99,99,()=>{if(k>=10){TryLaunch(UsageMeters.Binding(k-10));return;}selected=k;PaintSelection();LoadKey();});keys[i]=b;
                if(i<10){b.Anchor=AnchorStyles.None;b.Margin=new Padding(5);grid.SetCellPosition(b,new TableLayoutPanelCellPosition(i%5,i/5));}
                b.BackgroundImageLayout=ImageLayout.Zoom;b.AccessibleName="버튼 "+(i+1);tips.SetToolTip(b,k>=10?UsageMeters.Names[k-10]+" 사용량 · 클릭하여 사용량 페이지 열기":"클릭하여 편집 · 저장 후 장치 버튼으로 실행");
            }
            grid.Layout+=(s,e)=>{int side=Math.Max(1,Math.Min(grid.ClientSize.Width/5,grid.ClientSize.Height/2)-10);for(int i=0;i<10;i++)keys[i].Size=new Size(side,side);};
            TextAt(deckPanel,"AI 구독 사용량",18,299,220,20,8,Color.FromArgb(172,187,205));
            TextAt(deckPanel,"장치 가로 화면",470,299,260,20,8,Color.FromArgb(172,187,205));
            widePreview=new PictureBox {Location=new Point(470,321),Size=new Size(288,101),BackColor=background,SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="가로 화면: 시계 / 세 노브 볼륨",Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};deckPanel.Controls.Add(widePreview);PreviewClock();

            var audioPanel=Card(canvas,26,570,776,160);audioPanel.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            TextAt(audioPanel,"오디오 제어",18,13,200,26,12,ForeColor);
            TextAt(audioPanel,"노브를 돌려 볼륨 조절 · 눌러서 음소거",290,18,465,22,9,Color.FromArgb(172,187,205));
            var audioGrid=new TableLayoutPanel {Location=new Point(12,49),Size=new Size(752,101),ColumnCount=3,RowCount=1,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};audioPanel.Controls.Add(audioGrid);
            audioGrid.SuspendLayout();for(int i=0;i<3;i++)audioGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
            string[] titles={"01  마이크","02  현재 사용하는 앱","03  전체 출력"};
            for(int i=0;i<3;i++) {
                int k=i;
                var panel=new Panel {Size=new Size(238,101),Dock=DockStyle.Fill,Margin=new Padding(6,0,6,0),BackColor=background};audioGrid.Controls.Add(panel,i,0);
                var audioTitle=TextAt(panel,titles[i],10,7,140,22,9,accent);audioTitle.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;levels[i]=TextAt(panel,"—",155,4,72,30,17,ForeColor);levels[i].Anchor=AnchorStyles.Top|AnchorStyles.Right;levels[i].TextAlign=ContentAlignment.MiddleRight;
                details[i]=TextAt(panel,previewMode?"오디오 상태 미리보기":"확인 중…",10,35,217,19,8,Color.FromArgb(172,187,205));details[i].Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
                var minus=ButtonAt(panel,"−",10,62,42,28,()=>ControlAudio(k,-(int)step.Value,false));minus.AccessibleName=titles[i]+" 볼륨 낮추기";
                var mute=ButtonAt(panel,"음소거",59,62,112,28,()=>ControlAudio(k,0,true));mute.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;mute.AccessibleName=titles[i]+" 음소거 전환";
                var plus=ButtonAt(panel,"+",178,62,42,28,()=>ControlAudio(k,(int)step.Value,false));plus.Anchor=AnchorStyles.Top|AnchorStyles.Right;plus.AccessibleName=titles[i]+" 볼륨 높이기";
            }
            audioGrid.ResumeLayout(true);
            var settings=Card(canvas,26,746,776,72);settings.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            TextAt(settings,"장치 설정",18,16,112,25,11,ForeColor);
            TextAt(settings,"변경 즉시 저장",18,43,120,20,8,Color.FromArgb(172,187,205));
            step=NumberAt(settings,"회전 간격 (%)",155,8,1,10,config.Step);
            brightness=NumberAt(settings,"화면 밝기 (%)",345,8,0,100,config.Brightness);
            var overlaySeconds=NumberAt(settings,"볼륨 표시 (초)",535,8,1,30,config.OverlaySeconds);
            step.ValueChanged+=(s,e)=>{config.Step=(int)step.Value;try{SaveConfiguration();}catch(Exception ex){Feedback(ex.Message,true);}};
            brightness.ValueChanged+=async(s,e)=>{config.Brightness=(int)brightness.Value;try{SaveConfiguration();if(deck.IsConnected)await deck.Send(10,Encoding.ASCII.GetBytes(config.Brightness.ToString()),false);}catch(Exception ex){Feedback(ex.Message,true);}};
            overlaySeconds.ValueChanged+=(s,e)=>{config.OverlaySeconds=(int)overlaySeconds.Value;wideState.HoldMilliseconds=config.OverlaySeconds*1000;try{SaveConfiguration();}catch(Exception ex){Feedback(ex.Message,true);}};

            editorPanel=Card(canvas,820,114,334,704);editorPanel.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            editorTitle=TextAt(editorPanel,"버튼 01",20,16,177,32,17,ForeColor);
            editState=TextAt(editorPanel,"저장됨",211,24,105,24,9,accent);editState.TextAlign=ContentAlignment.TopRight;
            editorPreview=new PictureBox {Location=new Point(20,61),Size=new Size(62,62),BackColor=background,SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="편집 중인 버튼 미리보기"};editorPanel.Controls.Add(editorPreview);
            TextAt(editorPanel,"적용할 버튼 미리보기",96,65,220,24,10,ForeColor);
            selectionSummary=TextAt(editorPanel,"",96,94,220,23,8,Color.FromArgb(172,187,205));
            label=InputAt(editorPanel,"표시 이름",139);label.MaxLength=40;
            TextAt(editorPanel,"동작",20,205,286,23,9,Color.FromArgb(172,187,205));kind=SelectAt(editorPanel,20,231,286);kind.AccessibleName="버튼 동작";kind.Items.AddRange(new object[]{"사이트 열기","앱 / 파일 / 폴더 열기"});
            targetCaption=TextAt(editorPanel,"주소 또는 실행 경로",20,273,286,22,9,Color.FromArgb(172,187,205));
            target=new TextBox {Location=new Point(20,299),Size=new Size(286,28),BackColor=background,ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle,AccessibleName="주소 또는 실행 경로"};editorPanel.Controls.Add(target);
            targetHint=TextAt(editorPanel,"",20,329,286,22,8,Color.FromArgb(172,187,205));
            fileButton=ButtonAt(editorPanel,"파일 찾기",20,357,88,29,()=>BrowseTarget());
            folderButton=ButtonAt(editorPanel,"폴더 찾기",116,357,88,29,()=>{using(var dialog=new FolderBrowserDialog {Description="열 폴더를 선택하세요."})if(dialog.ShowDialog(this)==DialogResult.OK){target.Text=dialog.SelectedPath;kind.SelectedIndex=1;}});
            ButtonAt(editorPanel,"실행 확인",212,357,94,29,()=>TryLaunch(CurrentEditor()));
            TextAt(editorPanel,"설치된 앱에서 고르기",20,402,286,23,9,Color.FromArgb(172,187,205));apps=SelectAt(editorPanel,20,428,286);apps.AccessibleName="설치된 앱 선택";apps.Items.Add("앱 선택…");
            foreach(var path in Configuration.InstalledApps())apps.Items.Add(new AppItem(path));apps.SelectedIndex=0;
            apps.SelectedIndexChanged+=(s,e)=>{var item=apps.SelectedItem as AppItem;if(item==null)return;target.Text=item.Path;label.Text=Path.GetFileNameWithoutExtension(item.Path);kind.SelectedIndex=1;};
            image=InputAt(editorPanel,"아이콘 이미지 (선택)",470);
            ButtonAt(editorPanel,"이미지 선택",20,532,138,29,()=>{using(var dialog=new OpenFileDialog {Filter="이미지|*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK)image.Text=dialog.FileName;});
            ButtonAt(editorPanel,"이미지 비우기",168,532,138,29,()=>image.Text="");
            validation=TextAt(editorPanel,"",20,568,286,36,8,Color.FromArgb(250,174,132));validation.AccessibleName="편집 입력 오류";
            saveButton=ButtonAt(editorPanel,"저장 · 장치에 적용",20,609,286,42,()=>SaveKey());saveButton.BackColor=Color.FromArgb(37,91,76);saveButton.ForeColor=Color.White;
            undoButton=ButtonAt(editorPanel,"편집 취소",20,661,88,29,()=>CancelEdit());
            ButtonAt(editorPanel,"버튼 초기화",116,661,88,29,()=>{label.Text="빈 버튼";target.Text="";image.Text="";kind.SelectedIndex=0;EditorChanged();});
            ButtonAt(editorPanel,"저장본 실행",212,661,94,29,()=>TryLaunch(config.Pages[config.Page].Keys[selected]));
            feedback=TextAt(canvas,"버튼 선택 → 동작 편집 → 저장 · 장치에 적용",26,830,770,25,9,Color.FromArgb(172,187,205));feedback.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var shortcuts=TextAt(canvas,"Ctrl+S 저장  ·  Esc 편집 취소",820,830,334,25,9,Color.FromArgb(172,187,205));shortcuts.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            tips.SetToolTip(target,"웹 주소는 https:// 또는 http://로 시작해야 합니다. 파일과 폴더는 전체 경로를 입력하세요. 비워 두면 빈 버튼으로 저장합니다.");
            tips.SetToolTip(saveButton,"선택 버튼을 저장하고 장치에 적용합니다 (Ctrl+S).");
            // Keep the tab order consistent with the visual workflow in each container.
            SetTabOrder(canvas);ResumeLayout(true);
        }
        static void SetTabOrder(Control parent) {
            int index=0;foreach(var child in parent.Controls.Cast<Control>().OrderBy(c=>c.Top).ThenBy(c=>c.Left)){child.TabIndex=index++;if(child.HasChildren)SetTabOrder(child);}
        }
        void BuildTray() {
            var menu=new ContextMenuStrip(); menu.Items.Add("설정 열기",null,(s,e)=>ShowEditor());menu.Items.Add("종료",null,(s,e)=>{exiting=true;Close();});
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
            try {KeyBinding k;if(!drafts.TryGetValue(DraftId,out k))k=config.Pages[config.Page].Keys[selected];editorTitle.Text="버튼 "+(selected+1).ToString("00");label.Text=k.Label;target.Text=k.Target;image.Text=k.Image;kind.SelectedIndex=k.Kind=="application"?1:0;apps.SelectedIndex=0;}
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
            bool application=kind.SelectedIndex==1;targetCaption.Text=application?"파일 또는 폴더 경로":"사이트 주소";targetHint.Text=application?"전체 경로 입력 또는 아래에서 파일 선택":"https://example.com · 비워 두면 빈 버튼";
            fileButton.Enabled=folderButton.Enabled=application;
        }
        void UpdateEditState() {
            bool dirty=drafts.ContainsKey(DraftId);editState.Text=dirty?"● 저장 전":"저장됨";editState.ForeColor=dirty?Color.FromArgb(244,186,112):accent;
            saveButton.Text=dirty?"변경 저장 · 장치에 적용":"저장 · 장치에 적용";
            Text="Ulanzi Lite · D200X"+(drafts.Count>0?" · 편집 중 "+drafts.Count:"");
            undoButton.Enabled=dirty;pagePosition.Text=(config.Page+1)+" / "+config.Pages.Count;
            KeyBinding preview;if(!drafts.TryGetValue(DraftId,out preview))preview=config.Pages[config.Page].Keys[selected];
            var old=editorPreview.Image;editorPreview.Image=Artwork.Render(preview);if(old!=null)old.Dispose();
            selectionSummary.Text=config.Pages[config.Page].Name+" · "+(dirty?"저장하면 장치에 반영":"저장된 버튼");tips.SetToolTip(selectionSummary,selectionSummary.Text);
            for(int i=0;i<keys.Length;i++){KeyBinding key;if(!drafts.TryGetValue(config.Page*13+i,out key))key=config.Pages[config.Page].Keys[i];keys[i].AccessibleName="버튼 "+(i+1)+" · "+(i>=10?UsageMeters.Names[i-10]+" 사용량":key.Label)+(drafts.ContainsKey(config.Page*13+i)?" · 저장 전":"");keys[i].Text=drafts.ContainsKey(config.Page*13+i)?"●":"";keys[i].TextAlign=ContentAlignment.TopRight;keys[i].ForeColor=Color.FromArgb(244,186,112);}
        }
        bool FinishEditing() {
            if(drafts.Count==0)return true;
            var answer=MessageBox.Show(this,"저장하지 않은 버튼 "+drafts.Count+"개가 있습니다. 저장하고 종료할까요?","편집 내용 저장",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            if(answer==DialogResult.Cancel)return false;if(answer==DialogResult.No)return true;
            var originals=new Dictionary<int,KeyBinding>();
            try {
                foreach(var item in drafts)item.Value.Validate();
                foreach(var item in drafts){originals[item.Key]=config.Pages[item.Key/13].Keys[item.Key%13];config.Pages[item.Key/13].Keys[item.Key%13]=item.Value;}
                SaveConfiguration();drafts.Clear();return true;
            }catch(Exception ex){foreach(var item in originals)config.Pages[item.Key/13].Keys[item.Key%13]=item.Value;MessageBox.Show(this,ex.Message,"저장하지 못했습니다",MessageBoxButtons.OK,MessageBoxIcon.Warning);return false;}
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
            if(keyData==(Keys.Control|Keys.S)){SaveKey();return true;}
            if(keyData==Keys.Escape && drafts.ContainsKey(DraftId)){CancelEdit();return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        void CancelEdit() {if(!drafts.Remove(DraftId))return;LoadKey();RenderKey(selected);Feedback("선택 버튼의 편집을 취소했습니다.",false);}
        KeyBinding CurrentEditor() {return new KeyBinding {Label=label.Text.Trim(),Target=target.Text.Trim(),Kind=kind.SelectedIndex==1?"application":"website",Image=image.Text.Trim(),Color=config.Pages[config.Page].Keys[selected].Color};}
        async void SaveKey() {
            try {
                var key=CurrentEditor();
                try {key.Validate();}catch(ArgumentException ex){
                    validation.Text=ex.Message;tips.SetToolTip(validation,ex.Message);
                    TextBox invalid=ex.Message.Contains("이름")?label:ex.Message.Contains("이미지") || ex.Message.Contains("아이콘")?image:target;
                    ActiveControl=invalid;invalid.Focus();invalid.SelectAll();Feedback("입력 내용을 확인해 주세요. "+ex.Message,true);return;
                }
                validation.Text="";var page=config.Pages[config.Page];var old=page.Keys[selected];page.Keys[selected]=key;
                try{SaveConfiguration();}catch{page.Keys[selected]=old;throw;}
                drafts.Remove(DraftId);PaintKeys();UpdateEditState();Feedback("저장했습니다. 장치에 적용 중…",false);await SyncDeck();
            }catch(Exception ex){Feedback(ex.Message,true);}
        }
        void BrowseTarget() {using(var dialog=new OpenFileDialog {Filter="앱 / 바로가기|*.exe;*.lnk|모든 파일|*.*"})if(dialog.ShowDialog()==DialogResult.OK){target.Text=dialog.FileName;kind.SelectedIndex=1;if(label.Text=="빈 버튼")label.Text=Path.GetFileNameWithoutExtension(dialog.FileName);}}
        void TryLaunch(KeyBinding key) {try{key.Launch();string message=String.IsNullOrEmpty(key.Target)?"비어 있는 버튼입니다.":key.Label+" 실행 요청";Feedback(message,false);Program.Log(message);}catch(Exception ex){Program.Log("Launch failed: "+ex);Feedback(ex.Message,true);}}
        void HandleInput(int index,int state) {
            lastEvent="입력 "+index+":"+state;Program.Log(lastEvent);
            if(index>=17 && index<=19) {if(state==1)ControlAudio(index-17,0,true);else if(state==2 || state==3)ControlAudio(index-17,(state==2?-1:1)*config.Step,false);}
            else if(state==1){if(index>=0 && index<13)TryLaunch(index>=10?UsageMeters.Binding(index-10):config.Pages[config.Page].Keys[index]);else if(index==15 || index==16)ChangePage(index==15?-1:1);}
        }
        void ControlAudio(int knob,int delta,bool toggle) {var result=audio.Control(knob,delta,toggle);levels[knob].Text=result.ToString();details[knob].Text=result.Detail;tips.SetToolTip(details[knob],result.Detail);string message=(knob==0?"마이크":knob==1?(audio.ForegroundName??"현재 앱"):"전체 출력")+" · "+result.ToString();Program.Log("Audio knob="+knob+" delta="+delta+" toggle="+toggle+" available="+result.Available+" "+message+" · "+result.Detail);Feedback(message,!result.Available);wideState.Touch(uptime.ElapsedMilliseconds,knob);}
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
            if(syncing){pending=true;return;}if(!deck.IsConnected){Feedback("설정은 저장되었습니다. D200X 연결 시 적용됩니다.",false);return;}
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
                    Feedback("장치에 적용했습니다 · "+config.Pages[config.Page].Name,false);Program.Log("Bundle sent: "+bundle.Length+" bytes");
                }while(pending && deck.IsConnected);
            }catch(Exception ex){Feedback("설정 저장됨 · 장치 적용 실패: "+ex.Message,true);}finally{displayWriter.Release();syncing=false;}
        }
        protected override void Dispose(bool disposing) {if(disposing){if(timer!=null)timer.Dispose();tips.Dispose();deck.Dispose();if(tray!=null)tray.Dispose();if(Icon!=null)Icon.Dispose();foreach(var k in keys)if(k!=null && k.BackgroundImage!=null)k.BackgroundImage.Dispose();if(widePreview!=null && widePreview.Image!=null)widePreview.Image.Dispose();if(editorPreview!=null && editorPreview.Image!=null)editorPreview.Image.Dispose();}base.Dispose(disposing);}
        sealed class AppItem {public readonly string Path;public AppItem(string path){Path=path;}public override string ToString(){return System.IO.Path.GetFileNameWithoutExtension(Path);}}
    }
}
