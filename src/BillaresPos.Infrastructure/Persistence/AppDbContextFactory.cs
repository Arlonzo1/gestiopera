using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BillaresPos.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=billares_pos;Username=billares_pos;Password=billares_pos_dev")
            .Options;

        return new AppDbContext(options, new DesignTimeCurrentTenant(), new DesignTimeCurrentUser(), new SystemClock());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
        public bool IsInRole(string role) => false;
    }

    private sealed class DesignTimeCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
        public string? Slug => null;
        public string TimeZoneId => "America/Bogota";
        public string Currency => "COP";
        public bool IsResolved => false;
    }
}
