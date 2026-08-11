namespace Descartes.CertaintyLab.ThoughtCompanion.Security;

public enum CredentialImportStatus { Imported, Rejected, Cancelled }

public sealed record CredentialImportResult(CredentialImportStatus Status, string UserMessage);

public interface ICredentialProbe
{
    Task<bool> IsAcceptedAsync(SensitiveBuffer value, CancellationToken cancellationToken);
}

public sealed class DeepSeekCredentialImportService
{
    public const int MinimumCredentialCharacters = 8;
    public const int MaximumCredentialCharacters = 512;

    private readonly IClipboardSecretSource clipboard;
    private readonly ICredentialStore store;
    private readonly ICredentialProbe probe;

    public DeepSeekCredentialImportService(
        IClipboardSecretSource clipboard,
        ICredentialStore store,
        ICredentialProbe probe)
    {
        this.clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public async Task<CredentialImportResult> ImportFromClipboardAsync(CancellationToken cancellationToken)
    {
        SensitiveBuffer? value = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            value = clipboard.ReadOnce() ?? throw new InvalidOperationException("剪贴板没有返回凭据数据。");
            cancellationToken.ThrowIfCancellationRequested();

            if (!HasValidFormat(value))
            {
                return new(CredentialImportStatus.Rejected, "剪贴板内容不是可接受的凭据格式。");
            }

            if (!await probe.IsAcceptedAsync(value, cancellationToken).ConfigureAwait(false))
            {
                return new(CredentialImportStatus.Rejected, "凭据未通过最小连通性验证，未保存。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            store.Write(WindowsCredentialStore.TargetName, value);
            return new(CredentialImportStatus.Imported, "DeepSeek 凭据已保存在当前 Windows 用户的凭据管理器中。");
        }
        catch (OperationCanceledException)
        {
            return new(CredentialImportStatus.Cancelled, "导入已取消，凭据未保存。");
        }
        catch (Exception)
        {
            return new(CredentialImportStatus.Rejected, "凭据导入失败，未保存。");
        }
        finally
        {
            value?.Dispose();
        }
    }

    private static bool HasValidFormat(SensitiveBuffer value)
    {
        if (value.Length is < MinimumCredentialCharacters or > MaximumCredentialCharacters)
        {
            return false;
        }

        foreach (char character in value.Span)
        {
            if (character is < (char)33 or > (char)126)
            {
                return false;
            }
        }

        return true;
    }
}
