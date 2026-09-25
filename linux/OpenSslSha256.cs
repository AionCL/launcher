using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AionCL {
// EVP uses OpenSSL's CPU-accelerated implementation without shell processes.
public sealed class OpenSslSha256 : SHA256 {
    const string Library="libcrypto.so.3";
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr EVP_MD_CTX_new();
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void EVP_MD_CTX_free(IntPtr context);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr EVP_sha256();
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int EVP_DigestInit_ex(IntPtr context,IntPtr type,IntPtr engine);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int EVP_DigestUpdate(IntPtr context,IntPtr data,UIntPtr length);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int EVP_DigestFinal_ex(IntPtr context,byte[] output,out uint length);
    IntPtr context;
    public OpenSslSha256() {
        HashSizeValue=256; context=EVP_MD_CTX_new();
        if(context==IntPtr.Zero)throw new CryptographicException("Cannot allocate SHA-256 context.");
        try { Initialize(); } catch { Dispose(); throw; }
    }
    public override void Initialize() {
        if(context==IntPtr.Zero)throw new ObjectDisposedException("OpenSslSha256");
        if(EVP_DigestInit_ex(context,EVP_sha256(),IntPtr.Zero)!=1)throw new CryptographicException("SHA-256 initialization failed.");
    }
    protected override void HashCore(byte[] array,int offset,int count) {
        if(context==IntPtr.Zero)throw new ObjectDisposedException("OpenSslSha256");
        if(count==0)return;
        var pinned=GCHandle.Alloc(array,GCHandleType.Pinned);
        try { if(EVP_DigestUpdate(context,IntPtr.Add(pinned.AddrOfPinnedObject(),offset),(UIntPtr)(uint)count)!=1)throw new CryptographicException("SHA-256 update failed."); }
        finally { pinned.Free(); }
    }
    protected override byte[] HashFinal() {
        var result=new byte[32]; uint length;
        if(EVP_DigestFinal_ex(context,result,out length)!=1||length!=32)throw new CryptographicException("SHA-256 finalization failed.");
        return result;
    }
    protected override void Dispose(bool disposing) {
        if(context!=IntPtr.Zero){EVP_MD_CTX_free(context);context=IntPtr.Zero;}
        base.Dispose(disposing);
    }
    ~OpenSslSha256() { Dispose(false); }
}
}
