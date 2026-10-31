namespace MedicApp.Models.Entities;

public class StockItem : MedicalRecord
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Lot { get; set; } = "";
    public DateOnly? ExpiresOn { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal CurrentStock { get; set; }
}
