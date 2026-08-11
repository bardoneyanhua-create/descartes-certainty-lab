namespace Descartes.CertaintyLab.ThoughtCompanion.DeepSeek;

public sealed record DeepSeekOptions
{
    public DeepSeekOptions(Uri baseUrl, string model, TimeSpan timeout, int maximumOutputTokens)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        if (!baseUrl.IsAbsoluteUri ||
            !string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(baseUrl.Host, "api.deepseek.com", StringComparison.OrdinalIgnoreCase) ||
            !baseUrl.IsDefaultPort ||
            baseUrl.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(baseUrl.UserInfo) ||
            !string.IsNullOrEmpty(baseUrl.Query) ||
            !string.IsNullOrEmpty(baseUrl.Fragment))
        {
            throw new ArgumentException("DeepSeek base URL 不符合安全策略。", nameof(baseUrl));
        }

        if (string.IsNullOrWhiteSpace(model) || model.Length > 128)
        {
            throw new ArgumentException("DeepSeek 模型配置无效。", nameof(model));
        }

        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "DeepSeek 超时必须大于零且不超过两分钟。");
        }

        if (maximumOutputTokens is < 64 or > 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens), "DeepSeek 最大输出 token 必须为 64..2048。");
        }

        BaseUrl = baseUrl;
        Model = model;
        Timeout = timeout;
        MaximumOutputTokens = maximumOutputTokens;
    }

    public Uri BaseUrl { get; }
    public string Model { get; }
    public TimeSpan Timeout { get; }
    public int MaximumOutputTokens { get; }
}
