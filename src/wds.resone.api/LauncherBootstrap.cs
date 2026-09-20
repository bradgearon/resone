using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
namespace Wds.Resone.Api;
public static class LauncherBootstrap
{
 public static string? InstallRoot { get; set; }
 public static string PipeName => "wds-resone-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName)))[..16];
 public static async Task<string> EnsureAsync(CancellationToken token)
 {
  string result=await SendCommandAsync("ensure",token);
  if(!result.StartsWith("ready ",StringComparison.Ordinal))throw new InvalidOperationException(result);
  return result[6..];
 }
 public static async Task EnsureRuntimeComponentAsync(string component,CancellationToken token)
 {
  component=component.Trim().ToLowerInvariant() switch
  {
   "tts"=>"tts",
   "asr" or "stt" or "whisper"=>"asr",
   "llm"=>"llm",
   _=>throw new ArgumentException("Unknown AI runtime component: "+component,nameof(component))
  };
  string result=await SendCommandAsync("ensure-component "+component,token);
  if(!result.StartsWith("ready component ",StringComparison.Ordinal))throw new InvalidOperationException(result);
 }
 private static async Task<string> SendCommandAsync(string command,CancellationToken token)
 {
  var root=Path.GetFullPath(InstallRoot??ResoneRoot.Resolve());
  var exe=ResoneRoot.LauncherExecutable(root);
  // A second launcher contacts the existing singleton and exits. Never wait on the DAW thread.
  // Explicitly pass the resolved runtime root so a launcher under build/launcher
  // reads the source-tree config/engines/models instead of its own build folder
  // or an older %LOCALAPPDATA% installation.
  var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root};
  start.ArgumentList.Add("--no-ui");
  start.Environment["RESONE_HOME"]=root;
  start.Environment[AiRuntimeRoot.EnvironmentVariable]=AiRuntimeRoot.Resolve();
  using var child=Process.Start(start);
  using var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(30));
  await pipe.ConnectAsync(timeout.Token);
  using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);
  await writer.WriteLineAsync(command.AsMemory(),timeout.Token);
  var result=await reader.ReadLineAsync(timeout.Token);if(result==null)throw new InvalidOperationException("Launcher disconnected during AI runtime provisioning.");
  if(result.StartsWith("Startup failed: ",StringComparison.Ordinal))throw new InvalidOperationException(result[16..]);
  return result;
 }
}
