using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace Wds.Resone.Launcher;
public sealed class EnginePack
{
 public string Rid {get;set;}="win-x64";
 public string Backend {get;set;}="cpu";
 public string Url {get;set;}="";
 public string Sha256 {get;set;}="";
 public string Directory {get;set;}="engines/llm";
 public long MaxExtractedBytes {get;set;}=4L*1024*1024*1024;
}
public static class EnginePackInstaller
{
 public static string Rid => (OperatingSystem.IsWindows()?"win":OperatingSystem.IsMacOS()?"osx":"linux")+"-"+RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
 public static string Backend(RuntimeConfig cfg)
 {
  if(cfg.Backend!="auto")return cfg.Backend;
  if(cfg.EnginePacks.Any(p=>p.Rid==Rid&&p.Backend=="cuda")&&NativeLibrary.TryLoad(OperatingSystem.IsWindows()?"nvcuda.dll":"libcuda.so.1",out var h)){NativeLibrary.Free(h);return "cuda";}
  if(OperatingSystem.IsMacOS()&&cfg.EnginePacks.Any(p=>p.Rid==Rid&&p.Backend=="metal"))return "metal";
  return "cpu";
 }
 public static string Under(string root,string relative)
 {
  if(Path.IsPathRooted(relative)||relative.Split('/','\\').Any(x=>x==".."||x.Contains(':')))throw new InvalidDataException("Package path must be relative and stay inside the installation.");
  var path=Path.GetFullPath(Path.Combine(root,relative));
  if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidDataException("Package path escapes installation.");
  return path;
 }
 public static async Task InstallAsync(string root,EnginePack pack,HttpClient http,Action<string> progress,CancellationToken token)
 {
  if(pack.Sha256.Length!=64||!pack.Sha256.All(Uri.IsHexDigit))throw new InvalidDataException("Engine pack requires a SHA256 hash.");
  if(!Uri.TryCreate(pack.Url,UriKind.Absolute,out var url)||url.Scheme!="https")throw new InvalidDataException("Engine pack URL must use HTTPS.");
  var target=Under(root,pack.Directory);var receipt=Path.Combine(target,".resone-pack-sha256");
  if(File.Exists(receipt)&&(await File.ReadAllTextAsync(receipt,token)).Trim()==pack.Sha256)return;
  var work=Under(root,"work/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
  var archive=Path.Combine(work,"pack.zip");var stage=Path.Combine(work,"expanded");Directory.CreateDirectory(stage);
  try{
   progress("Downloading "+pack.Rid+" "+pack.Backend+" engine pack");
   using(var r=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token)){
    r.EnsureSuccessStatusCode();await using var input=await r.Content.ReadAsStreamAsync(token);await using var output=File.Create(archive);var buffer=new byte[81920];long bytes=0;int n;
    while((n=await input.ReadAsync(buffer,token))>0){bytes+=n;if(bytes>pack.MaxExtractedBytes)throw new InvalidDataException("Engine archive exceeds size limit.");await output.WriteAsync(buffer.AsMemory(0,n),token);}
   }
   await using(var input=File.OpenRead(archive)){if(!Convert.ToHexString(await SHA256.HashDataAsync(input,token)).Equals(pack.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Engine pack SHA256 mismatch.");}
   progress("Installing engine pack");long total=0;
   using(var zip=ZipFile.OpenRead(archive)){
    if(zip.Entries.Count>50000)throw new InvalidDataException("Too many archive entries.");
    foreach(var entry in zip.Entries){token.ThrowIfCancellationRequested();
     if(((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Archive symbolic links are not allowed.");
     total=checked(total+entry.Length);if(total>pack.MaxExtractedBytes)throw new InvalidDataException("Expanded engine pack exceeds size limit.");
     var name=entry.FullName.Replace('\\','/');var dest=Under(stage,name);
     if(name.EndsWith('/')){Directory.CreateDirectory(dest);continue;}
     Directory.CreateDirectory(Path.GetDirectoryName(dest)!);await using(var input=entry.Open())await using(var output=new FileStream(dest,FileMode.CreateNew))await input.CopyToAsync(output,token);
     if(!OperatingSystem.IsWindows()&&((entry.ExternalAttributes>>16)&0x49)!=0)File.SetUnixFileMode(dest,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute|UnixFileMode.GroupRead|UnixFileMode.GroupExecute|UnixFileMode.OtherRead|UnixFileMode.OtherExecute);
    }
   }
   await File.WriteAllTextAsync(Path.Combine(stage,".resone-pack-sha256"),pack.Sha256,token);
   Directory.CreateDirectory(Path.GetDirectoryName(target)!);var backup=target+".old-"+Guid.NewGuid().ToString("N");bool moved=false;
   try{if(Directory.Exists(target)){Directory.Move(target,backup);moved=true;}Directory.Move(stage,target);}catch{if(moved&&!Directory.Exists(target))Directory.Move(backup,target);throw;}
   if(moved)Directory.Delete(backup,true);
  }finally{if(Directory.Exists(work))Directory.Delete(work,true);}
 }
}
