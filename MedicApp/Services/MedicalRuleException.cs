namespace MedicApp.Services;

public class MedicalRuleException(string resourceKey) : Exception(resourceKey);
