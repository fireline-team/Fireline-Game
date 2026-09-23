using Game339.Shared.Infrastructure.Diagnostics;

namespace Game339.Tests.Infrastructure;

public class EmptyGameLog : IGameLog
{
    public static IGameLog Instance { get; } = new EmptyGameLog();

    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}
