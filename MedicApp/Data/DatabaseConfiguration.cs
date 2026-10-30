namespace MedicApp.Data;

public static class DatabaseConfiguration
{
    public static string Resolve(IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Workforce");
        return string.IsNullOrWhiteSpace(connection)
            ? "Server=(localdb)\\MSSQLLocalDB;Database=MedicAppNotConfigured;Integrated Security=true"
            : connection;
    }
}
