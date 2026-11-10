using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace UlanziLite {
    static class SelfTest {
        static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
        public static int Run() {
            string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt");
            try {
                Check(RuntimeContext.IsIsolatedDesktop("CodexSandboxDesktop-test"),"reject isolated controller runtime");
                Check(!RuntimeContext.IsIsolatedDesktop("Default"),"allow normal desktop runtime");
                byte[] packet=Protocol.Packet(0x0102,new byte[]{1,17,2,3},4);int index,state;
                Check(Protocol.Parse(packet,1024,out index,out state) && index==17 && state==3,"dial right");
                byte[] withId=new byte[1025];Buffer.BlockCopy(packet,0,withId,1,1024);
                Check(Protocol.Parse(withId,1025,out index,out state) && index==17 && state==3,"Windows report ID");
                Check(!Protocol.Parse(new byte[12],12,out index,out state),"reject bad header");
                Check(!Protocol.Parse(packet,10,out index,out state),"reject short input");
                packet[4]=255;Check(!Protocol.Parse(packet,12,out index,out state),"reject invalid payload size");
                bool rejected=false;try{new KeyBinding {Target="javascript:alert(1)"}.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"reject non-web scheme");
                var config=Configuration.Defaults(); Check(config.Pages.Count==3 && config.Pages.All(p=>p.Keys.Count==13),"page layout");
                config.Pages[0].Keys[0]=new KeyBinding {Label="한글 테스트",Target="https://example.com"};
                byte[] bundle=Artwork.Bundle(config.Pages[0]);
                for(int p=1016;p<bundle.Length;p+=1024)Check(bundle[p]!=0 && bundle[p]!=0x7c,"firmware packet boundary");
                using(var zip=new ZipArchive(new MemoryStream(bundle),ZipArchiveMode.Read)) {
                    var manifest=zip.GetEntry("manifest.json");Check(manifest!=null,"manifest exists");
                    string json;using(var reader=new StreamReader(manifest.Open(),Encoding.UTF8))json=reader.ReadToEnd();
                    var parsed=new JavaScriptSerializer().DeserializeObject(json) as System.Collections.Generic.Dictionary<string,object>;
                    Check(parsed.Count==14 && parsed.ContainsKey("3_2") && !parsed.ContainsKey("4_2"),"13 keys and wide screen");
                    foreach(var entry in zip.Entries.Where(e=>e.Name.EndsWith(".png")))using(var img=System.Drawing.Image.FromStream(entry.Open()))Check(img.Width==196 && img.Height==196,"native image size");
                }
                var clone=Configuration.Json.Deserialize<Configuration>(Configuration.Json.Serialize(config));Check(clone.Pages[0].Keys[0].Label=="한글 테스트","Unicode config roundtrip");
                var display=new WideScreenState();Check(!display.NeedsUpdate(0),"idle clock sends no extra packets");
                display.Touch(1000,2);int firstVersion=display.Version;Check(display.WantsAudio(3999) && display.NeedsUpdate(1000),"show volume for three seconds");
                display.MarkSent(true,firstVersion);Check(!display.NeedsUpdate(2000),"unchanged volume avoids duplicate upload");
                display.Touch(3900,0);Check(display.WantsAudio(6899) && display.ActiveKnob==0,"last input extends overlay and changes highlight");
                display.MarkSent(true,firstVersion);Check(display.NeedsUpdate(4000),"input during transfer must not be lost");
                display.MarkSent(true,display.Version);Check(!display.NeedsUpdate(5000) && display.NeedsUpdate(6900),"restore clock after last input");
                display.MarkSent(false,display.Version);Check(!display.NeedsUpdate(7000),"clock restores only once");
                display.Touch(8000,1);display.MarkSent(true,display.Version);display.Reset();Check(display.NeedsUpdate(8100),"reapply overlay after page sync");
                display=new WideScreenState {HoldMilliseconds=5000};display.Touch(1000,2);display.MarkSent(true,display.Version);
                Check(display.WantsAudio(5999) && !display.WantsAudio(6000) && display.NeedsUpdate(6000,100),"configured timeout restores clock");
                display.MarkSent(false,display.Version,100);Check(!display.NeedsUpdate(6100,100) && display.NeedsUpdate(7000,101),"clock images refresh once per second after overlay");
                display.MarkSent(false,display.Version,101);display.Touch(7100,1);Check(display.NeedsUpdate(7100,101),"new knob input interrupts ticking clock");
                byte[] clockPng;
                using(var clock=WideArtwork.Clock(new DateTime(2026,11,10,22,0,0))){clock.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"clock-preview.png"));clockPng=WideArtwork.Encode(clock);}
                using(var clock=WideArtwork.Clock(new DateTime(2026,11,10,22,0,1)))Check(!clockPng.SequenceEqual(WideArtwork.Encode(clock)),"clock image seconds advance");
                byte[] widePng;
                using(var wide=WideArtwork.Audio(new[]{new AudioState {Available=true,Level=.65f},new AudioState {Available=true,Level=.42f,Muted=true},new AudioState {Available=true,Level=.68f}},2,"Chrome")) {
                    Check(wide.Width==392 && wide.Height==196,"wide display aspect ratio");wide.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"audio-preview.png"));widePng=WideArtwork.Encode(wide);
                }
                using(var native=System.Drawing.Image.FromStream(new MemoryStream(widePng)))Check(native.Width==196 && native.Height==196,"wide image squeezed for firmware");
                var partial=Artwork.WideBundle(widePng);
                for(int p=1016;p<partial.Length;p+=1024)Check(partial[p]!=0 && partial[p]!=0x7c,"partial packet boundaries");
                using(var zip=new ZipArchive(new MemoryStream(partial),ZipArchiveMode.Read))using(var reader=new StreamReader(zip.GetEntry("manifest.json").Open())) {
                    var entries=new JavaScriptSerializer().DeserializeObject(reader.ReadToEnd()) as System.Collections.Generic.Dictionary<string,object>;
                    Check(entries.Count==1 && entries.ContainsKey("3_2"),"partial upload must leave all other keys untouched");
                }
                var partialPacket=Protocol.Packet(13,partial.Take(1016).ToArray(),partial.Length);Check(partialPacket[2]==0 && partialPacket[3]==13 && BitConverter.ToInt32(partialPacket,4)==partial.Length,"partial command framing");
                File.WriteAllText(report,"PASS: HID framing, input validation, URL validation, page layout, ZIP manifest, packet boundaries, PNG dimensions, Korean config roundtrip; configurable volume timeout, repeated input, transfer races, page sync reset, wide-only partial bundle, aspect ratio, partial HID command; clock image restore, once-per-second clock updates, clock interrupted by new input, advancing clock seconds.\n");return 0;
            }catch(Exception ex){File.WriteAllText(report,"FAIL: "+ex);return 1;}
        }
    }
}
