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
        readonly Deck deck=new Deck(); readonly AudioController audio=new AudioController();
        readonly bool previewMode; Configuration config; readonly Button[] keys=new Button[13]; readonly Label[] levels=new Label[3]; readonly Label[] details=new Label[3];
        readonly Color background=Color.FromArgb(15,19,27),surface=Color.FromArgb(24,30,41),accent=Color.FromArgb(115,221,185);
        Label status,feedback,editorTitle; TextBox label,target,image; ComboBox kind,apps,pages; NumericUpDown step,brightness;
        readonly Dictionary<int,KeyBinding> drafts=new Dictionary<int,KeyBinding>();
        readonly ToolTip tips=new ToolTip {AutoPopDelay=8000};
        Button saveButton; Label editState; bool loadingEditor;
        NotifyIcon tray; System.Windows.Forms.Timer timer; int selected=0,ticks; bool exiting,syncing,pending; string lastEvent="";
        readonly WideScreenState wideState=new WideScreenState();
        readonly Stopwatch uptime=Stopwatch.StartNew();
        readonly SemaphoreSlim displayWriter=new SemaphoreSlim(1,1);
        PictureBox widePreview;bool updatingWide;long nextWideAttempt;
        public MainForm(Configuration configuration) : this(configuration,false) { }
        public MainForm(Configuration configuration,bool preview) {
            previewMode=preview;config=configuration;wideState.HoldMilliseconds=config.OverlaySeconds*1000; Text="Ulanzi Lite · D200X"; BackColor=background; ForeColor=Color.FromArgb(232,238,245);
            Font=new Font(Typography.UiFontName,9); ClientSize=new Size(1120,800); MinimumSize=new Size(860,640); AutoScaleMode=AutoScaleMode.Dpi;
            AutoScroll=true;AutoScrollMinSize=new Size(1120,800);
            DoubleBuffered=true; Icon=Branding.CreateIcon(); StartPosition=FormStartPosition.CenterScreen;
            BuildUI(); if(!preview)BuildTray(); PaintKeys(); LoadKey();
            label.TextChanged+=(s,e)=>EditorChanged();target.TextChanged+=(s,e)=>EditorChanged();image.TextChanged+=(s,e)=>EditorChanged();kind.SelectedIndexChanged+=(s,e)=>EditorChanged();
            deck.Status+=(ok,text)=>OnUI(()=>{status.Text=text; status.ForeColor=ok?accent:Color.FromArgb(244,186,112); Program.Log(text);});
            deck.Input+=(index,state)=>OnUI(()=>HandleInput(index,state));
            deck.Connected+=()=>OnUI(async()=>await SyncDeck());
            if(!preview)Shown+=(s,e)=>{deck.Start(); RefreshAudio();};
            timer=new System.Windows.Forms.Timer {Interval=250}; timer.Tick+=async(s,e)=>{audio.TrackForeground();if(++ticks%4==0 && Visible){RefreshAudio();if(!wideState.WantsAudio(uptime.ElapsedMilliseconds))PreviewClock();}await PumpWideDisplay();}; if(!preview)timer.Start();
            FormClosing+=(s,e)=>{if(!exiting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}else{if(!FinishEditing()){e.Cancel=true;exiting=false;return;}timer.Stop();deck.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}}};
        }
        void OnUI(Action action) {if(!IsDisposed && IsHandleCreated)try{BeginInvoke(action);}catch(InvalidOperationException){}}
        Label TextAt(Control parent,string text,int x,int y,int w,int h,float size,Color color) {
            var l=new Label {Text=text,Location=new Point(x,y),Size=new Size(w,h),ForeColor=color,Font=new Font(Typography.UiFontName,size),BackColor=Color.Transparent}; l.AutoEllipsis=true; parent.Controls.Add(l);return l;
        }
        Button ButtonAt(Control parent,string text,int x,int y,int w,int h,Action action) {
            var b=new Button {Text=text,Location=new Point(x,y),Size=new Size(w,h),FlatStyle=FlatStyle.Flat,BackColor=surface,ForeColor=ForeColor,Cursor=Cursors.Hand,TabStop=true};
            b.AccessibleName=text; b.FlatAppearance.BorderColor=Color.FromArgb(52,65,81); b.FlatAppearance.MouseOverBackColor=Color.FromArgb(43,55,70);
            b.Click+=(s,e)=>action(); parent.Controls.Add(b); return b;
        }
        TextBox InputAt(string caption,int y) {
            TextAt(this,caption,817,y,270,22,9,Color.FromArgb(158,175,195));
            var t=new TextBox {Location=new Point(817,y+26),Size=new Size(270,28),BackColor=surface,ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle,AccessibleName=caption};Controls.Add(t);return t;
        }
        ComboBox SelectAt(int x,int y,int w) {
            var c=new ThemeComboBox {Location=new Point(x,y),Size=new Size(w,28),DropDownStyle=ComboBoxStyle.DropDownList,BackColor=surface,ForeColor=ForeColor,FlatStyle=FlatStyle.Flat,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=22};
            c.DrawItem+=(s,e)=>{if(e.Index<0)return;using(var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(44,66,70):surface))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,c.Items[e.Index].ToString(),Font,e.Bounds,ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.Left);};
            Controls.Add(c);return c;
        }
        void BuildUI() {
            TextAt(this,"ULANZI / LITE",30,23,420,43,25,ForeColor);
            TextAt(this,"버튼은 바로 실행하고, 소리는 손끝에서.",32,73,650,28,11,Color.FromArgb(156,173,193));
            status=TextAt(this,"D200X 연결 확인 중…",755,28,340,25,10,accent);
            ButtonAt(this,"설정 폴더",817,65,128,29,()=>{try{Process.Start(new ProcessStartInfo(AppDomain.CurrentDomain.BaseDirectory){UseShellExecute=true});}catch(Exception ex){Feedback(ex.Message,true);}});
            ButtonAt(this,"사용 안내",959,65,128,29,()=>MessageBox.Show(this,"Ulanzi Lite 0.2.0 · D200X\n\n1. 버튼을 선택하고 동작을 편집합니다.\n2. Ctrl+S 또는 저장 버튼으로 적용합니다.\n\n편집 내용은 버튼·페이지를 이동해도 유지됩니다.\n저장 전에는 장치 버튼에 반영되지 않습니다.\nEsc: 선택 버튼의 편집 취소\n창 닫기: 트레이로 숨기기\n트레이 우클릭 → 종료: 완전히 종료\n\n설정·백업·로그는 실행 파일 옆에 저장됩니다.\n자세한 안내는 함께 제공된 README.md를 확인하세요.","사용 안내",MessageBoxButtons.OK,MessageBoxIcon.Information));
            var divider=new Panel {Location=new Point(790,120),Size=new Size(1,632),BackColor=Color.FromArgb(44,55,71)};Controls.Add(divider);
            pages=SelectAt(32,120,235); pages.AccessibleName="페이지 선택"; foreach(var page in config.Pages)pages.Items.Add(page.Name); pages.SelectedIndex=config.Page;
            pages.SelectedIndexChanged+=async(s,e)=>{config.Page=pages.SelectedIndex;PaintKeys();LoadKey();try{SaveConfiguration();await SyncDeck();}catch(Exception ex){Feedback(ex.Message,true);}};
            ButtonAt(this,"‹",280,119,40,31,()=>ChangePage(-1)); ButtonAt(this,"›",326,119,40,31,()=>ChangePage(1));
            TextAt(this,"버튼 선택 → 오른쪽에서 편집",413,125,360,26,9,Color.FromArgb(156,173,193));
            for(int i=0;i<13;i++) {
                int k=i; var b=ButtonAt(this,"",32+(i%5)*147,170+(i/5)*125,135,113,()=>{selected=k;PaintSelection();LoadKey();}); keys[i]=b;
                b.BackgroundImageLayout=ImageLayout.Zoom; b.AccessibleName="버튼 "+(i+1);tips.SetToolTip(b,"클릭하여 편집 · 장치 버튼을 누르면 저장된 동작 실행");
            }
            widePreview=new PictureBox {Location=new Point(473,420),Size=new Size(282,113),BackColor=surface,SizeMode=PictureBoxSizeMode.StretchImage,AccessibleName="가로 화면: 시계 / 세 노브 볼륨"};Controls.Add(widePreview);PreviewClock();
            TextAt(this,"AUDIO CONTROLS",32,563,400,25,10,Color.FromArgb(156,173,193));
            TextAt(this,"볼륨 표시 시간",490,563,140,25,9,Color.FromArgb(156,173,193));
            var overlaySeconds=new NumericUpDown {Location=new Point(625,560),Size=new Size(75,28),Minimum=1,Maximum=30,Value=config.OverlaySeconds,BackColor=surface,ForeColor=ForeColor,AccessibleName="볼륨 표시 시간 (초)"};Controls.Add(overlaySeconds);TextAt(this,"초",710,563,30,25,9,ForeColor);
            overlaySeconds.ValueChanged+=(s,e)=>{config.OverlaySeconds=(int)overlaySeconds.Value;wideState.HoldMilliseconds=config.OverlaySeconds*1000;try{SaveConfiguration();}catch(Exception ex){Feedback(ex.Message,true);}};
            string[] titles={"01   마이크","02   현재 사용하는 앱","03   전체 출력"};
            for(int i=0;i<3;i++) {
                int k=i; var panel=new Panel {Location=new Point(32+i*246,600),Size=new Size(233,149),BackColor=surface}; Controls.Add(panel);
                TextAt(panel,titles[i],14,12,215,24,10,accent); levels[i]=TextAt(panel,"—",14,42,215,35,19,ForeColor);
                details[i]=TextAt(panel,"",14,80,215,23,8,Color.FromArgb(156,173,193));
                ButtonAt(panel,"−",14,110,43,28,()=>ControlAudio(k,-(int)step.Value,false));
                ButtonAt(panel,"음소거",64,110,92,28,()=>ControlAudio(k,0,true));
                ButtonAt(panel,"+",163,110,55,28,()=>ControlAudio(k,(int)step.Value,false));
            }
            editorTitle=TextAt(this,"버튼 01",817,121,140,35,18,ForeColor);
            editState=TextAt(this,"저장됨",961,131,126,24,9,accent);
            label=InputAt("표시 이름",171);label.MaxLength=40;
            TextAt(this,"동작",817,236,270,23,9,Color.FromArgb(158,175,195)); kind=SelectAt(817,263,270);kind.AccessibleName="버튼 동작";kind.Items.AddRange(new object[]{"사이트 열기","앱 / 파일 / 폴더 열기"});
            target=InputAt("주소 또는 실행 경로",305);
            ButtonAt(this,"파일",817,366,64,29,()=>BrowseTarget());
            ButtonAt(this,"폴더",887,366,64,29,()=>{using(var dialog=new FolderBrowserDialog {Description="열 폴더를 선택하세요."})if(dialog.ShowDialog(this)==DialogResult.OK){target.Text=dialog.SelectedPath;kind.SelectedIndex=1;}});
            ButtonAt(this,"실행 확인",959,366,128,29,()=>TryLaunch(CurrentEditor()));
            TextAt(this,"설치된 앱에서 고르기",817,412,270,23,9,Color.FromArgb(158,175,195)); apps=SelectAt(817,440,270);apps.AccessibleName="설치된 앱 선택";apps.Items.Add("앱 선택…");
            foreach(var path in Configuration.InstalledApps())apps.Items.Add(new AppItem(path));apps.SelectedIndex=0;
            apps.SelectedIndexChanged+=(s,e)=>{var item=apps.SelectedItem as AppItem;if(item==null)return;target.Text=item.Path;label.Text=Path.GetFileNameWithoutExtension(item.Path);kind.SelectedIndex=1;};
            image=InputAt("아이콘 이미지 (선택)",482);
            ButtonAt(this,"이미지 선택",817,544,132,29,()=>{using(var dialog=new OpenFileDialog {Filter="이미지|*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog()==DialogResult.OK)image.Text=dialog.FileName;});
            ButtonAt(this,"비우기",959,544,128,29,()=>image.Text="");
            saveButton=ButtonAt(this,"저장 · 장치에 적용",817,592,270,43,()=>SaveKey());saveButton.BackColor=Color.FromArgb(37,91,76);saveButton.ForeColor=Color.White;
            ButtonAt(this,"버튼 초기화",817,647,132,30,()=>{label.Text="빈 버튼";target.Text="";image.Text="";kind.SelectedIndex=0;EditorChanged();});
            ButtonAt(this,"선택 버튼 실행",959,647,128,30,()=>TryLaunch(config.Pages[config.Page].Keys[selected]));
            TextAt(this,"회전 간격 (%)",817,695,130,24,9,Color.FromArgb(158,175,195));
            step=new NumericUpDown {Location=new Point(817,724),Size=new Size(120,28),Minimum=1,Maximum=10,Value=config.Step,BackColor=surface,ForeColor=ForeColor,AccessibleName="볼륨 회전 간격 (%)"};Controls.Add(step);
            TextAt(this,"화면 밝기 (%)",961,695,130,24,9,Color.FromArgb(158,175,195));
            brightness=new NumericUpDown {Location=new Point(961,724),Size=new Size(126,28),Minimum=0,Maximum=100,Value=config.Brightness,BackColor=surface,ForeColor=ForeColor,AccessibleName="장치 화면 밝기 (%)"}; Controls.Add(brightness);
            step.ValueChanged+=(s,e)=>{config.Step=(int)step.Value;try{SaveConfiguration();}catch(Exception ex){Feedback(ex.Message,true);}};
            brightness.ValueChanged+=async(s,e)=>{config.Brightness=(int)brightness.Value;try{SaveConfiguration();if(deck.IsConnected)await deck.Send(10,Encoding.ASCII.GetBytes(config.Brightness.ToString()),false);}catch(Exception ex){Feedback(ex.Message,true);}};
            feedback=TextAt(this,"준비되었습니다. 버튼을 선택해 편집하세요.",32,764,720,27,9,Color.FromArgb(156,173,193));
            TextAt(this,"Ctrl+S 저장  ·  Esc 편집 취소",817,764,275,27,9,Color.FromArgb(156,173,193));
            tips.SetToolTip(target,"웹 주소는 https:// 또는 http://로 시작해야 합니다. 파일과 폴더는 전체 경로를 입력하세요.");
            tips.SetToolTip(saveButton,"선택 버튼을 저장하고 장치에 적용합니다 (Ctrl+S).");
        }
        void BuildTray() {
            var menu=new ContextMenuStrip(); menu.Items.Add("설정 열기",null,(s,e)=>ShowEditor());menu.Items.Add("종료",null,(s,e)=>{exiting=true;Close();});
            tray=new NotifyIcon {Icon=Icon,Text="Ulanzi Lite · D200X",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=(s,e)=>ShowEditor();
        }
        void ShowEditor() {Show();WindowState=FormWindowState.Normal;Activate();}
        void ChangePage(int direction) {pages.SelectedIndex=(config.Page+direction+config.Pages.Count)%config.Pages.Count;}
        void PaintKeys() {
            for(int i=0;i<keys.Length;i++) {var old=keys[i].BackgroundImage;keys[i].BackgroundImage=Artwork.Render(config.Pages[config.Page].Keys[i]); if(old!=null)old.Dispose();}
            PaintSelection();
        }
        void PaintSelection() {if(editState!=null)UpdateEditState();for(int i=0;i<13;i++){keys[i].FlatAppearance.BorderSize=i==selected?3:1;keys[i].FlatAppearance.BorderColor=i==selected?accent:Color.FromArgb(52,65,81);}}
        void SaveConfiguration() {if(!previewMode)config.Save();}
        int DraftId {get{return config.Page*13+selected;}}
        void LoadKey() {
            loadingEditor=true;
            try {KeyBinding k;if(!drafts.TryGetValue(DraftId,out k))k=config.Pages[config.Page].Keys[selected];editorTitle.Text="버튼 "+(selected+1).ToString("00");label.Text=k.Label;target.Text=k.Target;image.Text=k.Image;kind.SelectedIndex=k.Kind=="application"?1:0;apps.SelectedIndex=0;}
            finally {loadingEditor=false;}UpdateEditState();
        }
        void EditorChanged() {
            if(loadingEditor)return;
            var key=CurrentEditor();var saved=config.Pages[config.Page].Keys[selected];
            if(Configuration.Json.Serialize(key)==Configuration.Json.Serialize(saved))drafts.Remove(DraftId);else drafts[DraftId]=key;
            UpdateEditState();
        }
        void UpdateEditState() {
            bool dirty=drafts.ContainsKey(DraftId);editState.Text=dirty?"● 저장 전":"저장됨";editState.ForeColor=dirty?Color.FromArgb(244,186,112):accent;
            saveButton.Text=dirty?"변경 저장 · 장치에 적용":"저장 · 장치에 적용";
            Text="Ulanzi Lite · D200X"+(drafts.Count>0?" · 편집 중 "+drafts.Count:"");
            for(int i=0;i<keys.Length;i++){var key=config.Pages[config.Page].Keys[i];keys[i].AccessibleName="버튼 "+(i+1)+" · "+key.Label+(drafts.ContainsKey(config.Page*13+i)?" · 저장 전":"");keys[i].Text=drafts.ContainsKey(config.Page*13+i)?"●":"";keys[i].TextAlign=ContentAlignment.TopRight;}
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
            if(keyData==Keys.Escape && drafts.ContainsKey(DraftId)){drafts.Remove(DraftId);LoadKey();Feedback("선택 버튼의 편집을 취소했습니다.",false);return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        KeyBinding CurrentEditor() {return new KeyBinding {Label=label.Text.Trim(),Target=target.Text.Trim(),Kind=kind.SelectedIndex==1?"application":"website",Image=image.Text.Trim(),Color=config.Pages[config.Page].Keys[selected].Color};}
        async void SaveKey() {
            try {
                var key=CurrentEditor();key.Validate();var page=config.Pages[config.Page];var old=page.Keys[selected];page.Keys[selected]=key;
                try{SaveConfiguration();}catch{page.Keys[selected]=old;throw;}
                drafts.Remove(DraftId);PaintKeys();UpdateEditState();Feedback("저장했습니다. 장치에 적용 중…",false);await SyncDeck();
            }catch(Exception ex){Feedback(ex.Message,true);}
        }
        void BrowseTarget() {using(var dialog=new OpenFileDialog {Filter="앱 / 바로가기|*.exe;*.lnk|모든 파일|*.*"})if(dialog.ShowDialog()==DialogResult.OK){target.Text=dialog.FileName;kind.SelectedIndex=1;if(label.Text=="빈 버튼")label.Text=Path.GetFileNameWithoutExtension(dialog.FileName);}}
        void TryLaunch(KeyBinding key) {try{key.Launch();string message=String.IsNullOrEmpty(key.Target)?"비어 있는 버튼입니다.":key.Label+" 실행 요청";Feedback(message,false);Program.Log(message);}catch(Exception ex){Program.Log("Launch failed: "+ex);Feedback(ex.Message,true);}}
        void HandleInput(int index,int state) {
            lastEvent="입력 "+index+":"+state;Program.Log(lastEvent);
            if(index>=17 && index<=19) {if(state==1)ControlAudio(index-17,0,true);else if(state==2 || state==3)ControlAudio(index-17,(state==2?-1:1)*config.Step,false);}
            else if(state==1){if(index>=0 && index<13)TryLaunch(config.Pages[config.Page].Keys[index]);else if(index==15 || index==16)ChangePage(index==15?-1:1);}
        }
        void ControlAudio(int knob,int delta,bool toggle) {var result=audio.Control(knob,delta,toggle);levels[knob].Text=result.ToString();details[knob].Text=result.Detail;string message=(knob==0?"마이크":knob==1?(audio.ForegroundName??"현재 앱"):"전체 출력")+" · "+result.ToString();Program.Log("Audio knob="+knob+" delta="+delta+" toggle="+toggle+" available="+result.Available+" "+message+" · "+result.Detail);Feedback(message,!result.Available);wideState.Touch(uptime.ElapsedMilliseconds,knob);}
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
                byte[] bundle=await Task.Run(()=>Artwork.WideBundle(png));
                await deck.Send(13,bundle,true);
                await deck.Send(6,WindowMode(2),false);
                bool wasAudio=wideState.SentAudio;wideState.MarkSent(showAudio,version,clockSecond);
                if(showAudio || wasAudio)Program.Log("Wide display: "+(showAudio?"audio":"clock image")+" ("+bundle.Length+" bytes)");
            }catch(Exception ex){wideState.MarkSent(true,-1);nextWideAttempt=uptime.ElapsedMilliseconds+1000;Program.Log("Wide display update failed: "+ex.Message);}
            finally {displayWriter.Release();updatingWide=false;}
        }
        void RefreshAudio() {audio.TrackForeground();for(int i=0;i<3;i++){var result=audio.Control(i,0,false);levels[i].Text=result.Available?result.ToString():"—";details[i].Text=result.Detail;}}
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
                    byte[] bundle=await Task.Run(()=>{using(var clock=WideArtwork.Clock(initialClock))return Artwork.Bundle(snapshot,WideArtwork.Encode(clock));});
                    await deck.Send(11,Encoding.UTF8.GetBytes("{\"Align\":\"bottom\",\"Color\":16777215,\"FontName\":\"Roboto\",\"ShowTitle\":false,\"Size\":10,\"Weight\":80}"),false);
                    await deck.Send(1,bundle,true);
                    await deck.Send(6,WindowMode(2),false);wideState.Reset();wideState.MarkSent(false,wideState.Version,initialClock.Ticks/TimeSpan.TicksPerSecond);PreviewClock();
                    await deck.Send(10,Encoding.ASCII.GetBytes(config.Brightness.ToString()),false);
                    Feedback("장치에 적용했습니다 · "+config.Pages[config.Page].Name,false);Program.Log("Bundle sent: "+bundle.Length+" bytes");
                }while(pending && deck.IsConnected);
            }catch(Exception ex){Feedback("설정 저장됨 · 장치 적용 실패: "+ex.Message,true);}finally{displayWriter.Release();syncing=false;}
        }
        protected override void Dispose(bool disposing) {if(disposing){if(timer!=null)timer.Dispose();tips.Dispose();deck.Dispose();if(tray!=null)tray.Dispose();if(Icon!=null)Icon.Dispose();foreach(var k in keys)if(k!=null && k.BackgroundImage!=null)k.BackgroundImage.Dispose();if(widePreview!=null && widePreview.Image!=null)widePreview.Image.Dispose();}base.Dispose(disposing);}
        sealed class AppItem {public readonly string Path;public AppItem(string path){Path=path;}public override string ToString(){return System.IO.Path.GetFileNameWithoutExtension(Path);}}
    }
}
