using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Wds.Resone.Api;
using Wds.Resone.Launcher;

int activationFileIndex=Array.IndexOf(args,"--activate-license-file");
if(activationFileIndex>=0)
{
 try
 {
  if(activationFileIndex+1>=args.Length)throw new ArgumentException("Missing activation file path.");
  string activationRoot=ResoneRoot.Resolve();
  string licenseKey=(await File.ReadAllTextAsync(args[activationFileIndex+1])).Trim();
  if(string.IsNullOrWhiteSpace(licenseKey))throw new InvalidDataException("License key is empty.");
  using var activationHttp=new HttpClient{Timeout=Timeout.InfiniteTimeSpan};
  await new LicenseService(activationRoot,activationHttp).ActivateAsync(licenseKey,CancellationToken.None);
  Environment.ExitCode=0;return;
 }
 catch(Exception e){Console.Error.WriteLine("Activation failed: "+e.Message);Environment.ExitCode=20;return;}
}

int reloadWorkerIndex=Array.IndexOf(args,"--reload-worker");
if(reloadWorkerIndex>=0)
{
 if(reloadWorkerIndex+1>=args.Length)throw new ArgumentException("Missing worker executable path.");
 string workerExecutable=Path.GetFullPath(args[reloadWorkerIndex+1]);
 using var pipe=new NamedPipeClientStream(".",LauncherBootstrap.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
 using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
 await pipe.ConnectAsync(stop.Token);
 using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
 using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);
 await writer.WriteLineAsync("reload-worker "+workerExecutable);
 string? response=await reader.ReadLineAsync(stop.Token);
 if(response is null || !response.StartsWith("ready",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(response??"Launcher did not acknowledge worker reload.");
 return;
}

if(args.Contains("--worker")){await WorkerHost.RunAsync([]);return;}
string root=ResoneRoot.Resolve();
string aiRoot=AiRuntimeLocation.Configure(args);
string user=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Wds","Resone","user");Directory.CreateDirectory(user);
FileStream owner;
try{owner=new FileStream(Path.Combine(user,"launcher.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
catch(IOException){
 if(!args.Contains("--no-ui")){
  using var pipe=new NamedPipeClientStream(".",LauncherBootstrap.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
  using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(15));await pipe.ConnectAsync(stop.Token);
  using var writer=new StreamWriter(pipe){AutoFlush=true};await writer.WriteLineAsync("open");
 }
 return;
}
using(owner)
using(var shutdown=new CancellationTokenSource())
using(var http=new HttpClient{Timeout=Timeout.InfiniteTimeSpan})
using(var controller=new BackgroundController(root,aiRoot,http,shutdown,args.Contains("--no-services")))
using(var tray=new TrayIcon(()=>controller.OpenUi(),()=>controller.ToggleAsync(),()=>shutdown.Cancel()))
{
 Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;shutdown.Cancel();};
 tray.Start();
 controller.StatusChanged+=tray.Status;
 var updateService=new AppUpdateService(root,http,tray);
 if(await updateService.TryStartUpdateAsync(args,aiRoot,shutdown.Token)){shutdown.Cancel();return;}
 var listener=ListenAsync();
 if(!args.Contains("--no-ui"))controller.OpenUi();
 try{await controller.EnsureAsync(shutdown.Token);}catch(Exception e){Console.Error.WriteLine("AI setup: "+e.Message);tray.Status("AI setup failed: "+e.Message);}
 try{await listener;}catch(OperationCanceledException){}
 async Task ListenAsync(){
  while(!shutdown.IsCancellationRequested){
   var pipe=new NamedPipeServerStream(LauncherBootstrap.PipeName,PipeDirection.InOut,16,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
   try{await pipe.WaitForConnectionAsync(shutdown.Token);}catch{pipe.Dispose();throw;}
   _=Handle(pipe);
  }
 }
 async Task Handle(NamedPipeServerStream pipe){
  using(pipe)try{
   using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
   using var request=CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);request.CancelAfter(TimeSpan.FromMinutes(30));
   string? cmd=await reader.ReadLineAsync(request.Token);
   if(cmd=="open"){controller.OpenUi();await writer.WriteLineAsync("ok");}
   else if(cmd=="ensure"){string secret=await controller.EnsureAsync(request.Token);await writer.WriteLineAsync("ready "+secret);}
   else if(cmd is not null && cmd.StartsWith("reload-worker ",StringComparison.OrdinalIgnoreCase)){
    string executable=cmd["reload-worker ".Length..].Trim();
    string secret=await controller.ReloadWorkerAsync(executable,request.Token);
    await writer.WriteLineAsync("ready "+secret);
   }
   else if(cmd is not null && cmd.StartsWith("ensure-component ",StringComparison.OrdinalIgnoreCase)){
    string component=cmd["ensure-component ".Length..].Trim();
    await controller.EnsureRuntimeComponentAsync(component,request.Token);
    await writer.WriteLineAsync("ready component "+component);
   }
   else await writer.WriteLineAsync("Unknown launcher command.");
  }catch(Exception e){try{using var writer=new StreamWriter(pipe){AutoFlush=true};await writer.WriteLineAsync("Startup failed: "+e.Message);}catch{}}
 }
}
