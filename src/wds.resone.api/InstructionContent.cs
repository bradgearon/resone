using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Wds.Resone.Api;
public static class InstructionContent
{
#if RESONE_CUSTOMER_RELEASE
    private static readonly Lazy<JsonDocument> Bundle=new(()=>{
        byte[] data=Convert.FromBase64String(PackedInstructions.Data),plain=new byte[data.Length];
        using var aes=new AesGcm(Convert.FromBase64String(PackedInstructions.Key),16);
        aes.Decrypt(Convert.FromBase64String(PackedInstructions.Nonce),data,Convert.FromBase64String(PackedInstructions.Tag),plain);
        try{return JsonDocument.Parse(Encoding.UTF8.GetString(plain));}finally{CryptographicOperations.ZeroMemory(plain);}
    });
#endif
    public static string Read(string root,string name)
    {
#if RESONE_CUSTOMER_RELEASE
        return Bundle.Value.RootElement.GetProperty(name).GetString()!;
#else
        return File.ReadAllText(Path.Combine(root,"Instructions","Music",name));
#endif
    }
}
