using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace UlanziLite {
    // CLI credentials are read in place, never copied into configuration or logs.
    // Only quota metadata is returned to the UI; conversation content is ignored.
    sealed class LocalUsageReader {
        readonly string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        bool refreshing;DateTime nextLocal,nextNetwork;
        string geminiToken,geminiProject;
        sealed class LoginExpiredException : Exception { }
        static readonly DateTime Epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
        static Dictionary<string,object> Parse(string json) {return new JavaScriptSerializer {MaxJsonLength=2*1024*1024}.DeserializeObject(json) as Dictionary<string,object>;}
        internal static object Field(object obj,string key) {var map=obj as Dictionary<string,object>;object value;return map!=null && map.TryGetValue(key,out value)?value:null;}
        static string StringField(object obj,string key) {return Field(obj,key) as string;}
        internal static double? Number(object obj,string key) {var value=Field(obj,key);if(value==null)return null;double result;return Double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out result)?(double?)result:null;}
        static string Reset(object obj,string key) {var value=Field(obj,key);if(value is string)return (string)value;var seconds=Number(obj,key);return seconds.HasValue?Epoch.AddSeconds(seconds.Value).ToString("o"):null;}
        static string ReadSmall(string path) {if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException();using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}
        public async Task RefreshAsync() {
            if(refreshing)return;
            DateTime now=DateTime.UtcNow;if(now<nextLocal && now<nextNetwork)return;
            refreshing=true;
            try {
                var work=new List<Task>();
                if(now>=nextLocal){nextLocal=now.AddSeconds(10);work.Add(Task.Run(()=>Fetch(0,ReadCodex)));}
                if(now>=nextNetwork){nextNetwork=now.AddMinutes(2);work.Add(Task.Run(()=>Fetch(1,ReadClaude)));work.Add(Task.Run(()=>Fetch(2,ReadGemini)));}
                await Task.WhenAll(work);
            }finally {refreshing=false;}
        }
        void Fetch(int index,Func<UsageReading> read) {
            try {var value=read();if(value!=null && (value.SessionUsed.HasValue || value.WeeklyUsed.HasValue)){UsageMeters.Update(value);UsageMeters.SetStatus(index,"연결됨");}else UsageMeters.SetStatus(index,"사용량 정보 없음");}
            catch(LoginExpiredException){UsageMeters.SetStatus(index,"CLI 재로그인 필요");}
            catch(FileNotFoundException){UsageMeters.SetStatus(index,"CLI 로그인 필요");}
            catch(DirectoryNotFoundException){UsageMeters.SetStatus(index,"CLI 로그인 필요");}
            catch(WebException ex) {
                var response=ex.Response as HttpWebResponse;int code=response==null?0:(int)response.StatusCode;
                UsageMeters.SetStatus(index,code==401?"CLI 재로그인 필요":code==403?"CLI 조회 권한 없음":code==429?"조회 제한 · 재시도":code==0?"네트워크 확인":"조회 오류 · "+code);
                if(response!=null)response.Dispose();
            }
            catch {UsageMeters.SetStatus(index,"CLI 정보 확인 필요");}
        }
        internal static UsageReading ParseCodex(string line) {
            var item=Parse(line);var payload=Field(item,"payload");
            if(StringField(item,"type")!="event_msg" || StringField(payload,"type")!="token_count")return null;
            var rate=Field(payload,"rate_limits");if(rate==null)return null;
            string limit=StringField(rate,"limit_id");if(limit!=null && limit!="codex")return null;
            var first=Field(rate,"primary");var second=Field(rate,"secondary");
            // Window labels must reflect the provider's durations, not array order.
            var windows=new[]{first,second};
            var session=windows.FirstOrDefault(w=>Number(w,"window_minutes")==300);
            var week=windows.FirstOrDefault(w=>Number(w,"window_minutes")==10080);
            var value=new UsageReading {Provider="chatgpt",SessionUsed=Number(session,"used_percent"),WeeklyUsed=Number(week,"used_percent"),ObservedAt=StringField(item,"timestamp"),SessionResetAt=Reset(session,"resets_at"),WeeklyResetAt=Reset(week,"resets_at"),Source="codex-cli"};
            if(!value.SessionUsed.HasValue && !value.WeeklyUsed.HasValue)return null;value.Validate();return value;
        }
        UsageReading ReadCodex() {
            string root=Environment.GetEnvironmentVariable("CODEX_HOME");if(String.IsNullOrWhiteSpace(root))root=Path.Combine(home,".codex");
            string sessions=Path.Combine(root,"sessions");if(!Directory.Exists(sessions))throw new DirectoryNotFoundException();
            var files=new List<FileInfo>();
            for(int day=0;day<14;day++) {
                string folder=Path.Combine(sessions,DateTime.Now.AddDays(-day).ToString("yyyy/MM/dd",CultureInfo.InvariantCulture));
                if(Directory.Exists(folder))files.AddRange(new DirectoryInfo(folder).EnumerateFiles("rollout-*.jsonl"));
            }
            UsageReading newest=null;
            foreach(var file in files.OrderByDescending(f=>f.LastWriteTimeUtc).Take(12)) {
                try {
                    using(var stream=new FileStream(file.FullName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                        bool partial=stream.Length>1024*1024;if(partial)stream.Seek(-1024*1024,SeekOrigin.End);
                        using(var reader=new StreamReader(stream,Encoding.UTF8)){if(partial)reader.ReadLine();string line;
                            while((line=reader.ReadLine())!=null) {
                                if(!line.Contains("\"token_count\"") || !line.Contains("\"rate_limits\""))continue;
                                try {var value=ParseCodex(line);if(value!=null && (newest==null || DateTimeOffset.Parse(value.ObservedAt)>DateTimeOffset.Parse(newest.ObservedAt)))newest=value;}catch(ArgumentException){ }
                            }
                        }
                    }
                }catch(IOException){ }catch(UnauthorizedAccessException){ }
            }
            return newest;
        }
        internal static UsageReading ParseClaude(string json,DateTime now) {
            var item=Parse(json);var first=Field(item,"five_hour");var second=Field(item,"seven_day");
            var reading=new UsageReading {Provider="claude",SessionUsed=Number(first,"utilization"),WeeklyUsed=Number(second,"utilization"),SessionResetAt=Reset(first,"resets_at"),WeeklyResetAt=Reset(second,"resets_at"),ObservedAt=now.ToString("o"),Source="claude-oauth"};reading.Validate();return reading;
        }
        UsageReading ReadClaude() {
            string root=Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");if(String.IsNullOrWhiteSpace(root))root=Path.Combine(home,".claude");
            var credentials=Field(Parse(ReadSmall(Path.Combine(root,".credentials.json"))),"claudeAiOauth");string token=StringField(credentials,"accessToken");
            if(String.IsNullOrEmpty(token))throw new FileNotFoundException();
            string json=Request("https://api.anthropic.com/api/oauth/usage",token,null,false,true);
            return ParseClaude(json,DateTime.UtcNow);
        }
        internal static UsageReading ParseGemini(string json,DateTime now) {
            var item=Parse(json);var buckets=Field(item,"buckets") as IEnumerable;
            double? pro=null,flash=null;string proReset=null,flashReset=null;
            if(buckets!=null)foreach(var bucket in buckets) {
                string model=StringField(bucket,"modelId")??"";double? remaining=Number(bucket,"remainingFraction");
                if(!remaining.HasValue || remaining<0 || remaining>1)continue;
                double used=100*(1-remaining.Value);
                if(model.IndexOf("pro",StringComparison.OrdinalIgnoreCase)>=0 && (!pro.HasValue || used>pro)){pro=used;proReset=StringField(bucket,"resetTime");}
                if(model.IndexOf("flash",StringComparison.OrdinalIgnoreCase)>=0 && (!flash.HasValue || used>flash)){flash=used;flashReset=StringField(bucket,"resetTime");}
            }
            var value=new UsageReading {Provider="gemini",SessionUsed=pro,WeeklyUsed=flash,SessionResetAt=proReset,WeeklyResetAt=flashReset,ObservedAt=now.ToString("o"),Source="gemini-cli"};value.Validate();return value;
        }
        UsageReading ReadGemini() {
            var credentials=Parse(ReadSmall(Path.Combine(home,".gemini","oauth_creds.json")));
            double? expiry=Number(credentials,"expiry_date");
            if(expiry.HasValue && Epoch.AddMilliseconds(expiry.Value)<=DateTime.UtcNow.AddMinutes(1))throw new LoginExpiredException();
            string currentToken=StringField(credentials,"access_token");
            if(geminiToken!=currentToken){geminiToken=currentToken;geminiProject=null;}
            if(String.IsNullOrEmpty(geminiToken))throw new FileNotFoundException();
            if(geminiProject==null) {
                var response=Parse(Request("https://cloudcode-pa.googleapis.com/v1internal:loadCodeAssist",geminiToken,"{\"metadata\":{\"ideType\":\"GEMINI_CLI\",\"platform\":\"WINDOWS_AMD64\",\"pluginType\":\"GEMINI\"}}",false,false));
                geminiProject=StringField(response,"cloudaicompanionProject");
                if(geminiProject==null)geminiProject=StringField(Field(response,"cloudaicompanionProject"),"id")??"";
            }
            string body=new JavaScriptSerializer().Serialize(String.IsNullOrEmpty(geminiProject)?(object)new {}:new {project=geminiProject});
            return ParseGemini(Request("https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota",geminiToken,body,false,false),DateTime.UtcNow);
        }
        static string Request(string url,string token,string body,bool form,bool claude) {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url);request.Timeout=10000;request.ReadWriteTimeout=10000;request.AllowAutoRedirect=false;request.UserAgent="UlanziLite/0.3.0";
            if(token!=null)request.Headers[HttpRequestHeader.Authorization]="Bearer "+token;
            if(claude)request.Headers["anthropic-beta"]="oauth-2025-04-20";
            if(body!=null){request.Method="POST";request.ContentType=form?"application/x-www-form-urlencoded":"application/json";byte[] bytes=Encoding.UTF8.GetBytes(body);request.ContentLength=bytes.Length;using(var output=request.GetRequestStream())output.Write(bytes,0,bytes.Length);}
            using(var response=(HttpWebResponse)request.GetResponse())using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream)) {
                var text=new StringBuilder();var buffer=new char[4096];int count;
                while((count=reader.Read(buffer,0,buffer.Length))>0){text.Append(buffer,0,count);if(text.Length>1024*1024)throw new InvalidDataException();}return text.ToString();
            }
        }
        public static int Diagnose() {
            new LocalUsageReader().RefreshAsync().GetAwaiter().GetResult();
            var report=new StringBuilder();
            for(int i=0;i<3;i++){var value=UsageMeters.Get(i);report.AppendLine(UsageMeters.Names[i]+": "+UsageMeters.Status(i));if(value!=null)report.AppendLine(new JavaScriptSerializer().Serialize(value));}
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"usage-test-results.txt"),report.ToString(),Encoding.UTF8);return 0;
        }
    }
}
