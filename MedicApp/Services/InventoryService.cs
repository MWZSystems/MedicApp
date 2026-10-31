using System.ComponentModel.DataAnnotations;
using System.Data;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Services;

public class InventoryService(MedicDbContext db, MedicalAccess access, MedicalService medical, MedicalClock clock)
{
    public async Task<int> CreateAsync(StockItemForm input, CancellationToken ct)
    {
        if (!await access.CanUseInventoryAsync(ct)) { throw new MedicalRuleException("Forbidden"); }
        if (!Validator.TryValidateObject(input, new ValidationContext(input), [], true) || decimal.Round(input.MinimumStock, 3) != input.MinimumStock) { throw new MedicalRuleException("InvalidStockInput"); }
        var code = input.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Unit)) { throw new MedicalRuleException("InvalidStockInput"); }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.StockCatalog", ct);
        if (await db.StockItems.AnyAsync(x => x.Code == code, ct)) { throw new MedicalRuleException("Duplicate"); }
        var item = new StockItem { Code = code, Name = input.Name.Trim(), Unit = input.Unit.Trim(), Lot = input.Lot?.Trim() ?? "", ExpiresOn = input.ExpiresOn, MinimumStock = input.MinimumStock };
        medical.Stamp(item); db.StockItems.Add(item); await db.SaveChangesAsync(ct);
        medical.Audit("StockItem", item.Id, "Create"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return item.Id;
    }
    public async Task MoveAsync(StockMovementForm input, CancellationToken ct)
    {
        if (!await access.CanUseInventoryAsync(ct)) { throw new MedicalRuleException("Forbidden"); }
        if (!Validator.TryValidateObject(input, new ValidationContext(input), [], true) || input.RequestKey == Guid.Empty || string.IsNullOrWhiteSpace(input.Reason) || decimal.Round(input.Quantity, 3) != input.Quantity) { throw new MedicalRuleException("InvalidStockInput"); }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // A single lock also serializes duplicate request keys submitted for different items.
        await MedicalLocks.AcquireAsync(db, "MedicApp.StockMovements", ct);
        var existing = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == input.RequestKey, ct);
        if (existing != null)
        {
            if (existing.UpdatedById != access.UserId || existing.StockItemId != input.StockItemId || existing.Kind != input.Kind || existing.Quantity != input.Quantity || existing.Reason != input.Reason.Trim()) { throw new MedicalRuleException("Concurrency"); }
            return;
        }
        var item = await db.StockItems.SingleOrDefaultAsync(x => x.Id == input.StockItemId && x.Available, ct) ?? throw new MedicalRuleException("NotFound");
        if (input.Kind == StockMovementKind.Exit && item.ExpiresOn != null && item.ExpiresOn < DateOnly.FromDateTime(clock.Local(clock.UtcNow))) { throw new MedicalRuleException("StockExpired"); }
        var balance = item.CurrentStock + (input.Kind == StockMovementKind.Entry ? input.Quantity : -input.Quantity);
        if (balance < 0) { throw new MedicalRuleException("StockInsufficient"); }
        if (balance > 999999999.999m) { throw new MedicalRuleException("InvalidStockInput"); }
        item.CurrentStock = balance; medical.Stamp(item);
        var movement = new StockMovement { StockItemId = item.Id, Kind = input.Kind, Quantity = input.Quantity, Balance = balance, Reason = input.Reason.Trim(), RequestKey = input.RequestKey };
        medical.Stamp(movement); db.StockMovements.Add(movement); await db.SaveChangesAsync(ct);
        medical.Audit("StockMovement", movement.Id, input.Kind.ToString()); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
