using Pickarr.Core.Abstractions;

namespace Pickarr.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
