using LicenseLogView;
namespace LicenseLogView.Tests;
public static class SelfTests {
 public static void Run(){int n=0;void Assert(bool x,string label){if(!x)throw new Exception(label);Console.WriteLine("PASS "+label);n++;}
  var c=new Config{Start=new(2026,7,1),EndExclusive=new(2026,7,2),Features=new(){{"SE",new("Solid Edge","SMD3",1)},{"NX",new("NX","SMD3",3)},{"OPT",new("Option","VBL",25)}}};
  Report Parse(string s)=>Analyzer.Analyze(c,new[]{("a.log",s)});
  string Header="00:00:00 (ugslmd) TIMESTAMP 7/1/2026\n";
  var r=Parse(Header+"09:00:00 (ugslmd) OUT: \"SE\" u@h\n09:00:00 (ugslmd) OUT: \"NX\" v@j\n10:00:00 (ugslmd) IN: \"SE\" u@h\n11:00:00 (ugslmd) IN: \"NX\" v@j\n");
  Assert(Math.Abs(r.Daily[0].Smd3Utilization-7.0/(30*9))<1e-9,"weighted shared pool exact time integral");
  Assert(Math.Abs(r.Daily[0].Smd3CalendarUtilization-7.0/(30*24))<1e-9,"calendar versus business denominator");
  Assert(r.Daily[0].Smd3Peak==4.0/3 && r.WarningCount==0,"mixed NX/SE peak");
  r=Parse(Header+"09:00:00 (ugslmd) OUT: \"OPT\" u@h (2 licenses)\n10:00:00 (ugslmd) IN: \"OPT\" u@h (1 license)\n11:00:00 (ugslmd) IN: \"OPT\" u@h (1 license)\n");
  Assert(Math.Abs(r.Sessions.Sum(s=>(s.End-s.Start).TotalHours*s.Units)-75)<1e-9 && r.WarningCount==0,"partial multi-license returns");
  r=Parse(Header+"09:00:00 (ugslmd) OUT: \"SE\" u@h [session=x]\n09:30:00 (ugslmd) OUT: \"SE\" u@h [session=y]\n10:00:00 (ugslmd) IN: \"SE\" u@h [session=y]\n11:00:00 (ugslmd) IN: \"SE\" u@h [session=x]\n");
  Assert(r.Sessions.Count==2 && r.Sessions.Sum(s=>(s.End-s.Start).TotalHours)==2.5,"session ID pairs out-of-order returns");
  r=Parse(Header+"09:00:00 (ugslmd) IN: \"SE\" u@h\n09:30:00 (ugslmd) OUT: \"SE\" u@h\n");Assert(r.WarningCount==2 && r.Sessions.Single().Censored,"missing checkout and unreturned warnings");
  r=Parse("09:00:00 (ugslmd) OUT: \"SE\" u@h\n");Assert(r.WarningCount>=1 && r.Sessions.Count==0,"missing date never guessed");
  r=Parse(Header+"09:00:00 (ugslmd) OUT: \"UNKNOWN\" u@h\n");Assert(r.WarningCount==1,"unknown feature visible warning");
  var warnings=new List<string>();var ev=Analyzer.Parse(new[]{("a.log",Header+"23:55:00 (ugslmd) OUT: \"SE\" u@h\n00:05:00 (ugslmd) IN: \"SE\" u@h\n")},warnings.Add);
  Assert(ev[1].At==new DateTime(2026,7,2,0,5,0),"midnight rollover");
  r=Parse(Header+"09:00:00 (ugslmd) DENIED: \"SE\" u@h Licensed number of users already reached. (-4,342)\n");Assert(r.Daily[0].Denied==1 && r.Sessions.Count==0,"denial does not consume licenses");
  var identical=Header+"09:00:00 (ugslmd) OUT: \"SE\" u@h\n10:00:00 (ugslmd) IN: \"SE\" u@h\n";r=Analyzer.Analyze(c,new[]{("a.log",identical),("b.log",identical)});Assert(r.Sessions.Count==1 && r.WarningCount==1,"duplicate files not double counted");
  var split=Analyzer.Analyze(new Config{Start=new(2026,7,1),EndExclusive=new(2026,8,2),Features=c.Features},new[]{("a.log","00:00:00 (ugslmd) TIMESTAMP 7/31/2026\n23:00:00 (ugslmd) OUT: \"SE\" u@h\n"),("b.log","00:00:00 (ugslmd) TIMESTAMP 8/1/2026\n01:00:00 (ugslmd) IN: \"SE\" u@h\n")});Assert(split.Sessions.Single().End-split.Sessions.Single().Start==TimeSpan.FromHours(2),"session crossing rotated logs / month");
  c.Holidays=["2026-07-01"];r=Parse(identical);Assert(r.Daily[0].BusinessHours==0 && r.Daily[0].Smd3CalendarUtilization>0,"holiday exclusion keeps calendar usage");
  Console.WriteLine($"{n} tests passed.");
 }
}
