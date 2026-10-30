using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Trainify.Data;

namespace WFMCore.Medical;

/// <summary>Reads only labor restrictions. Database failures propagate; they are never treated as employee availability.</summary>
public class MedicalRestrictionReader(TrainifyDbContext shared)
{
    public async Task<IReadOnlyList<MedicalRestriction>> FindAsync(int personId, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(personId);
        if (startsAtUtc.Kind != DateTimeKind.Utc || endsAtUtc.Kind != DateTimeKind.Utc || endsAtUtc <= startsAtUtc)
        { throw new ArgumentException("Provide a positive UTC interval."); }
        await using var connection = new SqlConnection(shared.Database.GetConnectionString());
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                  LR.PKMedicalLeave
                , LR.FKPerson
                , LR.StartsAtUtc
                , LR.EndsAtUtc
                , LR.Revision
            FROM medic.vw_ApprovedLaborRestrictions AS LR
            WHERE LR.FKPerson = @FKPerson
                AND LR.StartsAtUtc < @EndsAtUtc
                AND LR.EndsAtUtc > @StartsAtUtc
            ORDER BY LR.StartsAtUtc, LR.PKMedicalLeave;
            """;
        command.Parameters.Add("@FKPerson", System.Data.SqlDbType.Int).Value = personId;
        command.Parameters.Add("@StartsAtUtc", System.Data.SqlDbType.DateTime2).Value = startsAtUtc;
        command.Parameters.Add("@EndsAtUtc", System.Data.SqlDbType.DateTime2).Value = endsAtUtc;
        var result = new List<MedicalRestriction>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new(reader.GetInt32(0), reader.GetInt32(1), DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc), DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc), reader.GetInt32(4)));
        }
        return result;
    }
}
