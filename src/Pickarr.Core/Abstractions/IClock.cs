namespace Pickarr.Core.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
