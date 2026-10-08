namespace AcxiomCrm.Services
{
    public interface IAuditLogService
    {
        Task LogAsync(string entityName, string? entityId, string action, string details, string? userId = null, string? userName = null, string? ipAddress = null);
    }
}
