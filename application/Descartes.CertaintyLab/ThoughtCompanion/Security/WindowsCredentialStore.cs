using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Descartes.CertaintyLab.ThoughtCompanion.Security;

public interface ICredentialStore
{
    bool Exists(string targetName);
    SensitiveBuffer? Read(string targetName);
    void Write(string targetName, SensitiveBuffer value);
    bool Delete(string targetName);
}

internal interface ICredentialNativeApi
{
    bool Write(string targetName, byte[] credentialBlob);
    byte[]? Read(string targetName);
    bool Delete(string targetName);
}

public sealed class WindowsCredentialStore : ICredentialStore
{
    public const string TargetName = "PhilosophyVault/Descartes.CertaintyLab/DeepSeek";

    private readonly ICredentialNativeApi native;

    public WindowsCredentialStore()
        : this(new WindowsCredentialNativeApi())
    {
    }

    internal WindowsCredentialStore(ICredentialNativeApi native)
    {
        this.native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public bool Exists(string targetName)
    {
        using SensitiveBuffer? value = Read(targetName);
        return value is not null;
    }

    public SensitiveBuffer? Read(string targetName)
    {
        ValidateTarget(targetName);
        byte[]? nativeBytes = native.Read(targetName);
        if (nativeBytes is null)
        {
            return null;
        }

        char[]? characters = null;
        try
        {
            if (nativeBytes.Length == 0 || nativeBytes.Length % sizeof(char) != 0)
            {
                throw new InvalidOperationException("Windows 凭据包含无效数据。");
            }

            characters = Encoding.Unicode.GetChars(nativeBytes);
            return SensitiveBuffer.CopyFrom(characters);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nativeBytes);
            if (characters is not null)
            {
                CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
            }
        }
    }

    public void Write(string targetName, SensitiveBuffer value)
    {
        ValidateTarget(targetName);
        ArgumentNullException.ThrowIfNull(value);
        byte[] bytes = new byte[checked(value.Span.Length * sizeof(char))];
        try
        {
            Encoding.Unicode.GetBytes(value.Span, bytes);
            if (!native.Write(targetName, bytes))
            {
                throw new InvalidOperationException("无法保存 Windows 凭据。");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public bool Delete(string targetName)
    {
        ValidateTarget(targetName);
        return native.Delete(targetName);
    }

    private static void ValidateTarget(string targetName)
    {
        if (!string.Equals(targetName, TargetName, StringComparison.Ordinal) &&
            !CompanionCredentialTargets.IsApplicationOwnedProfileTarget(targetName))
        {
            throw new ArgumentException("只允许访问应用拥有的配置凭据目标。", nameof(targetName));
        }
    }
}

internal sealed class WindowsCredentialNativeApi : ICredentialNativeApi
{
    internal const uint CredentialTypeGeneric = 1;
    internal const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public bool Write(string targetName, byte[] credentialBlob)
    {
        ArgumentNullException.ThrowIfNull(credentialBlob);
        GCHandle pinned = default;
        try
        {
            IntPtr blobPointer = IntPtr.Zero;
            if (credentialBlob.Length != 0)
            {
                pinned = GCHandle.Alloc(credentialBlob, GCHandleType.Pinned);
                blobPointer = pinned.AddrOfPinnedObject();
            }

            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = targetName,
                CredentialBlobSize = checked((uint)credentialBlob.Length),
                CredentialBlob = blobPointer,
                Persist = CredentialPersistLocalMachine,
                UserName = Environment.UserName
            };

            if (!CredWriteW(ref credential, 0))
            {
                throw NativeFailure("无法保存 Windows 凭据。");
            }

            return true;
        }
        finally
        {
            if (pinned.IsAllocated)
            {
                pinned.Free();
            }
        }
    }

    public byte[]? Read(string targetName)
    {
        if (!CredReadW(targetName, CredentialTypeGeneric, 0, out IntPtr credentialPointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error, "无法读取 Windows 凭据。");
        }

        try
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            int length = checked((int)credential.CredentialBlobSize);
            return CopyCredentialBlob(
                length,
                bytes =>
                {
                    if (bytes.Length != 0)
                    {
                        Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                    }
                });
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    internal static byte[] CopyCredentialBlob(int length, Action<byte[]> copy)
    {
        byte[] bytes = new byte[length];
        bool ownershipTransferred = false;
        try
        {
            copy(bytes);
            ownershipTransferred = true;
            return bytes;
        }
        finally
        {
            if (!ownershipTransferred)
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    public bool Delete(string targetName)
    {
        if (CredDeleteW(targetName, CredentialTypeGeneric, 0))
        {
            return true;
        }

        int error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
        {
            return true;
        }

        throw new Win32Exception(error, "无法删除 Windows 凭据。");
    }

    private static Win32Exception NativeFailure(string message) =>
        new(Marshal.GetLastWin32Error(), message);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
