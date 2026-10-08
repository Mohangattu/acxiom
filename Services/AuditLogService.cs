using AcxiomCrm.Data;
using AcxiomCrm.Models;
using System.Security.Claims;

namespace AcxiomCrm.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string entityName, string? entityId, string action, string details, string? userId = null, string? userName = null, string? ipAddress = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                
                // Fallback to current authenticated user if not provided
                if (string.IsNullOrEmpty(userId) && httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                }

                if (string.IsNullOrEmpty(userName) && httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    userName = httpContext.User.Identity.Name;
                }

                if (string.IsNullOrEmpty(ipAddress) && httpContext != null)
                {
                    ipAddress = httpContext.Connection?.RemoteIpAddress?.ToString();
                }

                var log = new AuditLog
                {
                    EntityName = entityName,
                    EntityId = entityId,
                    Action = action,
                    Details = details,
                    UserId = userId,
                    UserName = userName ?? "System",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = ipAddress
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Silently handle logging failures so core business actions are never interrupted
            }
        }
    }
}
