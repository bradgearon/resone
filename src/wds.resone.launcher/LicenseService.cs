using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Wds.Resone.Api;
namespace Wds.Resone.Launcher;
public sealed class LicenseService
{
 private readonly HttpClient http; private readonly JsonObject config; private readonly string file; private readonly SemaphoreSlim gate=new(1,1);
 private static byte[] Decode(string s)=>Convert.FromBase64String(s.Replace('-','+').Replace('_','/').PadRight((s.Length+3)/4*4,'='));
 private static string Encode(byte[] b)=>Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');
 public bool Enabled {
 get {
#if RESONE_LICENSED_RELEASE
 return true;
#else
 return config["enabled"]?.GetValue<bool>()??false;
#endif
 }}
 public LicenseService(string root,HttpClient client){http=client;var path=Path.Combine(root,"config","licensing.json");config=File.Exists(path)?JsonNode.Parse(File.ReadAllText(path))!.AsObject():new();var user=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Wds","Resone","user");Directory.CreateDirectory(user);file=Path.Combine(user,"license.dat");}
 private ECDsa Device()
 {
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Device key storage currently supports Windows; add Keychain/OS keystore integration before shipping another platform.");
  const string name="Wds.Resone.Device.v1";
  using var key=CngKey.Exists(name)?CngKey.Open(name):CngKey.Create(CngAlgorithm.ECDsaP256,name,new CngKeyCreationParameters {ExportPolicy=CngExportPolicies.None,KeyUsage=CngKeyUsages.Signing});
  return new ECDsaCng(key);
 }
 private static JsonObject Public(ECDsa key){var p=key.ExportParameters(false);return new(){["crv"]="P-256",["kty"]="EC",["x"]=Encode(p.Q.X!),["y"]=Encode(p.Q.Y!)};}
 private JsonObject Load()=>File.Exists(file)?JsonNode.Parse(Encoding.UTF8.GetString(Protect(File.ReadAllBytes(file),false)))!.AsObject():new();
 private void Save(JsonObject value){var tmp=file+".tmp";File.WriteAllBytes(tmp,Protect(Encoding.UTF8.GetBytes(value.ToJsonString()),true));File.Move(tmp,file,true);}
 private bool Valid(string? token,ECDsa device)
 {
  if(string.IsNullOrEmpty(token))return false;
  try{
   var p=token.Split('.');if(p.Length!=3)return false;
   var header=JsonNode.Parse(Decode(p[0]))!;if(header["alg"]?.GetValue<string>()!="ES256")return false;
   string pub;
#if RESONE_LICENSED_RELEASE
   using(var resource=typeof(LicenseService).Assembly.GetManifestResourceStream("Resone.LicensePublicKey")??throw new InvalidDataException("Release public key is missing."))using(var reader=new StreamReader(resource))pub=reader.ReadToEnd();
#else
   pub=config["publicKey"]?.ToJsonString()??"{}";
#endif
   var j=JsonNode.Parse(pub)!;using var verifier=ECDsa.Create(new ECParameters{Curve=ECCurve.NamedCurves.nistP256,Q=new ECPoint{X=Decode(j["x"]!.GetValue<string>()),Y=Decode(j["y"]!.GetValue<string>())}});
   if(!verifier.VerifyData(Encoding.UTF8.GetBytes(p[0]+"."+p[1]),Decode(p[2]),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))return false;
   var c=JsonNode.Parse(Decode(p[1]))!;long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),issued=c["iat"]!.GetValue<long>(),expires=c["exp"]!.GetValue<long>();
   var fingerprint=Encode(SHA256.HashData(Encoding.UTF8.GetBytes(Public(device).ToJsonString())));
   return c["iss"]!.GetValue<string>()=="resone-licensing"&&c["aud"]!.GetValue<string>()=="wds.resone"&&c["device"]!.GetValue<string>()==fingerprint&&expires>now&&issued<=now+30&&expires-issued<=604830;
  }catch{return false;}
 }
 private async Task<string> InstructionKeyAsync(string licenseKey,ECDsa device,CancellationToken token)
 {
  var result=await Exchange("instruction-key",licenseKey,device,token);
  string key=result["instructionKey"]?.GetValue<string>()??throw new InvalidDataException("License service did not return an instruction key.");
  string expectedDevice=Encode(SHA256.HashData(Encoding.UTF8.GetBytes(Public(device).ToJsonString())));
  if(result["device"]?.GetValue<string>()!=expectedDevice)throw new InvalidDataException("Instruction key grant was not bound to this device.");
  byte[] decoded=Convert.FromBase64String(key);try{if(decoded.Length!=32)throw new InvalidDataException("License service returned an invalid instruction key.");}finally{CryptographicOperations.ZeroMemory(decoded);}
  InstructionContent.ConfigureReleaseKey(key);return key;
 }
 public async Task EnsureAsync(CancellationToken token)
 {
  if(!Enabled)return;
  await gate.WaitAsync(token);try{
   using var device=Device();var saved=Load();string? lease=saved["lease"]?.GetValue<string>();string? key=saved["licenseKey"]?.GetValue<string>();
   if(Valid(lease,device)){
#if RESONE_ENCRYPTED_INSTRUCTIONS
    string? instruction=saved["instructionKey"]?.GetValue<string>();
    if(!string.IsNullOrWhiteSpace(instruction)){InstructionContent.ConfigureReleaseKey(instruction);return;}
    if(string.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Activate Resone first.");
    saved["instructionKey"]=await InstructionKeyAsync(key,device,token);Save(saved);
#endif
    return;
   }
   if(string.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Activate Resone first.");
   var result=await Exchange("renew",key,device,token);saved["lease"]=result["lease"]?.DeepClone();
   if(!Valid(saved["lease"]?.GetValue<string>(),device))throw new InvalidDataException("Invalid license signature or device binding.");
#if RESONE_ENCRYPTED_INSTRUCTIONS
   saved["instructionKey"]=await InstructionKeyAsync(key,device,token);
#endif
   Save(saved);
  }finally{gate.Release();}
 }
 public async Task<JsonObject> ActivateAsync(string key,CancellationToken token)
 {
#if RESONE_LOCAL_INSTALLER_TEST
  string localTestKey=ReadEmbeddedText("Resone.LocalTestLicenseKey").Trim();
  if(CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key.Trim()),Encoding.UTF8.GetBytes(localTestKey)))
  {
   await gate.WaitAsync(token);try{Save(new(){["licenseKey"]=localTestKey,["localTest"]=true});return new(){["message"]="Resone local installer test activation succeeded."};}finally{gate.Release();}
  }
#endif
  await gate.WaitAsync(token);try{using var device=Device();var result=await Exchange("activate",key,device,token);string? lease=result["lease"]?.GetValue<string>();if(!Valid(lease,device))throw new InvalidDataException("Invalid license signature or device binding.");var saved=new JsonObject{["licenseKey"]=key,["lease"]=lease};
#if RESONE_ENCRYPTED_INSTRUCTIONS
   saved["instructionKey"]=await InstructionKeyAsync(key,device,token);
#endif
   Save(saved);return new(){["message"]="Resone activated on this device.",["expiresAt"]=result["expiresAt"]?.DeepClone()};}finally{gate.Release();}
 }
 public async Task<JsonObject> ReleaseAsync(CancellationToken token)
 {
  await gate.WaitAsync(token);try{using var device=Device();var saved=Load();var result=await Exchange("release",saved["licenseKey"]!.GetValue<string>(),device,token);File.Delete(file);InstructionContent.ClearReleaseKey();return result;}finally{gate.Release();}
 }
 private async Task<JsonObject> Exchange(string action,string key,ECDsa device,CancellationToken token)
 {
  var baseUrl=config["url"]?.GetValue<string>()??throw new InvalidDataException("Configure the licensing URL.");if(!Uri.TryCreate(baseUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new InvalidDataException("License API must use HTTPS.");
  async Task<JsonObject> Post(string path,JsonObject body){using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(15));using var content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");using var r=await http.PostAsync(baseUrl.TrimEnd('/')+path,content,timeout.Token);var text=await r.Content.ReadAsStringAsync(timeout.Token);if(!r.IsSuccessStatusCode)throw new InvalidOperationException("License service: "+(JsonNode.Parse(text)?["error"]?.GetValue<string>()??r.StatusCode.ToString()));return JsonNode.Parse(text)!.AsObject();}
  var challenge=await Post("/v1/challenge",new(){["action"]=action,["licenseKey"]=key,["devicePublicKey"]=Public(device)});string value=challenge["challenge"]!.GetValue<string>();
  return await Post("/v1/"+action,new(){["challenge"]=value,["licenseKey"]=key,["devicePublicKey"]=Public(device),["signature"]=Encode(device.SignData(Encoding.UTF8.GetBytes(value),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))});
 }

#if RESONE_LOCAL_INSTALLER_TEST
 private static string ReadEmbeddedText(string name)
 {
  using var stream=typeof(LicenseService).Assembly.GetManifestResourceStream(name)??throw new InvalidDataException("Local installer test resource is missing: "+name);
  using var reader=new StreamReader(stream,Encoding.UTF8);
  return reader.ReadToEnd();
 }
#endif
 [StructLayout(LayoutKind.Sequential)] private struct Blob{public int Size;public nint Data;}
 [DllImport("crypt32",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input,nint description,nint entropy,nint reserved,nint prompt,int flags,out Blob output);
 [DllImport("crypt32",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input,nint description,nint entropy,nint reserved,nint prompt,int flags,out Blob output);
 [DllImport("kernel32")] private static extern nint LocalFree(nint value);
 private static byte[] Protect(byte[] bytes,bool encrypt){var input=new Blob{Size=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};try{Marshal.Copy(bytes,0,input.Data,bytes.Length);Blob output;bool ok=encrypt?CryptProtectData(ref input,0,0,0,0,1,out output):CryptUnprotectData(ref input,0,0,0,0,1,out output);if(!ok)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());try{var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;}finally{LocalFree(output.Data);}}finally{Marshal.FreeHGlobal(input.Data);}}
}
