using System.Net.Http;
using Descartes.CertaintyLab.ThoughtCompanion.DeepSeek;
using Descartes.CertaintyLab.ThoughtCompanion.Security;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public sealed class ThoughtCompanionServices : IDisposable
{
    private HttpClient? ownedHttpClient;

    internal ThoughtCompanionServices(
        CompanionContextBuilder contextBuilder,
        CompanionResponseValidator validator,
        IThoughtCompanionProvider provider,
        ICredentialStore credentials,
        CompanionRequestCoordinator coordinator,
        HttpClient? ownedHttpClient)
    {
        ContextBuilder = contextBuilder;
        Validator = validator;
        Provider = provider;
        Credentials = credentials;
        Coordinator = coordinator;
        this.ownedHttpClient = ownedHttpClient;
    }

    public CompanionContextBuilder ContextBuilder { get; }
    public CompanionResponseValidator Validator { get; }
    public IThoughtCompanionProvider Provider { get; }
    public ICredentialStore Credentials { get; }
    public CompanionRequestCoordinator Coordinator { get; }

    public void Dispose() => Interlocked.Exchange(ref ownedHttpClient, null)?.Dispose();
}

public static class ThoughtCompanionComposition
{
    public static ICompanionServiceFactory CreateServiceFactory(
        HttpClient client,
        ICredentialStore credentials,
        CompanionBudgetOptions budgetOptions,
        ICompanionAuditSink audit,
        TimeProvider timeProvider,
        TimeSpan timeout,
        int maximumOutputTokens) =>
        new CompanionServiceFactory(
            client,
            credentials,
            budgetOptions,
            audit,
            timeProvider,
            timeout,
            maximumOutputTokens);

    /// <summary>
    /// Creates services with a factory-owned HTTP client that is released when the services are disposed.
    /// </summary>
    public static ThoughtCompanionServices Create(
        DeepSeekOptions options,
        CompanionBudgetOptions budgetOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(budgetOptions);

        var client = new HttpClient();
        return Create(client, options, budgetOptions, ownsHttpClient: true);
    }

    /// <summary>
    /// Creates services with a caller-owned HTTP client. Disposing the services does not dispose the client.
    /// </summary>
    public static ThoughtCompanionServices Create(
        HttpClient client,
        DeepSeekOptions options,
        CompanionBudgetOptions budgetOptions) =>
        Create(client, options, budgetOptions, ownsHttpClient: false);

    internal static ThoughtCompanionServices Create(
        HttpClient client,
        DeepSeekOptions options,
        CompanionBudgetOptions budgetOptions,
        bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(budgetOptions);

        try
        {
            return CreateCore(client, options, budgetOptions, ownsHttpClient);
        }
        catch
        {
            if (ownsHttpClient)
            {
                client.Dispose();
            }

            throw;
        }
    }

    private static ThoughtCompanionServices CreateCore(
        HttpClient client,
        DeepSeekOptions options,
        CompanionBudgetOptions budgetOptions,
        bool ownsHttpClient)
    {
        var builder = new CompanionContextBuilder(3);
        var validator = new CompanionResponseValidator();
        var credentials = new WindowsCredentialStore();
        var provider = new DeepSeekThoughtCompanionProvider(client, options);
        var budget = new InMemoryCompanionBudget(budgetOptions);
        var audit = new FileCompanionAuditSink();
        var coordinator = new CompanionRequestCoordinator(
            builder,
            validator,
            provider,
            credentials,
            budget,
            audit,
            TimeProvider.System);

        return new(
            builder,
            validator,
            provider,
            credentials,
            coordinator,
            ownsHttpClient ? client : null);
    }
}
