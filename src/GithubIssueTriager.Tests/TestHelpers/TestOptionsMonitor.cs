using Microsoft.Extensions.Options;

namespace GithubIssueTriager.Tests.TestHelpers;

/// <summary>Minimal IOptionsMonitor&lt;T&gt; test double — just enough for services
/// that read .CurrentValue; tests don't need live-reload behaviour.</summary>
public class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
