using System.Globalization;
using System.Text.RegularExpressions;
namespace LicenseLogView;
public static partial class Analyzer {
 [GeneratedRegex(@"^\s*(\d{1,2}:\d{2}:\d{2})\s+\(([^)]+)\)\s+(.*)$")] private static partial Regex LinePattern();
 [GeneratedRegex(@"^TIMESTAMP\s+(\d{1,2}/\d{1,2}/\d{4})")] private static partial Regex DatePattern();
 [GeneratedRegex("^(OUT|IN|DENIED):\\s+\"([^\"]+)\"\\s+(\\S+)@(\\S+)(.*)$")] private static partial Regex EventPattern();
 [GeneratedRegex(@"\[session=([^\]]+)\]")] private static partial Regex SessionPattern();
 [GeneratedRegex(@"\((\d+) licenses?\)")] private static partial Regex CountPattern();
 public static List<Event> Parse(IEnumerable<(string Name,string Text)> files,Action<string> warn) {
  var events=new List<Event>();var hashes=new HashSet<string>();
  foreach(var (name,text) in files.OrderBy(f=>f.Name,StringComparer.Ordinal)) {
   var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
   if(!hashes.Add(hash)){warn($"{name}: 同じ内容のファイルを重複除外しました。");continue;}
   DateTime? date=null; TimeSpan? lastClock=null;var n=0;
   foreach(var line in text.Split('\n')) {
    n++; var m=LinePattern().Match(line.TrimEnd('\r')); if(!m.Success){if(Regex.IsMatch(line,@"\b(OUT|IN|DENIED):")) warn($"{name}:{n}: 未対応の行形式。");continue;}
    if(!TimeSpan.TryParse(m.Groups[1].Value,CultureInfo.InvariantCulture,out var clock) || clock.TotalDays>=1) {warn($"{name}:{n}: 時刻が不正。");continue;}
    var body=m.Groups[3].Value;var dm=DatePattern().Match(body);
    if(dm.Success){if(DateTime.TryParseExact(dm.Groups[1].Value,"M/d/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var dd))date=dd;else warn($"{name}:{n}: 日付が不正。");lastClock=clock;continue;}
    if(date!=null && lastClock!=null && clock<lastClock) date=date.Value.AddDays(1);
    lastClock=clock;
    if(!Regex.IsMatch(body,@"^(OUT|IN|DENIED)\s*:")) {if(Regex.IsMatch(body,@"REStarted|Started vendor|Shutting down|SHUTDOWN",RegexOptions.IgnoreCase))warn($"{name}:{n}: 再起動/停止検出。セッションを自動解除しません。実ログで確認してください。");continue;}
    var em=EventPattern().Match(body);
    if(!em.Success || body.Contains("^^^")){warn($"{name}:{n}: 未対応イベント形式（verbose等）。");continue;}
    if(date==null){warn($"{name}:{n}: TIMESTAMPがなく日付を特定できません。");continue;}
    var cm=CountPattern().Match(em.Groups[5].Value);var count=cm.Success && int.TryParse(cm.Groups[1].Value,out var v)?v:1;
    if(count<=0){warn($"{name}:{n}: 数量が不正。");continue;}
    events.Add(new(date.Value+clock,em.Groups[1].Value,em.Groups[2].Value,em.Groups[3].Value,em.Groups[4].Value,count,SessionPattern().Match(body).Groups[1].Value,body.Contains("(-4,"),name,n));
   }
  }
  return events.OrderBy(e=>e.At).ToList(); // Preserve input order for equal timestamps.
 }
 class Live(Event e,int count) {public Event E=e;public int Count=count;}
 public static Report Analyze(Config c,IEnumerable<(string Name,string Text)> files) {
  var report=new Report{Config=c}; void Warn(string x){report.WarningCount++;if(report.Warnings.Count<100) report.Warnings.Add(x);}
  var inputs=files.ToArray();
  var events=Parse(inputs,Warn);
  var dates=inputs.SelectMany(f=>Regex.Matches(f.Text,@"TIMESTAMP\s+(\d{1,2}/\d{1,2}/\d{4})").Cast<Match>()).Select(m=>DateTime.TryParseExact(m.Groups[1].Value,"M/d/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?(DateTime?)d:null).Where(d=>d!=null).Select(d=>d!.Value).Concat(events.Select(e=>e.At.Date)).ToArray();
  if(dates.Length>0 && (dates.Min()>c.Start || dates.Max()<c.EndExclusive.AddDays(-1)))Warn("入力ログの確認できる日付範囲が集計期間を覆っていません。欠けた期間を利用なしと見なすと平均が低くなるため、期間設定またはログを確認してください。");var active=new List<Live>();var sessions=report.Sessions;var denials=new List<Event>();int valid=0;
  var capacityErrors=0;
  foreach(var e in events) {
   if(e.At>=c.EndExclusive) continue;
   if(!c.Features.TryGetValue(e.Feature,out var f)){Warn($"{e.Source}:{e.Line}: 未設定の機能 {e.Feature} を集計対象から除外。");continue;}
   valid++;
   if(e.Action=="DENIED"){if(e.At>=c.Start)denials.Add(e);continue;}
   if(e.Action=="OUT") {
    if(e.Session!="" && active.Any(a=>a.E.Session==e.Session)){Warn($"{e.Source}:{e.Line}: 重複セッションID。除外しました。");continue;}
    active.Add(new(e,e.Count));
    var used=active.Where(a=>c.Features[a.E.Feature].Pool==f.Pool).Sum(a=>c.Features[a.E.Feature].Units*a.Count);
    if(used>c.Capacity(f.Pool)){capacityErrors++;Warn($"{e.Source}:{e.Line}: {f.Pool}容量超過 {used}/{c.Capacity(f.Pool)}。");}
   } else {
    var remaining=e.Count;
    foreach(var a in active.Where(a=>a.E.Feature==e.Feature && a.E.User==e.User && a.E.Host==e.Host && (e.Session=="" || e.Session==a.E.Session)).ToArray()) {
     var n=Math.Min(remaining,a.Count); Add(a,e.At,n,false);remaining-=n;a.Count-=n;if(a.Count==0)active.Remove(a);if(remaining==0)break;
    }
    if(remaining>0)Warn($"{e.Source}:{e.Line}: 対応するOUTのないIN ({remaining}件)。期間開始前の取得・ログ欠落を確認。");
   }
  }
  foreach(var a in active){Add(a,c.EndExclusive,a.Count,true);Warn($"未返却: {a.E.Feature} {a.E.User}@{a.E.Host}。期間末まで占有と仮定。");}
  void Add(Live a,DateTime end,int n,bool censored){var f=c.Features[a.E.Feature];var start=a.E.At<c.Start?c.Start:a.E.At;if(end>start)sessions.Add(new(start,end,a.E.Feature,f.Product,f.Pool,a.E.User,a.E.Host,n*f.Units,censored));}
  // Exact intervals, not event-count averages. Split intervals at 5-minute boundaries.
  var buckets=Enumerable.Range(0,(int)(c.EndExclusive-c.Start).TotalMinutes/5).Select(i=>new Bucket{At=c.Start.AddMinutes(i*5)}).ToArray();
  var changes=new SortedDictionary<DateTime,List<(Session S,int Sign)>>();
  foreach(var s in sessions){Change(s.Start,s,1);Change(s.End,s,-1);}
  void Change(DateTime t,Session s,int sign){if(!changes.TryGetValue(t,out var list))changes[t]=list=[];list.Add((s,sign));}
  int smd=0,vbl=0,se=0,nx=0;var previous=c.Start;
  foreach(var (at,list) in changes) {
   var t=previous;
   while(t<at){var idx=(int)((t-c.Start).TotalMinutes/5);if(idx<0 || idx>=buckets.Length)break;var b=buckets[idx];var until=new[]{at,b.At.AddMinutes(5)}.Min();var duration=(until-t).TotalMinutes;
    b.Smd3Minutes+=smd*duration;b.VblMinutes+=vbl*duration;b.SeMinutes+=se*duration;b.NxMinutes+=nx*duration;b.Smd3Peak=Math.Max(b.Smd3Peak,smd);b.VblPeak=Math.Max(b.VblPeak,vbl);if(smd>=c.Capacity("SMD3"))b.FullMinutes+=duration;t=until;}
   foreach(var (s,sign) in list){if(s.Pool=="SMD3"){smd+=sign*s.Units;if(s.Product=="Solid Edge")se+=sign*s.Units;else if(s.Product=="NX")nx+=sign*s.Units/3;}else vbl+=sign*s.Units;} previous=at;
  }
  foreach(var e in denials){var i=(int)((e.At-c.Start).TotalMinutes/5);if(i>=0 && i<buckets.Length){if(c.Features[e.Feature].Pool=="SMD3")buckets[i].Denied++;else buckets[i].VblDenied++;}}
  for(var day=c.Start;day<c.EndExclusive;day=day.AddDays(1)){
   var all=buckets.Where(b=>b.At.Date==day).ToArray();var work=c.Working(day)?all.Where(b=>b.At.Hour>=c.BusinessStartHour && b.At.Hour<c.BusinessEndHour).ToArray():[];
   var mins=work.Length*5.0;
   report.Daily.Add(new(day.ToString("yyyy-MM-dd"),c.Working(day),mins/60,mins==0?0:work.Sum(b=>b.Smd3Minutes)/(mins*c.Capacity("SMD3")),mins==0?0:work.Sum(b=>b.VblMinutes)/(mins*c.VblCapacity),all.Sum(b=>b.Smd3Minutes)/(1440*c.Capacity("SMD3")),all.Sum(b=>b.VblMinutes)/(1440*c.VblCapacity),all.Sum(b=>b.SeMinutes)/60,all.Sum(b=>b.NxMinutes)/60,all.Max(b=>b.Smd3Peak)/3.0,all.Max(b=>b.VblPeak),work.Sum(b=>b.FullMinutes),all.Sum(b=>b.Denied),all.Sum(b=>b.VblDenied)));
  }
  var business=report.Daily.Sum(d=>d.BusinessHours);
  var smHours=report.Daily.Sum(d=>d.Smd3Utilization*d.BusinessHours*c.Smd3Capacity);
  var vbHours=report.Daily.Sum(d=>d.VblUtilization*d.BusinessHours*c.VblCapacity);
  report.Summary=new{businessHours=business,smd3Utilization=business==0?0:smHours/(business*c.Smd3Capacity),vblUtilization=business==0?0:vbHours/(business*c.VblCapacity),smd3CalendarUtilization=report.Daily.Average(d=>d.Smd3CalendarUtilization),vblCalendarUtilization=report.Daily.Average(d=>d.VblCalendarUtilization),smd3Peak=report.Daily.Max(d=>d.Smd3Peak),vblPeak=report.Daily.Max(d=>d.VblPeak),fullMinutes=report.Daily.Sum(d=>d.FullMinutes),denied=denials.Count(e=>c.Features[e.Feature].Pool=="SMD3"),vblDenied=denials.Count(e=>c.Features[e.Feature].Pool=="VBL"),exhaustionDenials=denials.Count(e=>e.Exhaustion),users=sessions.Select(s=>s.User).Distinct().Count(),sessions=sessions.Count,events=valid,capacityErrors,warningCount=report.WarningCount};
  report.Samples=buckets.Where(b=>c.Working(b.At) && b.At.Hour>=c.BusinessStartHour && b.At.Hour<c.BusinessEndHour).Select(b=>(object)new{at=b.At.ToString("yyyy-MM-ddTHH:mm:ss"),smd3=b.Smd3Minutes/15,vbl=b.VblMinutes/5,se=b.SeMinutes/5,nx=b.NxMinutes/5,smd3Peak=b.Smd3Peak/3.0,b.VblPeak,b.FullMinutes,b.Denied,b.VblDenied}).ToList();
  report.Monthly=report.Daily.GroupBy(d=>d.Date[..7]).Select(g=>(object)new{month=g.Key,businessHours=g.Sum(d=>d.BusinessHours),smd3Utilization=g.Sum(d=>d.Smd3Utilization*d.BusinessHours)/Math.Max(1,g.Sum(d=>d.BusinessHours)),vblUtilization=g.Sum(d=>d.VblUtilization*d.BusinessHours)/Math.Max(1,g.Sum(d=>d.BusinessHours)),smd3Peak=g.Max(d=>d.Smd3Peak),vblPeak=g.Max(d=>d.VblPeak),denied=g.Sum(d=>d.Denied),vblDenied=g.Sum(d=>d.VblDenied),fullMinutes=g.Sum(d=>d.FullMinutes)}).ToList();
  report.Users=sessions.GroupBy(s=>s.User).Select(g=>(object)new{user=g.Key,seHours=g.Where(s=>s.Product=="Solid Edge").Sum(s=>(s.End-s.Start).TotalHours*s.Units),nxHours=g.Where(s=>s.Product=="NX").Sum(s=>(s.End-s.Start).TotalHours*s.Units/3),vblTokenHours=g.Where(s=>s.Pool=="VBL").Sum(s=>(s.End-s.Start).TotalHours*s.Units),sessions=g.Count(),denied=denials.Count(e=>e.User==g.Key)}).ToList();
  report.Options=sessions.Where(s=>s.Pool=="VBL").GroupBy(s=>s.Feature).Select(g=>(object)new{feature=g.Key,product=c.Features[g.Key].Product,tokens=c.Features[g.Key].Units,tokenHours=g.Sum(s=>(s.End-s.Start).TotalHours*s.Units),optionHours=g.Sum(s=>(s.End-s.Start).TotalHours*s.Units/c.Features[g.Key].Units),sessions=g.Count(),denied=denials.Count(e=>e.Feature==g.Key)}).ToList();
  report.Denials=denials.Select(e=>(object)new{at=e.At.ToString("yyyy-MM-ddTHH:mm:ss"),e.User,feature=e.Feature,product=c.Features[e.Feature].Product,pool=c.Features[e.Feature].Pool,exhaustion=e.Exhaustion}).ToList();
  if(business==0)Warn("期間に営業時間がありません。");
  if(events.Count==0)Warn("読み取り可能なイベントがありません。");
  return report;
 }
 public static void Export(Report r,string dir){Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"report.json"),Json.Dump(r));
  WriteCsv("daily.csv",r.Daily);WriteCsv("monthly.csv",r.Monthly);WriteCsv("sessions.csv",r.Sessions);WriteCsv("users.csv",r.Users);WriteCsv("options.csv",r.Options);WriteCsv("denials.csv",r.Denials);
  void WriteCsv(string name,object value){File.WriteAllText(Path.Combine(dir,name),Csv(value),new System.Text.UTF8Encoding(true));}
 }
 public static string Csv(object rows) {var array=System.Text.Json.JsonSerializer.SerializeToElement(rows,Json.Options);if(array.GetArrayLength()==0)return "";var keys=array[0].EnumerateObject().Select(x=>x.Name).ToArray();string Q(string x)=>"\""+x.Replace("\"","\"\"")+"\"";var lines=new List<string>{string.Join(',',keys.Select(Q))};foreach(var row in array.EnumerateArray())lines.Add(string.Join(',',keys.Select(k=>Q(row.GetProperty(k).ToString()))));return string.Join("\r\n",lines)+"\r\n";}
}
