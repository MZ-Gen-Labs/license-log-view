using LicenseLogView;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Net;

var root=FindDataRoot();
string Arg(string key,string fallback){var i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length?args[i+1]:fallback;}
try {
 var command=args.FirstOrDefault() ?? "serve";
 if(command=="selftest")throw new Exception("Tests are separate: dotnet run --project tests/LicenseLogView.Tests/LicenseLogView.Tests.csproj -c Release");
 var config=Json.Load(Arg("--config",Path.Combine(root,"config.json")));
 config.Seed=int.Parse(Arg("--seed",config.Seed.ToString()));
 var logs=Arg("--logs",Path.Combine(root,"data"));var resultDir=Arg("--out",Path.Combine(root,"reports"));
 IEnumerable<(string Name,string Text)> ReadLogs()=>Directory.GetFiles(logs,"*.log").OrderBy(x=>x).Select(p=>(Path.GetFileName(p),File.ReadAllText(p)));
 if(command=="generate") {Console.WriteLine(Json.Dump(Generator.Generate(config,logs)));Audit.Run(config,logs);Console.WriteLine("Audit passed. See audit.json.");return;}
 if(command=="analyze") {var r=Analyzer.Analyze(config,ReadLogs());Analyzer.Export(r,resultDir);Console.WriteLine(Json.Dump(r.Summary));return;}
 if(command=="audit"){Console.WriteLine(Json.Dump(Audit.Run(config,logs)));return;}
 if(command!="serve")throw new Exception("Commands: serve | generate | analyze | audit | selftest. Options: --config FILE --logs DIR --out DIR --port 8765 --no-browser");
 var port=int.Parse(Arg("--port","8765"));if(port<1024 || port>65535)throw new Exception("ポートは1024〜65535にしてください。");
 var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[],ContentRootPath=root});
 builder.WebHost.ConfigureKestrel(k=>{k.Listen(IPAddress.Loopback,port);k.Limits.MaxRequestBodySize=100*1024*1024;});
 builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o=>{o.MultipartBodyLengthLimit=100*1024*1024;o.MemoryBufferThreshold=100*1024*1024;});
 var app=builder.Build();var gate=new object();var current=Analyzer.Analyze(config,ReadLogs());
 var html=ReadHtml();
 app.Use(async(ctx,next)=>{
  if(ctx.Request.Host.Host!="127.0.0.1" || ctx.Request.Host.Port!=port){ctx.Response.StatusCode=403;return;}
  if(ctx.Request.Method=="POST" && ctx.Request.Headers.Origin.Count>0 && ctx.Request.Headers.Origin.ToString()!=$"http://127.0.0.1:{port}"){ctx.Response.StatusCode=403;return;}
  ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["Cache-Control"]="no-store";
  ctx.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; object-src 'none'; frame-ancestors 'none'";
  try{await next();}catch(Exception e){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{error=e.Message});}
 });
 app.MapGet("/",()=>Results.Content(html,"text/html; charset=utf-8"));
 app.MapGet("/api/report",()=>{lock(gate)return Results.Json(current,Json.Options);});
 app.MapPost("/api/analyze",async(HttpRequest request)=>{
  var form=await request.ReadFormAsync();if(form.Files.Count<1 || form.Files.Count>24)throw new Exception("ログは1〜24ファイルを選択してください。");
  var raw=form["config"].ToString();if(raw.Length>1024*1024)throw new Exception("設定が大きすぎます。");
  var c=System.Text.Json.JsonSerializer.Deserialize<Config>(raw,Json.Options)??throw new Exception("設定がありません。");c.Validate();
  var files=new List<(string,string)>();foreach(var f in form.Files){if(!f.FileName.EndsWith(".log",StringComparison.OrdinalIgnoreCase) && !f.FileName.EndsWith(".txt",StringComparison.OrdinalIgnoreCase))throw new Exception(".log / .txtを選んでください。");using var reader=new StreamReader(f.OpenReadStream(),Encoding.UTF8,true);files.Add((Path.GetFileName(f.FileName),await reader.ReadToEndAsync()));}
  var r=Analyzer.Analyze(c,files);lock(gate)current=r;return Results.Json(r,Json.Options);
 }).DisableAntiforgery();
 app.MapPost("/api/generate",async(HttpRequest request)=>{
  var c=await request.ReadFromJsonAsync<Config>(Json.Options)??throw new Exception("設定がありません。");c.Validate();
  var dir=Path.Combine(Path.GetTempPath(),"license-log-view-"+Guid.NewGuid().ToString("N"));
  try {
   await Task.Run(()=>{Generator.Generate(c,dir);Audit.Run(c,dir);File.WriteAllText(Path.Combine(dir,"config.json"),Json.Dump(c));});
   using var bytes=new MemoryStream();using(var zip=new System.IO.Compression.ZipArchive(bytes,System.IO.Compression.ZipArchiveMode.Create,true))
    foreach(var file in Directory.GetFiles(dir))System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(zip,file,Path.GetFileName(file));
   return Results.File(bytes.ToArray(),"application/zip",$"license-log-view-generated-{c.Seed}.zip");
  }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 });
 app.MapGet("/export/{kind}",(string kind)=>{
  lock(gate){object data=kind switch{"daily"=>current.Daily,"monthly"=>current.Monthly,"sessions"=>current.Sessions,"users"=>current.Users,"options"=>current.Options,"denials"=>current.Denials,_=>throw new Exception("不明なCSV種別です。")};return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Analyzer.Csv(data))).ToArray(),"text/csv",$"{kind}.csv");}
 });
 app.MapGet("/export-report",()=>{lock(gate)return Results.File(Encoding.UTF8.GetBytes(Json.Dump(current)),"application/json","report.json");});
 app.MapGet("/export-html",()=>{lock(gate){var page=html.Replace("/*OFFLINE_DATA*/null",Json.Dump(current));return Results.File(Encoding.UTF8.GetBytes(page),"text/html","license-log-view-report.html");}});
 app.Lifetime.ApplicationStarted.Register(()=>{Console.WriteLine($"Local dashboard: http://127.0.0.1:{port}  (Ctrl+C to stop)");if(!args.Contains("--no-browser"))try{Process.Start(new ProcessStartInfo($"http://127.0.0.1:{port}"){UseShellExecute=true});}catch{}});
 await app.RunAsync();
} catch(Exception e){Console.Error.WriteLine(e.Message);Environment.ExitCode=1;}
static string ReadHtml(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LicenseLogView.Web.index.html")??throw new Exception("画面リソースがありません。");using var reader=new StreamReader(stream);return reader.ReadToEnd();}

static string FindDataRoot(){
 if(File.Exists(Path.Combine(Environment.CurrentDirectory,"config.json")))return Environment.CurrentDirectory;
 for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir!=null;dir=dir.Parent)
  if(File.Exists(Path.Combine(dir.FullName,"config.json")))return dir.FullName;
 return AppContext.BaseDirectory;
}
