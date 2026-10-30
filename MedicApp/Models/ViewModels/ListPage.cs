namespace MedicApp.Models.ViewModels;

public class ListPage<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public Dictionary<int, string> People { get; set; } = [];
    public Dictionary<int, string> Doctors { get; set; } = [];
    public int Page { get; set; }
    public int Total { get; set; }
    public string Query { get; set; } = "";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? CurrentDoctorId { get; set; }
}
