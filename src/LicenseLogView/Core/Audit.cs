namespace LicenseLogView;
public static class Audit {
 public static object Run(Config c,string dir) {
  var truth=System.Text.Json.JsonSerializer.Deserialize<List<Event>>(File.ReadAllText(Path.Combine(dir,"truth-events.json")),Json.Options)??throw new Exception("正解データがありません。");
  var report=Analyzer.Analyze(c,Directory.GetFiles(dir,"*.log").Select(p=>(Path.GetFileName(p),File.ReadAllText(p))));
  var parsed=Analyzer.Parse(Directory.GetFiles(dir,"*.log").Select(p=>(Path.GetFileName(p),File.ReadAllText(p))),x=>throw new Exception(x));
  void Check(bool ok,string message){if(!ok)throw new Exception("Audit failed: "+message);}
  Check(parsed.Count==truth.Count,"イベント件数");
  for(var i=0;i<truth.Count;i++){var a=truth[i];var b=parsed[i];Check(a.At==b.At && a.Action==b.Action && a.Feature==b.Feature && a.User==b.User && a.Host==b.Host && a.Session==b.Session && a.Exhaustion==b.Exhaustion,"ログと正解イベント "+i);}
  var live=new Dictionary<string,Event>();var used=new Dictionary<string,int>{{"SMD3",0},{"VBL",0}};var peaks=new Dictionary<string,int>{{"SMD3",0},{"VBL",0}};var hours=new Dictionary<string,double>();
  foreach(var e in truth){var f=c.Features[e.Feature];var cost=f.Units*e.Count;
   if(e.Action=="OUT"){
    Check(!live.ContainsKey(e.Session),"セッションID重複");Check(used[f.Pool]+cost<=c.Capacity(f.Pool),"容量超過");
    if(f.Pool=="SMD3")Check(!live.Values.Any(x=>x.User==e.User && c.Features[x.Feature].Pool=="SMD3"),"利用者の二重起動");
    else Check(live.Values.Any(x=>x.User==e.User && c.Features[x.Feature].Product=="Solid Edge"),"Solid Edgeなしのオプション取得");
    live.Add(e.Session,e);used[f.Pool]+=cost;peaks[f.Pool]=Math.Max(peaks[f.Pool],used[f.Pool]);
   }else if(e.Action=="IN"){
    Check(live.TryGetValue(e.Session,out var start),"取得なしの返却");Check(start!.Feature==e.Feature && start.User==e.User,"返却対応");
    if(f.Pool=="SMD3")Check(!live.Values.Any(x=>x.User==e.User && c.Features[x.Feature].Pool=="VBL"),"オプション未返却で基本製品終了");
    hours[e.Feature]=hours.GetValueOrDefault(e.Feature)+(e.At-start.At).TotalHours*cost;used[f.Pool]-=cost;live.Remove(e.Session);
   }else Check(e.Exhaustion && used[f.Pool]+cost>c.Capacity(f.Pool),"残容量があるのに枯渇拒否");
  }
  Check(live.Count==0,"未返却");Check(report.WarningCount==0,"解析警告");
  foreach(var (feature,total) in hours){var measured=report.Sessions.Where(s=>s.Feature==feature).Sum(s=>(s.End-s.Start).TotalHours*s.Units);Check(Math.Abs(total-measured)<1e-6,"時間積分 "+feature);}
  var baseUsers=truth.Where(e=>e.Action=="OUT" && c.Features[e.Feature].Pool=="SMD3").Select(e=>e.User).Distinct().Count();Check(baseUsers==c.Users,"利用者総数");
  var average=Generator.Utilization(c,truth);Check(Math.Abs(average-c.TargetUtilization)<.002,"平均80%の許容差0.2ポイント");
  Check(truth.Any(e=>e.Exhaustion && c.Features[e.Feature].Pool=="SMD3"),"SMD3枯渇が発生しない");
  var result=new{passed=true,eventCount=truth.Count,users=baseUsers,smd3Average=average,smd3Peak=peaks["SMD3"]/3.0,vblPeak=peaks["VBL"],featureUnitHours=hours,checks=new[]{"全ログイベントを生成正解データと照合","全取得・返却ペアを照合","全時点で容量を厳守","枯渇拒否の残容量を検証","VBLの基本製品依存を検証","同一利用者の基本製品二重起動なし","機能別時間積分を独立照合","50人全員が利用","平均稼働率80% ±0.2ポイント","SMD3枯渇が発生"}};
  File.WriteAllText(Path.Combine(dir,"audit.json"),Json.Dump(result));return result;
 }
}
