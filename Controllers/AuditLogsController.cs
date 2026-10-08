using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Data;
using AcxiomCrm.Models;
using AcxiomCrm.Services;

namespace AcxiomCrm.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLog;

        public AuditLogsController(ApplicationDbContext context, IAuditLogService auditLog)
        {
            _context = context;
            _auditLog = auditLog;
        }

        // GET: AuditLogs
        public async Task<IActionResult> Index(
            string? searchTerm, 
            string? entityFilter, 
            string? actionFilter, 
            int page = 1)
        {
            const int pageSize = 15;
            if (page < 1) page = 1;

            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(a =>
                    a.Details.ToLower().Contains(term) ||
                    (a.UserName != null && a.UserName.ToLower().Contains(term)) ||
                    (a.EntityId != null && a.EntityId.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(entityFilter))
            {
                query = query.Where(a => a.EntityName == entityFilter);
            }

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(a => a.Action == actionFilter);
            }

            var totalItems = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.EntityFilter = entityFilter;
            ViewBag.ActionFilter = actionFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.TotalItems = totalItems;

            // List of unique entities and actions for dropdowns
            ViewBag.Entities = await _context.AuditLogs.Select(a => a.EntityName).Distinct().ToListAsync();
            ViewBag.Actions = await _context.AuditLogs.Select(a => a.Action).Distinct().ToListAsync();

            return View(logs);
        }

        // GET: AuditLogs/ExportCsv
        public async Task<IActionResult> ExportCsv()
        {
            var logs = await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Id,Timestamp,EntityName,EntityId,Action,Details,UserName,UserId,IpAddress");

            foreach (var a in logs)
            {
                csv.AppendLine($"\"{a.Id}\",\"{a.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{a.EntityName}\",\"{a.EntityId}\",\"{a.Action}\",\"{a.Details.Replace("\"", "\"\"")}\",\"{a.UserName}\",\"{a.UserId}\",\"{a.IpAddress}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"AuditLogs_Export_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // POST: AuditLogs/ClearLogs
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ClearLogs()
        {
            var oldLogs = await _context.AuditLogs.ToListAsync();
            _context.AuditLogs.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("AuditLog", "All", "ClearLogs", "All previous audit log records were cleared by Administrator.");

            TempData["SuccessMessage"] = "Audit log archive has been cleared.";
            return RedirectToAction(nameof(Index));
        }
    }
}
