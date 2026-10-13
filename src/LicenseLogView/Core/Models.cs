using System.Text.Json;
namespace LicenseLogView;
public record Feature(string Product, string Pool, int Units);
public class Config {
 public DateTime Start {get;set;} = new(2026,7,1);
 public DateTime EndExclusive {get;set;} = new(2026,10,1);
 public string Timezone {get;set;} = "Asia/Tokyo";
 public int BusinessStartHour {get;set;} = 9;
 public int BusinessEndHour {get;set;} = 18;
 public string[] Holidays {get;set;} = [];
 public int Smd3Capacity {get;set;} = 10;
 public int VblCapacity {get;set;} = 200;
 public int Seed {get;set;} = 20260701;
 public int Users {get;set;} = 50;
 public double TargetUtilization {get;set;} = .8;
 public Dictionary<string,Feature> Features {get;set;} = new();
 public int Capacity(string pool) => pool=="SMD3" ? Smd3Capacity*3 : VblCapacity;
 public bool Working(DateTime d) => d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !Holidays.Contains(d.ToString("yyyy-MM-dd"));
 public void Validate() {
  if(Start!=Start.Date || EndExclusive!=EndExclusive.Date || EndExclusive<=Start || (EndExclusive-Start).TotalDays>370) throw new Exception("期間は日単位、1〜370日にしてください。");
  if(BusinessStartHour<0 || BusinessEndHour>24 || BusinessEndHour<=BusinessStartHour) throw new Exception("営業時間が不正です。");
  if(Smd3Capacity<=0 || VblCapacity<=0 || Users<1 || Users>1000 || TargetUtilization<=0 || TargetUtilization>=1) throw new Exception("容量・利用者数・目標値が不正です。");
  if(Features.Count==0 || Features.Any(x=>x.Value.Units<=0 || x.Value.Pool is not ("SMD3" or "VBL"))) throw new Exception("機能マッピングが不正です。");
 }
}
public static class Json {
 public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web){WriteIndented=true};
 public static Config Load(string path) {var c=JsonSerializer.Deserialize<Config>(File.ReadAllText(path),Options) ?? throw new Exception("設定を読み込めません。"); c.Validate(); return c;}
 public static string Dump(object x)=>JsonSerializer.Serialize(x,Options);
}
public record Event(DateTime At, string Action, string Feature, string User, string Host, int Count, string Session, bool Exhaustion, string Source, int Line);
public record Session(DateTime Start, DateTime End, string Feature, string Product, string Pool, string User, string Host, int Units, bool Censored);
public class Bucket {
 public DateTime At {get;set;}
 public double Smd3Minutes {get;set;}
 public double VblMinutes {get;set;}
 public double SeMinutes {get;set;}
 public double NxMinutes {get;set;}
 public int Smd3Peak {get;set;}
 public int VblPeak {get;set;}
 public double FullMinutes {get;set;}
 public int Denied {get;set;}
 public int VblDenied {get;set;}
}
public record Daily(string Date, bool Working, double BusinessHours, double Smd3Utilization, double VblUtilization, double Smd3CalendarUtilization, double VblCalendarUtilization, double SeHours, double NxHours, double Smd3Peak, int VblPeak, double FullMinutes, int Denied, int VblDenied);
public class Report {
 public Config Config {get;set;} = new();
 public object Summary {get;set;} = new();
 public List<Daily> Daily {get;set;}=[];
 public List<object> Monthly {get;set;}=[];
 public List<object> Samples {get;set;}=[];
 public List<object> Users {get;set;}=[];
 public List<object> Options {get;set;}=[];
 public List<object> Denials {get;set;}=[];
 public List<Session> Sessions {get;set;}=[];
 public List<string> Warnings {get;set;}=[];
 public int WarningCount {get;set;}
}
