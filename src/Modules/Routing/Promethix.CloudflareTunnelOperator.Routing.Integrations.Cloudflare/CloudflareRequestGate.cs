using System.Net;
using System.Globalization;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

public sealed class CloudflareRequestGate : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeProvider clock;
    private readonly Func<int> jitter;

    private int failures;
    private long attempts;
    private long rateLimits;
    private long transientFailures;

    public CloudflareRequestGate() : this(TimeProvider.System, () => System.Security.Cryptography.RandomNumberGenerator.GetInt32(5000)) { }

    public CloudflareRequestGate(TimeProvider clock, Func<int> jitter)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(jitter);
        this.clock = clock;
        this.jitter = jitter;
    }

    public DateTimeOffset RetryAt { get; private set; }
    public long Attempts => Interlocked.Read(ref attempts);
    public long RateLimits => Interlocked.Read(ref rateLimits);
    public long TransientFailures => Interlocked.Read(ref transientFailures);

    public async Task<HttpResponseMessage> SendAsync(Func<CancellationToken, Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var delay = RetryAt - clock.GetUtcNow();
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            _ = Interlocked.Increment(ref attempts);
            HttpResponseMessage response;
            try
            {
                response = await send(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                _ = Interlocked.Increment(ref transientFailures);
                SetCooldown(TimeSpan.Zero);
                throw;
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                _ = response.StatusCode == HttpStatusCode.TooManyRequests ? Interlocked.Increment(ref rateLimits) : Interlocked.Increment(ref transientFailures);
                var requested = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - clock.GetUtcNow()) ?? TimeSpan.Zero;
                // Legacy Cloudflare reset header is an absolute Unix timestamp.
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resets)
                    && long.TryParse(resets.FirstOrDefault(), CultureInfo.InvariantCulture, out var reset)
                    && reset is >= 0 and <= 253402300799)
                {
                    var resetDelay = DateTimeOffset.FromUnixTimeSeconds(reset) - clock.GetUtcNow();
                    requested = resetDelay > requested ? resetDelay : requested;
                }
                SetCooldown(requested);
            }
            else if (response.IsSuccessStatusCode) { failures = 0; }
            return response;
        }
        finally { _ = gate.Release(); }
    }

    private void SetCooldown(TimeSpan requested)
    {
        failures = Math.Min(failures + 1, 7);
        var fallback = TimeSpan.FromSeconds(Math.Min(300, 5 * Math.Pow(2, failures - 1)))
            + TimeSpan.FromMilliseconds(Math.Clamp(jitter(), 0, 4999));
        RetryAt = clock.GetUtcNow() + (requested > fallback ? requested : fallback);
    }

    public void Dispose() { gate.Dispose(); }
}
