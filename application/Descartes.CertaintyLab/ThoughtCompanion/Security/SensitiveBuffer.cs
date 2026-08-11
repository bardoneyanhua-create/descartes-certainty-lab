using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Descartes.CertaintyLab.ThoughtCompanion.Security;

public sealed class SensitiveBuffer : IDisposable
{
    private readonly char[] characters;
    private readonly byte[] bytes;
    private int disposed;

    private SensitiveBuffer(char[] characters, byte[] bytes)
    {
        this.characters = characters;
        this.bytes = bytes;
    }

    public bool IsCleared => Volatile.Read(ref disposed) != 0;

    public int Length => characters.Length != 0 ? characters.Length : bytes.Length;

    public ReadOnlySpan<char> Span
    {
        get
        {
            ThrowIfDisposed();
            return characters;
        }
    }

    public ReadOnlySpan<byte> Bytes
    {
        get
        {
            ThrowIfDisposed();
            return bytes;
        }
    }

    public static SensitiveBuffer CopyFrom(ReadOnlySpan<char> source) =>
        new(source.ToArray(), []);

    public static SensitiveBuffer CopyFromBytes(ReadOnlySpan<byte> source) =>
        new([], source.ToArray());

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
        CryptographicOperations.ZeroMemory(bytes);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsCleared, this);
    }
}
