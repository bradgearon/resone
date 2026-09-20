using System.Security.Cryptography;
using System.Text.Json;
namespace Wds.Resone.Api;
public static class InstructionContent
{
#if RESONE_ENCRYPTED_INSTRUCTIONS
    private static readonly object Gate=new();
    private static JsonDocument? bundle;
    private static byte[]? releaseKey;
    public static void ConfigureReleaseKey(string base64)
    {
        byte[] key=Convert.FromBase64String(base64);
        if(key.Length!=32){CryptographicOperations.ZeroMemory(key);throw new InvalidDataException("Invalid production instruction key length.");}
        lock(Gate)
        {
            if(releaseKey is not null)CryptographicOperations.ZeroMemory(releaseKey);
            releaseKey=key;
            bundle?.Dispose();bundle=null;
        }
    }
    public static void ClearReleaseKey()
    {
        lock(Gate){if(releaseKey is not null)CryptographicOperations.ZeroMemory(releaseKey);releaseKey=null;bundle?.Dispose();bundle=null;}
    }
    private static JsonDocument Bundle()
    {
        lock(Gate)
        {
            if(bundle is not null)return bundle;
            if(releaseKey is null)throw new InvalidOperationException("Resone must validate this device before protected LLM instructions can be read.");
            byte[] data=Convert.FromBase64String(PackedInstructions.Data),plain=new byte[data.Length];
            using var aes=new AesGcm(releaseKey,16);
            aes.Decrypt(Convert.FromBase64String(PackedInstructions.Nonce),data,Convert.FromBase64String(PackedInstructions.Tag),plain);
            try{return bundle=JsonDocument.Parse(plain);}finally{CryptographicOperations.ZeroMemory(plain);}
        }
    }
#else
    public static void ConfigureReleaseKey(string base64) { }
    public static void ClearReleaseKey() { }
#endif
    public static string Read(string root,string name)
    {
#if RESONE_ENCRYPTED_INSTRUCTIONS
        return Bundle().RootElement.GetProperty(name).GetString()!;
#else
        return File.ReadAllText(Path.Combine(root,"Instructions","Music",name));
#endif
    }
}
