using System.Net;
using System.Net.Http.Headers;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class CloudflareCooldownTests
{
    [Fact]
    public async Task FailuresHaveOneAttemptPerCallAndBoundedBackoffThenRecover()
    {
        var clock = new ManualClock();
        using var gate = new CloudflareRequestGate(clock, () => 4999);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await gate.SendAsync(token => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)), CancellationToken.None);
            var delay = gate.RetryAt - clock.GetUtcNow();
            Assert.InRange(delay.TotalSeconds, 5, 304.999);
            clock.Now = gate.RetryAt;
        }
        Assert.Equal(10, gate.Attempts);
        using var recovered = await gate.SendAsync(token => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)), CancellationToken.None);
        using var failed = await gate.SendAsync(token => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)), CancellationToken.None);
        Assert.Equal(TimeSpan.FromMilliseconds(9999), gate.RetryAt - clock.GetUtcNow());
    }

    [Fact]
    public async Task RetryAfterDateAndResetHeaderTakePrecedence()
    {
        var clock = new ManualClock();
        using var gate = new CloudflareRequestGate(clock, () => 0);
        using var response = await gate.SendAsync(token =>
        {
            var result = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            result.Headers.RetryAfter = new RetryConditionHeaderValue(clock.Now.AddMinutes(2));
            _ = result.Headers.TryAddWithoutValidation("X-RateLimit-Reset", clock.Now.AddMinutes(3).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Task.FromResult(result);
        }, CancellationToken.None);
        Assert.Equal(clock.Now.AddMinutes(3), gate.RetryAt);
    }

    [Fact]
    public async Task NetworkFailureSetsCooldownAndPropagatesWithoutReplay()
    {
        var clock = new ManualClock();
        using var gate = new CloudflareRequestGate(clock, () => 0);
        _ = await Assert.ThrowsAsync<HttpRequestException>(() => gate.SendAsync(_ => throw new HttpRequestException("offline"), CancellationToken.None));
        Assert.Equal(1, gate.Attempts);
        Assert.Equal(clock.Now.AddSeconds(5), gate.RetryAt);
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1800000000);
        public override DateTimeOffset GetUtcNow() { return Now; }
    }
}
