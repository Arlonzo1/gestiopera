using BillaresPos.Application.Common.Abstractions;

namespace BillaresPos.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
