namespace LicenseLogView;
public static class Generator {
 record Lease(string Feature,int User,DateTime End,string Id);
 public static List<Event> Simulate(Config c, double baseline) {
  var rng=new Random(c.Seed); var events=new List<Event>(); var seq=0;
  var options=c.Features.Where(x=>x.Value.Pool=="VBL").Select(x=>x.Key).ToArray();
  var se=c.Features.First(x=>x.Value.Product=="Solid Edge" && x.Value.Pool=="SMD3").Key;
  var nx=c.Features.First(x=>x.Value.Product=="NX" && x.Value.Pool=="SMD3").Key;
  var cap=c.Capacity("SMD3");
  void Emit(DateTime t,string action,Lease l,bool denied=false) => events.Add(new(t,action,l.Feature,$"user{l.User+1:00}",$"PC-{l.User+1:00}",1,l.Id,denied,"synthetic",0));
  for(var d=c.Start;d<c.EndExclusive;d=d.AddDays(1)) {
   var working=c.Working(d);
   if(!working && rng.NextDouble()>.35) continue;
   var bases=new List<Lease>(); var opts=new List<Lease>(); var dayNoise=rng.NextDouble()*2-1;
   var close=working?d.AddHours(Math.Min(24,c.BusinessEndHour+2)):d.AddHours(16);
   var open=working?d.AddHours(c.BusinessStartHour).AddMinutes(-30):d.AddHours(10);
   for(var t=open;t<close;t=t.AddMinutes(5)) {
    foreach(var l in bases.Where(x=>x.End<=t).ToArray()) {
     foreach(var o in opts.Where(x=>x.User==l.User).ToArray()){Emit(t,"IN",o);opts.Remove(o);}
     Emit(t,"IN",l);bases.Remove(l);
    }
    foreach(var l in opts.Where(x=>x.End<=t).ToArray()){Emit(t,"IN",l);opts.Remove(l);}
    var progress=(t-d.AddHours(c.BusinessStartHour)).TotalHours/(c.BusinessEndHour-c.BusinessStartHour);
    // Morning / afternoon peaks and quieter lunch; demand, not consumption, may exceed capacity.
    var shape=progress<.09?-7:progress<.22?-2:progress<.36?4:progress<.46?1:progress<.57?-5:progress<.80?4:progress<.93?0:-6;
    var business=working && t>=d.AddHours(c.BusinessStartHour) && t<d.AddHours(c.BusinessEndHour);
    var target=business?Math.Max(1,(int)Math.Round(baseline+shape+dayNoise*1.5+(rng.NextDouble()-.5)*2)):
       working?(t.Hour<c.BusinessStartHour?2+rng.Next(3):Math.Max(0,(int)(8*(close-t).TotalHours/2)+rng.Next(3))):3+rng.Next(5);
    if(business && d.Day%5==0 && t.Hour==14 && t.Minute<30)target=cap+4;
    var used=bases.Sum(x=>c.Features[x.Feature].Units);
    var available=Enumerable.Range(0,c.Users).Where(u=>!bases.Any(x=>x.User==u)).OrderBy(_=>rng.Next()).ToArray();
    foreach(var u in available) {
     if(used>=target) break;
     // Eight of fifty users can use NX; they mostly use Solid Edge too.
     var f=u<Math.Max(1,c.Users*8/50) && rng.NextDouble()<.20?nx:se;
     var l=new Lease(f,u,t.AddMinutes(rng.Next(12,37)*5),$"B{++seq:000000}");
     var cost=c.Features[f].Units;
     if(used+cost>cap){Emit(t,"DENIED",l,true); if(rng.NextDouble()<.65) break;continue;}
     bases.Add(l); used+=cost; Emit(t,"OUT",l);
    }
    foreach(var b in bases.Where(x=>x.Feature==se).ToArray()) {
     if(opts.Any(x=>x.User==b.User) || rng.NextDouble()>=.025 || options.Length==0) continue;
     var f=options[rng.Next(options.Length)]; var l=new Lease(f,b.User,t.AddMinutes(rng.Next(4,13)*5),$"V{++seq:000000}");
     if(opts.Sum(x=>c.Features[x.Feature].Units)+c.Features[f].Units>c.VblCapacity) Emit(t,"DENIED",l,true);
     else {opts.Add(l);Emit(t,"OUT",l);}
    }
   }
   foreach(var o in opts) Emit(close,"IN",o);
   foreach(var b in bases) Emit(close,"IN",b);
  }
  return events;
 }
 public static double Utilization(Config c,List<Event> events) {
  var live=0;double minutes=0; var last=c.Start;
  foreach(var e in events){
   for(var d=last.Date;d<=e.At.Date;d=d.AddDays(1)){
    if(!c.Working(d))continue;
    var from=last>d.AddHours(c.BusinessStartHour)?last:d.AddHours(c.BusinessStartHour);
    var to=e.At<d.AddHours(c.BusinessEndHour)?e.At:d.AddHours(c.BusinessEndHour);
    if(to>from)minutes+=live*(to-from).TotalMinutes;
   }
   last=e.At;if(c.Features[e.Feature].Pool=="SMD3")live+=(e.Action=="OUT"?1:e.Action=="IN"?-1:0)*c.Features[e.Feature].Units;
  }
  var days=Enumerable.Range(0,(c.EndExclusive-c.Start).Days).Count(i=>c.Working(c.Start.AddDays(i)));
  return minutes/(days*(c.BusinessEndHour-c.BusinessStartHour)*60*c.Capacity("SMD3"));
 }
 public static object Generate(Config c,string dir) {
  double lo=1,hi=c.Capacity("SMD3")*1.5; List<Event> best=[];double error=double.MaxValue,level=0;
  for(int i=0;i<16;i++){var mid=(lo+hi)/2;var es=Simulate(c,mid);var u=Utilization(c,es);if(Math.Abs(u-c.TargetUtilization)<error){error=Math.Abs(u-c.TargetUtilization);best=es;level=mid;}if(u<c.TargetUtilization)lo=mid;else hi=mid;}
  Directory.CreateDirectory(dir);
  File.WriteAllText(Path.Combine(dir,"truth-events.json"),Json.Dump(best));
  foreach(var month in Enumerable.Range(0,(c.EndExclusive-c.Start).Days).Select(i=>c.Start.AddDays(i).ToString("yyyy-MM")).Distinct()) {
   using var writer=new StreamWriter(Path.Combine(dir,$"flexlm-{month}.log"),false,new System.Text.UTF8Encoding(false));
   writer.WriteLine("# SYNTHETIC TEST DATA. Feature names and VBL costs are fictional. Not a Siemens license file.");
   foreach(var d in Enumerable.Range(0,(c.EndExclusive-c.Start).Days).Select(i=>c.Start.AddDays(i)).Where(d=>d.ToString("yyyy-MM")==month)) {
    writer.WriteLine($"00:00:00 (ugslmd) TIMESTAMP {d:M/d/yyyy}");
    foreach(var e in best.Where(e=>e.At.Date==d)) {
     var suffix=e.Action=="DENIED"?" Licensed number of users already reached. (-4,342)":"";
     writer.WriteLine($"{e.At:HH:mm:ss} (ugslmd) {e.Action}: \"{e.Feature}\" {e.User}@{e.Host} [session={e.Session}]{suffix}");
    }
   }
  }
  var manifest=new {synthetic=true,seed=c.Seed,start=c.Start,endExclusive=c.EndExclusive,users=c.Users,targetUtilization=c.TargetUtilization,measuredUtilization=Utilization(c,best),demandBaseline=level,events=best.Count,denied=best.Count(e=>e.Exhaustion),simulationStepMinutes=5};
  File.WriteAllText(Path.Combine(dir,"manifest.json"),Json.Dump(manifest));return manifest;
 }
}
