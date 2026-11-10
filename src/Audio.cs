using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UlanziLite {
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceEnumerator {
        void EnumAudioEndpoints(int flow, uint mask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        void RegisterEndpointNotificationCallback(IntPtr client); void UnregisterEndpointNotificationCallback(IntPtr client);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceCollection {
        void GetCount(out uint count); void Item(uint index, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDevice {
        void Activate(ref Guid iid, uint context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
        void OpenPropertyStore(int access, out IntPtr store); void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id); void GetState(out uint state);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioEndpointVolume {
        void RegisterControlChangeNotify(IntPtr callback); void UnregisterControlChangeNotify(IntPtr callback); void GetChannelCount(out uint channels);
        void SetMasterVolumeLevel(float level, ref Guid context); void SetMasterVolumeLevelScalar(float level, ref Guid context);
        void GetMasterVolumeLevel(out float level); void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint channel,float level,ref Guid context); void SetChannelVolumeLevelScalar(uint channel,float level,ref Guid context);
        void GetChannelVolumeLevel(uint channel,out float level); void GetChannelVolumeLevelScalar(uint channel,out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute,ref Guid context); void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        void GetVolumeStepInfo(out uint step,out uint count); void VolumeStepUp(ref Guid context); void VolumeStepDown(ref Guid context);
        void QueryHardwareSupport(out uint mask); void GetVolumeRange(out float min,out float max,out float step);
    }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioSessionManager2 {
        void GetAudioSessionControl(IntPtr guid,uint flags,out IntPtr control); void GetSimpleAudioVolume(IntPtr guid,uint flags,out IntPtr volume);
        void GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
        void RegisterSessionNotification(IntPtr callback); void UnregisterSessionNotification(IntPtr callback);
        void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string id,IntPtr callback); void UnregisterDuckNotification(IntPtr callback);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioSessionEnumerator {
        void GetCount(out int count); void GetSession(int index,out IAudioSessionControl2 session);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioSessionControl2 {
        void GetState(out int state); void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name,ref Guid context);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path); void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path,ref Guid context);
        void GetGroupingParam(out Guid grouping); void SetGroupingParam(ref Guid grouping,ref Guid context);
        void RegisterAudioSessionNotification(IntPtr callback); void UnregisterAudioSessionNotification(IntPtr callback);
        void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id); void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetProcessId(out uint pid); [PreserveSig] int IsSystemSoundsSession(); void SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISimpleAudioVolume {
        void SetMasterVolume(float level,ref Guid context); void GetMasterVolume(out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute,ref Guid context); void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
    public sealed class AudioState {
        public float Level; public bool Muted, Available; public string Detail;
        public override string ToString() { return Available ? Math.Round(Level*100)+"%"+(Muted?" · 음소거":"") : Detail; }
    }
    public sealed class AudioController {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        public uint ForegroundPid { get; private set; }
        public string ForegroundName { get; private set; }
        readonly uint ownPid=(uint)Process.GetCurrentProcess().Id;
        static Guid context=new Guid("45426af6-f420-469b-905c-ccf0a19cd214");
        static void Release(object obj) { if(obj!=null && Marshal.IsComObject(obj))Marshal.ReleaseComObject(obj); }
        public void TrackForeground() {
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(),out pid);
            if(pid==0 || pid==ownPid || pid==ForegroundPid)return;
            SelectProcess(pid);
        }
        internal void SelectProcess(uint pid) {
            try { using(var p=Process.GetProcessById((int)pid)) { ForegroundPid=pid; ForegroundName=p.ProcessName; } } catch(ArgumentException) { }
        }
        static float Clamp(float v) { return Math.Max(0,Math.Min(1,v)); }
        public AudioState Endpoint(bool microphone,int delta,bool toggle) {
            object enumerator=null,volumeObj=null; IMMDevice device=null;
            try {
                enumerator=new DeviceEnumerator(); var e=(IMMDeviceEnumerator)enumerator;
                e.GetDefaultAudioEndpoint(microphone?1:0,microphone?2:0,out device);
                Guid iid=typeof(IAudioEndpointVolume).GUID; device.Activate(ref iid,23,IntPtr.Zero,out volumeObj);
                var volume=(IAudioEndpointVolume)volumeObj; float level; bool muted;
                volume.GetMasterVolumeLevelScalar(out level); volume.GetMute(out muted);
                if(toggle) { muted=!muted; volume.SetMute(muted,ref context); }
                if(delta!=0) { level=Clamp(level+delta/100f); volume.SetMasterVolumeLevelScalar(level,ref context); }
                return new AudioState { Level=level,Muted=muted,Available=true,Detail=microphone?"기본 통신 마이크":"기본 출력 장치" };
            } catch(Exception ex) { return new AudioState {Detail=(microphone?"마이크":"출력 장치")+" 사용 불가: "+ex.Message}; }
            finally { Release(volumeObj); Release(device); Release(enumerator); }
        }
        bool Matches(uint pid) {
            if(ForegroundPid==0)return false;
            if(pid==ForegroundPid)return true;
            if(pid==0 || String.IsNullOrEmpty(ForegroundName))return false;
            try { using(var p=Process.GetProcessById((int)pid))return String.Equals(p.ProcessName,ForegroundName,StringComparison.OrdinalIgnoreCase); } catch { return false; }
        }
        public AudioState ApplicationVolume(int delta,bool toggle) {
            object enumerator=null; IMMDeviceCollection devices=null; var matches=new List<IAudioSessionControl2>();
            try {
                enumerator=new DeviceEnumerator(); ((IMMDeviceEnumerator)enumerator).EnumAudioEndpoints(0,1,out devices);
                uint deviceCount; devices.GetCount(out deviceCount);
                for(uint d=0;d<deviceCount;d++) {
                    IMMDevice device=null; object managerObj=null; IAudioSessionEnumerator sessions=null;
                    try {
                        devices.Item(d,out device); Guid iid=typeof(IAudioSessionManager2).GUID; device.Activate(ref iid,23,IntPtr.Zero,out managerObj);
                        ((IAudioSessionManager2)managerObj).GetSessionEnumerator(out sessions); int count; sessions.GetCount(out count);
                        for(int i=0;i<count;i++) {
                            IAudioSessionControl2 session=null;
                            try { sessions.GetSession(i,out session); uint pid; session.GetProcessId(out pid); int state; session.GetState(out state);
                                if(state!=2 && Matches(pid)) { matches.Add(session); session=null; }
                            } finally { Release(session); }
                        }
                    } finally { Release(sessions); Release(managerObj); Release(device); }
                }
                if(matches.Count==0)return new AudioState {Detail=(ForegroundName??"앱 선택 대기")+" · 오디오 세션 없음"};
                bool anyUnmuted=false; float total=0;
                foreach(var session in matches) { var v=(ISimpleAudioVolume)session; bool muted; v.GetMute(out muted); if(!muted)anyUnmuted=true; }
                foreach(var session in matches) {
                    var volume=(ISimpleAudioVolume)session; float level; volume.GetMasterVolume(out level);
                    if(delta!=0) { level=Clamp(level+delta/100f); volume.SetMasterVolume(level,ref context); }
                    if(toggle)volume.SetMute(anyUnmuted,ref context); total+=level;
                }
                return new AudioState {Available=true, Level=total/matches.Count,Muted=toggle?anyUnmuted:!anyUnmuted,Detail=ForegroundName};
            } catch(Exception ex) { return new AudioState {Detail="앱 소리 사용 불가: "+ex.Message}; }
            finally { foreach(var s in matches)Release(s); Release(devices); Release(enumerator); }
        }
        public AudioState Control(int knob,int delta,bool toggle) { TrackForeground(); return knob==1?ApplicationVolume(delta,toggle):Endpoint(knob==0,delta,toggle); }
    }
}
