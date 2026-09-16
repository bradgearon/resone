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
  var root=InstallRoot??Environment.GetEnvironmentVariable("RESONE_HOME")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Wds","Resone");
  var exe=Path.Combine(root,OperatingSystem.IsWindows()?"wds.resone.launcher.exe":"wds.resone.launcher");
  if(!File.Exists(exe))throw new FileNotFoundException("Install the Resone launcher before using the plugin.",exe);
  // A second launcher contacts the existing singleton and exits. Never wait on the DAW thread.
  using var child=Process.Start(new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root,ArgumentList={"--no-ui"}});
  using var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(30));
  await pipe.ConnectAsync(timeout.Token);
  using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);
  await writer.WriteLineAsync("ensure".AsMemory(),timeout.Token);
  var result=await reader.ReadLineAsync(timeout.Token);if(result==null || !result.StartsWith("ready "))throw new InvalidOperationException(result??"Launcher disconnected during startup.");
  return result[6..];
 }
}
