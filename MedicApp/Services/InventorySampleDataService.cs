using System.Data;
using System.Security.Cryptography;
using System.Text;
using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Trainify.Data;
namespace MedicApp.Services;

public record InventorySampleResult(bool Created, int Items, int Movements);

public sealed class InventorySampleDataService(IConfiguration configuration, MedicalClock clock)
{
    public const string Prefix = "MEDIC-DEMO-STOCK-";
    public const string Batch = "MEDIC-DEMO-STOCK-V1";
    private sealed record Sample(string Code, string Name, string Unit, decimal Entry, decimal Exit, decimal Loss, decimal Minimum, int? ExpiryDays);
    private static readonly Sample[] Samples = [
        new("JER003", "Jeringa desechable 3 ml", "pieza", 150, 35, 5, 30, 540),
        new("JER005", "Jeringa desechable 5 ml", "pieza", 80, 60, 5, 20, 540),
        new("GUA", "Guantes de exploración", "caja", 25, 6, 1, 5, 365),
        new("GAS", "Gasas estériles", "paquete", 60, 15, 2, 10, 180),
        new("CUB", "Cubrebocas desechable", "caja", 20, 8, 1, 4, 365),
        new("ALC", "Alcohol antiséptico", "frasco", 30, 10, 2, 5, 20),
        new("VEN", "Venda elástica", "pieza", 40, 12, 2, 10, null),
        new("TER", "Termómetro digital", "pieza", 10, 3, 1, 2, null),
        new("OXI", "Oxímetro de pulso", "pieza", 5, 2, 1, 2, null),
        new("PAR", "Paracetamol", "caja", 40, 12, 2, 10, 300),
        new("IBU", "Ibuprofeno", "caja", 30, 22, 2, 8, 240),
        new("LOR", "Loratadina", "caja", 20, 15, 5, 5, 180),
        new("ANT", "Antiácido", "frasco", 18, 5, 1, 4, -10),
        new("SUE", "Solución salina", "frasco", 25, 10, 1, 5, 15),
        new("CUR", "Apósitos adhesivos", "caja", 35, 10, 2, 6, 365)
    ];
    public async Task<InventorySampleResult> CreateAsync(string actorName, CancellationToken ct)
    {
        await using var connection = new SqlConnection(DatabaseConfiguration.Resolve(configuration));
        await using var db = new MedicDbContext(new DbContextOptionsBuilder<MedicDbContext>().UseSqlServer(connection).Options);
        await using var shared = new TrainifyDbContext(new DbContextOptionsBuilder<TrainifyDbContext>().UseSqlServer(connection).Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await shared.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.StockCatalog", ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.StockMovements", ct);
        var actor = await shared.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == actorName && x.Active && x.Person.Active, ct) ?? throw new MedicalRuleException("Forbidden");
        if (!await db.AccessProfiles.AnyAsync(x => x.UserId == actor.Id && x.Available && x.ManageAccess, ct)) { throw new MedicalRuleException("Forbidden"); }
        if (await db.Audit.AnyAsync(x => x.EntityType == "InventorySampleBatch" && x.Reason == Batch, ct))
        {
            return new(false, await db.Audit.CountAsync(x => x.EntityType == "InventorySampleItem" && x.Reason == Batch, ct), await db.Audit.CountAsync(x => x.EntityType == "InventorySampleMovement" && x.Reason == Batch, ct));
        }
        // Never adopt, reset or overwrite a pre-existing item with a reserved sample code.
        if (await db.StockItems.AnyAsync(x => x.Code.StartsWith(Prefix), ct)) { throw new MedicalRuleException("Concurrency"); }
        var today = DateOnly.FromDateTime(clock.Local(clock.UtcNow));
        var items = new List<StockItem>(); var movements = new List<StockMovement>();
        foreach (var sample in Samples)
        {
            var item = new StockItem { Code = Prefix + sample.Code, Name = "DEMO · " + sample.Name, Unit = sample.Unit, Lot = "DEMO-LOTE-" + sample.Code, ExpiresOn = sample.ExpiryDays is int days ? today.AddDays(days) : null, MinimumStock = sample.Minimum, CurrentStock = sample.Entry - sample.Exit - sample.Loss, UpdatedById = actor.Id, LastUpdated = clock.UtcNow };
            db.StockItems.Add(item); await db.SaveChangesAsync(ct); items.Add(item);
            decimal balance = 0;
            foreach (var (kind, quantity) in new[] { (StockMovementKind.Entry, sample.Entry), (StockMovementKind.Exit, sample.Exit), (StockMovementKind.Loss, sample.Loss) })
            {
                balance += kind == StockMovementKind.Entry ? quantity : -quantity;
                var key = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(Batch + item.Code + kind.ToString())).AsSpan(0, 16));
                var movement = new StockMovement { StockItemId = item.Id, Kind = kind, Quantity = quantity, Balance = balance, Reason = "DEMO · Movimiento ficticio para demostración; no representa recepción, entrega ni consumo real.", RequestKey = key, UpdatedById = actor.Id, LastUpdated = clock.UtcNow };
                db.StockMovements.Add(movement); movements.Add(movement);
            }
        }
        await db.SaveChangesAsync(ct);
        void Audit(string type, int id) => db.Audit.Add(new AuditEntry { EntityType = type, EntityId = id, Operation = "Create", Reason = Batch, UpdatedById = actor.Id, LastUpdated = clock.UtcNow });
        foreach (var item in items) { Audit("InventorySampleItem", item.Id); }
        foreach (var movement in movements) { Audit("InventorySampleMovement", movement.Id); }
        Audit("InventorySampleBatch", items[0].Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, items.Count, movements.Count);
    }
}
