using System.Net;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

public sealed class CloudflareRequestGate : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset retryAt;
    private int failures;

    public async Task<HttpResponseMessage> SendAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var delay = retryAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var response = await send(timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                failures = Math.Min(failures + 1, 6);
                var backoff = TimeSpan.FromSeconds(Math.Min(300, 5 * Math.Pow(2, failures - 1)))
                    + TimeSpan.FromMilliseconds(System.Security.Cryptography.RandomNumberGenerator.GetInt32(5000));
                var requested = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                    ?? TimeSpan.Zero;
                retryAt = DateTimeOffset.UtcNow + (requested > backoff ? requested : backoff);
            }
            else if (response.IsSuccessStatusCode)
            {
                failures = 0;
            }

            return response;
        }
        finally
        {
            _ = gate.Release();
        }
    }

    public void Dispose()
    {
        gate.Dispose();
    }
}
