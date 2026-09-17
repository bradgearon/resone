using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Wds.Resone.Api;
using Wds.Resone.Launcher;

if(args.Contains("--worker")){await WorkerHost.RunAsync([]);return;}
string root=ResoneRoot.Resolve();
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
using(var controller=new BackgroundController(root,http,shutdown,args.Contains("--no-services")))
using(var tray=new TrayIcon(()=>controller.OpenUi(),()=>controller.ToggleAsync(),()=>shutdown.Cancel()))
{
 Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;shutdown.Cancel();};
 tray.Start();
 controller.StatusChanged+=tray.Status;
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
   else await writer.WriteLineAsync("Unknown launcher command.");
  }catch(Exception e){try{using var writer=new StreamWriter(pipe){AutoFlush=true};await writer.WriteLineAsync("Startup failed: "+e.Message);}catch{}}
 }
}
