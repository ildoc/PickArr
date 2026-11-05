using PickArr.Core.Abstractions;

namespace PickArr.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
