using BillaresPos.Domain.Sales;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Billiards;

public sealed record BilliardSaleInfo(string TableName, DateTimeOffset StartedAtUtc, DateTimeOffset? EndedAtUtc);

public static class BilliardSaleLookup
{
    private const string SessionPrefix = "Billiard session ";

    public static async Task<BilliardSaleInfo?> FindAsync(AppDbContext db, Sale sale, CancellationToken ct)
    {
        if (sale.Notes?.StartsWith(SessionPrefix, StringComparison.Ordinal) != true ||
            !Guid.TryParse(sale.Notes[SessionPrefix.Length..], out var sessionId))
            return null;

        return await db.BilliardSessions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == sessionId && x.TenantId == sale.TenantId)
            .Join(db.BilliardTables.IgnoreQueryFilters().AsNoTracking(), session => session.BilliardTableId, table => table.Id,
                (session, table) => new BilliardSaleInfo(table.Name, session.StartedAtUtc, session.EndedAtUtc))
            .SingleOrDefaultAsync(ct);
    }
}
