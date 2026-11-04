using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace MedicApp.Tests;

[CollectionDefinition("InventorySamplesSql", DisableParallelization = true)]
public class InventorySampleGroup : ICollectionFixture<SqlFixture>;
[Collection("InventorySamplesSql")]
public class InventorySampleTests(SqlFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private InventorySampleDataService Seeder() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection }).Build(), SqlFixture.Clock());
    [Fact]
    public async Task CreatesAuditedInventoryAndPreservesExistingStockOnRepeat()
    {
        await using var db = fixture.Medical();
        var real = new StockItem { Code = "EXISTING-INVENTORY", Name = "Existing item", Unit = "piece", Lot = "REAL", CurrentStock = 7, MinimumStock = 2, UpdatedById = "doctor" };
        db.StockItems.Add(real); await db.SaveChangesAsync(Ct); var originalVersion = real.RowVersion.ToArray();
        var labor = await db.LaborEvents.CountAsync(Ct); var visits = await db.ClinicalVisits.CountAsync(Ct);
        var result = await Seeder().CreateAsync("doctor", Ct); Assert.True(result.Created); Assert.Equal(15, result.Items); Assert.Equal(45, result.Movements);
        var items = await db.StockItems.AsNoTracking().Where(x => x.Code.StartsWith(InventorySampleDataService.Prefix)).ToListAsync(Ct);
        Assert.Equal(15, items.Count); Assert.All(items, x => { Assert.StartsWith("DEMO", x.Name); Assert.StartsWith("DEMO", x.Lot); });
        var ids = items.Select(x => x.Id).ToArray(); var movements = await db.StockMovements.Where(x => ids.Contains(x.StockItemId)).OrderBy(x => x.Id).ToListAsync(Ct);
        foreach (var item in items)
        {
            var history = movements.Where(x => x.StockItemId == item.Id).ToList(); Assert.Equal(3, history.Count); decimal balance = 0;
            foreach (var move in history) { balance += move.Kind == StockMovementKind.Entry ? move.Quantity : -move.Quantity; Assert.Equal(balance, move.Balance); Assert.Contains("DEMO", move.Reason); }
            Assert.Equal(balance, item.CurrentStock);
        }
        var today = DateOnly.FromDateTime(SqlFixture.Clock().Local(SqlFixture.Clock().UtcNow));
        Assert.Contains(items, x => x.CurrentStock == 0); Assert.Contains(items, x => x.CurrentStock > 0 && x.CurrentStock <= x.MinimumStock);
        Assert.Contains(items, x => x.ExpiresOn < today); Assert.Contains(items, x => x.ExpiresOn >= today && x.ExpiresOn <= today.AddDays(30));
        // A repeat must not reset even a demonstration balance that has since been used.
        var used = await db.StockItems.SingleAsync(x => x.Id == items[0].Id, Ct); used.CurrentStock -= 1; await db.SaveChangesAsync(Ct);
        var again = await Seeder().CreateAsync("doctor", Ct); Assert.False(again.Created); Assert.Equal(result.Items, again.Items); Assert.Equal(result.Movements, again.Movements);
        await db.Entry(used).ReloadAsync(Ct); Assert.Equal(items[0].CurrentStock - 1, used.CurrentStock);
        await db.Entry(real).ReloadAsync(Ct); Assert.Equal(7, real.CurrentStock); Assert.Equal(originalVersion, real.RowVersion);
        Assert.Equal(45, await db.Audit.CountAsync(x => x.EntityType == "InventorySampleMovement" && x.Reason == InventorySampleDataService.Batch, Ct));
        Assert.Equal(15, await db.Audit.CountAsync(x => x.EntityType == "InventorySampleItem" && x.Reason == InventorySampleDataService.Batch, Ct));
        Assert.Equal(labor, await db.LaborEvents.CountAsync(Ct)); Assert.Equal(visits, await db.ClinicalVisits.CountAsync(Ct));
    }
    [Fact]
    public async Task RejectsNonAdministratorWithoutAnyWrites()
    {
        await using var db = fixture.Medical(); var items = await db.StockItems.CountAsync(Ct); var movements = await db.StockMovements.CountAsync(Ct); var audit = await db.Audit.CountAsync(Ct);
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => Seeder().CreateAsync("employee", Ct)); Assert.Equal("Forbidden", error.Message);
        Assert.Equal(items, await db.StockItems.CountAsync(Ct)); Assert.Equal(movements, await db.StockMovements.CountAsync(Ct)); Assert.Equal(audit, await db.Audit.CountAsync(Ct));
    }
}
