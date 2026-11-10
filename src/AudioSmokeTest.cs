using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading;

namespace UlanziLite {
    static class AudioSmokeTest {
        public static int Run() {
            string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"audio-test-results.txt");
            try {
                var audio=new AudioController();
                if(audio.ApplicationVolume(0,false).Available)throw new Exception("No foreground PID must not select system sounds");
                var microphone=audio.Endpoint(true,0,false);var output=audio.Endpoint(false,0,false);
                if(!microphone.Available || !output.Available)throw new Exception("Endpoint access: "+microphone.Detail+" / "+output.Detail);
                // A silent PCM loop creates a real session belonging only to this test process.
                using(var wav=new MemoryStream()) {
                    using(var writer=new BinaryWriter(wav,System.Text.Encoding.ASCII,true)) {
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(16036);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                        writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(16000);writer.Write(new byte[16000]);
                    }wav.Position=0;
                    using(var player=new SoundPlayer(wav)) {
                        player.PlayLooping();Thread.Sleep(500);audio.SelectProcess((uint)Process.GetCurrentProcess().Id);
                        var initial=audio.ApplicationVolume(0,false);if(!initial.Available)throw new Exception("Silent test session unavailable: "+initial.Detail);
                        try {
                            audio.ApplicationVolume(-13,false);var lowered=audio.ApplicationVolume(0,false);
                            if(Math.Abs(lowered.Level-Math.Max(0,initial.Level-.13f))>.001)throw new Exception("Session volume did not change");
                            audio.ApplicationVolume(0,true);var muted=audio.ApplicationVolume(0,false);if(muted.Muted==initial.Muted)throw new Exception("Session mute did not toggle");
                        } finally {
                            var now=audio.ApplicationVolume(0,false);if(now.Muted!=initial.Muted)audio.ApplicationVolume(0,true);
                            audio.ApplicationVolume((int)Math.Round((initial.Level-now.Level)*100),false);player.Stop();
                        }
                    }
                }
                File.WriteAllText(report,"PASS: default microphone and output read; no foreground does not control system sounds; real silent application session volume write/read and mute roundtrip.\n");return 0;
            }catch(Exception ex){File.WriteAllText(report,"FAIL: "+ex);return 1;}
        }
    }
}
