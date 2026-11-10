using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace UlanziLite {
    public sealed class HidInfo {
        public string Path;
        public int InputLength, OutputLength;
        public override string ToString() { return Path + " input=" + InputLength + " output=" + OutputLength; }
    }
    public static class HidNative {
        [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int Size; public Guid ClassGuid; public int Flags; public IntPtr Reserved; }
        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr info, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr info, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr device);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr info);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle file, out IntPtr data);
        [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, IntPtr caps);
        [DllImport("hid.dll")] public static extern bool HidD_SetNumInputBuffers(SafeFileHandle file, uint count);
        public static List<HidInfo> Enumerate() {
            var result = new List<HidInfo>(); Guid guid; HidD_GetHidGuid(out guid);
            IntPtr info = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 18);
            if (info == new IntPtr(-1)) return result;
            try {
                for (uint i=0;;i++) {
                    var data = new InterfaceData(); data.Size=Marshal.SizeOf(data);
                    if (!SetupDiEnumDeviceInterfaces(info, IntPtr.Zero, ref guid, i, ref data)) break;
                    uint size; SetupDiGetDeviceInterfaceDetail(info, ref data, IntPtr.Zero, 0, out size, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal((int)size);
                    try {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(info, ref data, detail, size, out size, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        if (!path.ToLowerInvariant().Contains("vid_2207&pid_0019")) continue;
                        using (var file = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero)) {
                            IntPtr pre;
                            if (file.IsInvalid || !HidD_GetPreparsedData(file, out pre)) continue;
                            IntPtr caps = Marshal.AllocHGlobal(128);
                            try {
                                if (HidP_GetCaps(pre, caps) < 0) continue;
                                result.Add(new HidInfo { Path=path, InputLength=(ushort)Marshal.ReadInt16(caps,4), OutputLength=(ushort)Marshal.ReadInt16(caps,6) });
                            } finally { Marshal.FreeHGlobal(caps); HidD_FreePreparsedData(pre); }
                        }
                    } finally { Marshal.FreeHGlobal(detail); }
                }
            } finally { SetupDiDestroyDeviceInfoList(info); }
            return result;
        }
    }
    public static class Protocol {
        public static byte[] Packet(int command, byte[] body, int length) {
            if (body.Length > 1016) throw new ArgumentException("Payload too large");
            byte[] p = new byte[1024]; p[0]=p[1]=0x7c; p[2]=(byte)(command>>8); p[3]=(byte)command;
            Buffer.BlockCopy(BitConverter.GetBytes(length),0,p,4,4); Buffer.BlockCopy(body,0,p,8,body.Length); return p;
        }
        public static bool Parse(byte[] p, int count, out int index, out int state) {
            index=state=-1; int start = count>=13 && p[0]==0 ? 1 : 0;
            if (count-start<12 || p[start]!=0x7c || p[start+1]!=0x7c || p[start+2]!=1 || (p[start+3]!=1 && p[start+3]!=2)) return false;
            uint length = BitConverter.ToUInt32(p,start+4); if (length<4 || length>count-start-8) return false;
            index=p[start+9]; state=p[start+11]; return true;
        }
    }
    public sealed class Deck : IDisposable {
        public event Action<bool,string> Status;
        public event Action<int,int> Input;
        public event Action Connected;
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly SemaphoreSlim writer = new SemaphoreSlim(1,1);
        FileStream stream; HidInfo info;
        public bool IsConnected { get { return stream != null; } }
        public void Start() { Task.Run((Func<Task>)Run); }
        void Report(bool ok, string text) { var h=Status; if(h!=null) h(ok,text); }
        async Task Run() {
            while (!stop.IsCancellationRequested) {
                FileStream current=null;
                try {
                    foreach (var candidate in HidNative.Enumerate()) {
                        if(candidate.InputLength<1024 || candidate.OutputLength<1024) continue;
                        var handle=HidNative.CreateFile(candidate.Path,0xC0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);
                        if(handle.IsInvalid) { handle.Dispose(); continue; }
                        HidNative.HidD_SetNumInputBuffers(handle,64);
                        current=new FileStream(handle,FileAccess.ReadWrite,candidate.InputLength,true); info=candidate; break;
                    }
                    if(current==null) throw new IOException("D200X 연결 대기 · Studio가 실행 중이면 종료해 주세요");
                    stream=current; Report(true,"D200X 연결됨"); var c=Connected; if(c!=null)c();
                    byte[] buf=new byte[info.InputLength];
                    while(!stop.IsCancellationRequested) {
                        int count=await current.ReadAsync(buf,0,buf.Length,stop.Token).ConfigureAwait(false);
                        if(count==0)throw new IOException("USB 연결이 끊겼습니다");
                        int index,state; if(Protocol.Parse(buf,count,out index,out state)) { var h=Input; if(h!=null)h(index,state); }
                    }
                } catch(Exception ex) { if(!stop.IsCancellationRequested) Report(false,ex.Message); }
                finally { if(stream==current)stream=null; if(current!=null)current.Dispose(); }
                try { await Task.Delay(2000,stop.Token).ConfigureAwait(false); } catch(OperationCanceledException) { break; }
            }
        }
        async Task Write(FileStream target, byte[] packet) {
            byte[] report=new byte[info.OutputLength]; int offset=report.Length>1024 ? 1 : 0;
            Buffer.BlockCopy(packet,0,report,offset,1024);
            var pending=target.WriteAsync(report,0,report.Length,stop.Token);
            if(await Task.WhenAny(pending,Task.Delay(3000)).ConfigureAwait(false)!=pending) { target.Dispose(); throw new IOException("USB 쓰기 시간 초과"); }
            await pending.ConfigureAwait(false);
        }
        public async Task Send(int command, byte[] payload, bool bundle) {
            await writer.WaitAsync().ConfigureAwait(false);
            FileStream target=stream;
            try {
                if(target==null)throw new IOException("D200X가 연결되어 있지 않습니다");
                byte[] first=new byte[Math.Min(1016,payload.Length)]; Buffer.BlockCopy(payload,0,first,0,first.Length);
                await Write(target,Protocol.Packet(command,first,payload.Length)).ConfigureAwait(false);
                if(bundle)for(int pos=1016;pos<payload.Length;pos+=1024) {
                    byte[] chunk=new byte[1024]; Buffer.BlockCopy(payload,pos,chunk,0,Math.Min(1024,payload.Length-pos)); await Write(target,chunk).ConfigureAwait(false);
                }
            } catch { if(target!=null)target.Dispose(); throw; } finally { writer.Release(); }
        }
        public void Dispose() { stop.Cancel(); var s=stream; if(s!=null)s.Dispose(); }
    }
}
