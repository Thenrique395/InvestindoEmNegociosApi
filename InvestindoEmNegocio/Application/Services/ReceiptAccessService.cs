using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class ReceiptAccessService(IInvestDbContext db, ICurrentSpaceAccessor space, IReceiptStorageService storage) : IReceiptAccessService
{
    public async Task<ReceiptFile?> OpenAsync(Guid userId, string fileName, CancellationToken cancellationToken = default)
    {
        if (space.SpaceId is not Guid spaceId) return null;
        var suffix = "/" + fileName;
        var owned = await db.MoneyPayments.AnyAsync(p => p.UserId == userId && p.SpaceId == spaceId && p.ReceiptUrl != null && p.ReceiptUrl.EndsWith(suffix), cancellationToken)
            || await db.LoanPayments.AnyAsync(p => p.UserId == userId && p.SpaceId == spaceId && p.ReceiptUrl != null && p.ReceiptUrl.EndsWith(suffix), cancellationToken);
        return owned ? await storage.OpenAsync(userId, fileName, cancellationToken) : null;
    }
}
