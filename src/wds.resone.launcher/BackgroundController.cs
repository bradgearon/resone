using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
namespace Wds.Resone.Launcher;
internal sealed class BackgroundController(string root,HttpClient http,CancellationTokenSource shutdown,bool skipServices=false):IDisposable
{
 public event Action<string>? StatusChanged;
 private void Report(string message){StatusChanged?.Invoke(message);Console.WriteLine(message);}
 private readonly SemaphoreSlim gate=new(1,1);
 private Process? worker;private ServiceSupervisor? supervisor;private string secret="";private bool enabled=true;private int restarts;
 public void OpenUi(){
  var candidates=new[]{"wds.resone.ui.exe","Resone.exe","../ui/out/Resone.exe","build/ui/out/Resone.exe"}.Select(p=>Path.GetFullPath(Path.Combine(root,p)));
  var path=candidates.FirstOrDefault(File.Exists)??throw new FileNotFoundException("Standalone UI not found. Expected wds.resone.ui.exe beside the launcher or build/ui/out/Resone.exe.");
  var start=new ProcessStartInfo(path){UseShellExecute=false,WorkingDirectory=root};start.Environment["RESONE_HOME"]=root;using var p=Process.Start(start);
 }
 public async Task<string> EnsureAsync(CancellationToken token){await gate.WaitAsync(token);try{if(restarts>3)throw new InvalidOperationException("Repeated native crashes. Toggle the AI stack off/on after correcting the runtime.");if(!enabled)throw new InvalidOperationException("AI stack is off. Enable it from the Resone tray menu.");return await StartLocked(token);}finally{gate.Release();}}
 private async Task<string> StartLocked(CancellationToken token){
  if(worker is {HasExited:false})return secret;
  if(supervisor==null){supervisor=new(root,http,Report);try{if(!skipServices)await supervisor.StartAsync(token);}catch{supervisor.Dispose();supervisor=null;throw;}}
  string ready="resone-ready-"+Guid.NewGuid().ToString("N");
  using var pipe=new NamedPipeServerStream(ready,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
  var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,WorkingDirectory=root,CreateNoWindow=true};start.ArgumentList.Add("--worker");
  secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));start.Environment["RESONE_HOME"]=root;start.Environment["RESONE_READY_PIPE"]=ready;start.Environment["RESONE_SESSION_TOKEN"]=secret;start.Environment["RESONE_SELECTED_BACKEND"]=supervisor.SelectedBackend;
  var p=new Process{StartInfo=start,EnableRaisingEvents=true};p.Start();worker=p;
  try{
   using var readyStop=CancellationTokenSource.CreateLinkedTokenSource(token);readyStop.CancelAfter(TimeSpan.FromSeconds(30));
   var connected=pipe.WaitForConnectionAsync(readyStop.Token);var exited=p.WaitForExitAsync(readyStop.Token);
   if(await Task.WhenAny(connected,exited)==exited)throw new InvalidOperationException("Resone worker exited before becoming ready.");
   await connected;using var reader=new StreamReader(pipe);if(await reader.ReadLineAsync(readyStop.Token)!="ready")throw new InvalidOperationException("Invalid worker readiness signal.");
   p.Exited+=(sender,eventArgs)=>{ _ = RestartAfterExitAsync(p); };
   if(p.HasExited)throw new InvalidOperationException("Resone worker exited during startup.");
   Report("Resone AI stack is ready.");return secret;
  }catch{worker=null;try{if(!p.HasExited)p.Kill(true);}catch{}p.Dispose();throw;}
 }
 private async Task RestartAfterExitAsync(Process exited){
  await gate.WaitAsync();try{
   if(!ReferenceEquals(worker,exited)||!enabled||shutdown.IsCancellationRequested)return;
   worker=null;exited.Dispose();if(++restarts>3){Console.Error.WriteLine("AI worker repeatedly exited. Use tray Toggle AI stack to retry after checking the runtime.");return;}
   try{await StartLocked(shutdown.Token);}catch(Exception e){Console.Error.WriteLine("AI restart failed: "+e.Message);}
  }finally{gate.Release();}
 }
 public async Task ToggleAsync(){await gate.WaitAsync();try{if(worker is {HasExited:false}){enabled=false;StopLocked();}else{enabled=true;restarts=0;await StartLocked(shutdown.Token);}}catch(Exception e){Console.Error.WriteLine(e.Message);}finally{gate.Release();}}
 private void StopLocked(){var old=worker;worker=null;try{if(old is {HasExited:false})old.Kill(true);}catch{}old?.Dispose();supervisor?.Dispose();supervisor=null;}
 public void Dispose(){gate.Wait();try{enabled=false;StopLocked();}finally{gate.Release();}}
}
