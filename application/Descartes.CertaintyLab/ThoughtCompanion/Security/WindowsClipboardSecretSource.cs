using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Descartes.CertaintyLab.ThoughtCompanion.Security;

public interface IClipboardSecretSource
{
    SensitiveBuffer ReadOnce();
}

internal interface IClipboardNativeApi
{
    char[] ReadUnicodeTextSnapshot();
}

public sealed class WindowsClipboardSecretSource : IClipboardSecretSource
{
    private readonly IClipboardNativeApi native;

    public WindowsClipboardSecretSource()
        : this(new WindowsClipboardNativeApi())
    {
    }

    internal WindowsClipboardSecretSource(IClipboardNativeApi native)
    {
        this.native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public SensitiveBuffer ReadOnce()
    {
        char[] snapshot = native.ReadUnicodeTextSnapshot();
        try
        {
            return SensitiveBuffer.CopyFrom(snapshot);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(snapshot.AsSpan()));
        }
    }
}

internal sealed class WindowsClipboardNativeApi : IClipboardNativeApi
{
    private const uint UnicodeTextFormat = 13;
    private const int MaximumSnapshotCharacters = 513;

    public char[] ReadUnicodeTextSnapshot()
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            throw NativeFailure("无法打开剪贴板。");
        }

        try
        {
            IntPtr handle = GetClipboardData(UnicodeTextFormat);
            if (handle == IntPtr.Zero)
            {
                throw NativeFailure("剪贴板中没有 Unicode 文本。");
            }

            IntPtr pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                throw NativeFailure("无法读取剪贴板文本。");
            }

            try
            {
                nuint byteCount = GlobalSize(handle);
                int availableCharacters = checked((int)Math.Min(
                    byteCount / (nuint)sizeof(char),
                    (nuint)MaximumSnapshotCharacters));
                return CopySnapshot(
                    availableCharacters,
                    index => (char)Marshal.ReadInt16(pointer, index * sizeof(char)),
                    length => new char[length],
                    (source, length) => source.AsSpan(0, length).ToArray());
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    internal static char[] CopySnapshot(
        int availableCharacters,
        Func<int, char> readCharacter,
        Func<int, char[]> allocateSnapshot,
        Func<char[], int, char[]> copyResult)
    {
        char[] snapshot = allocateSnapshot(availableCharacters);
        bool ownershipTransferred = false;
        try
        {
            int length = 0;
            while (length < availableCharacters)
            {
                char character = readCharacter(length);
                if (character == '\0')
                {
                    break;
                }

                snapshot[length++] = character;
            }

            if (length == availableCharacters && availableCharacters < MaximumSnapshotCharacters)
            {
                throw new InvalidOperationException("剪贴板 Unicode 文本没有终止符。");
            }

            if (length == snapshot.Length)
            {
                ownershipTransferred = true;
                return snapshot;
            }

            char[] result = copyResult(snapshot, length);
            ownershipTransferred = ReferenceEquals(result, snapshot);
            return result;
        }
        finally
        {
            if (!ownershipTransferred)
            {
                CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(snapshot.AsSpan()));
            }
        }
    }

    private static Win32Exception NativeFailure(string message) =>
        new(Marshal.GetLastWin32Error(), message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint GlobalSize(IntPtr memory);
}
