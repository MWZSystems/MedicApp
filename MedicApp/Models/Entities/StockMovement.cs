namespace MedicApp.Models.Entities;

public enum StockMovementKind { Entry, Exit, Loss }
public class StockMovement : MedicalRecord
{
    public int StockItemId { get; set; }
    public StockMovementKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public decimal Balance { get; set; }
    public string Reason { get; set; } = "";
    public Guid RequestKey { get; set; }
}
