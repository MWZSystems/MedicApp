namespace WFMCore.Medical;

public record MedicalRestriction(int LeaveId, int PersonId, DateTime StartsAtUtc, DateTime EndsAtUtc, int Revision);
