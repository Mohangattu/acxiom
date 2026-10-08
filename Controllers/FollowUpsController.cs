using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Data;
using AcxiomCrm.Models;
using AcxiomCrm.Services;

namespace AcxiomCrm.Controllers
{
    [Authorize]
    public class FollowUpsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditLog;

        public FollowUpsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditLogService auditLog)
        {
            _context = context;
            _userManager = userManager;
            _auditLog = auditLog;
        }

        // GET: FollowUps
        public async Task<IActionResult> Index(
            string? searchTerm, 
            FollowUpStatus? statusFilter, 
            FollowUpType? typeFilter, 
            PriorityLevel? priorityFilter,
            int page = 1)
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedToUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(f =>
                    f.Subject.ToLower().Contains(term) ||
                    (f.Notes != null && f.Notes.ToLower().Contains(term)) ||
                    (f.Customer != null && f.Customer.Name.ToLower().Contains(term)) ||
                    (f.Lead != null && (f.Lead.FirstName.ToLower().Contains(term) || f.Lead.LastName.ToLower().Contains(term))));
            }

            if (statusFilter.HasValue)
            {
                query = query.Where(f => f.Status == statusFilter.Value);
            }

            if (typeFilter.HasValue)
            {
                query = query.Where(f => f.Type == typeFilter.Value);
            }

            if (priorityFilter.HasValue)
            {
                query = query.Where(f => f.Priority == priorityFilter.Value);
            }

            var totalItems = await query.CountAsync();
            var followUps = await query
                .OrderBy(f => f.Status == FollowUpStatus.Pending ? 0 : 1)
                .ThenBy(f => f.DueDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.TypeFilter = typeFilter;
            ViewBag.PriorityFilter = priorityFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.TotalItems = totalItems;

            return View(followUps);
        }

        // GET: FollowUps/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var followUp = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (followUp == null)
            {
                return NotFound();
            }

            return View(followUp);
        }

        // GET: FollowUps/Create
        public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
        {
            await PopulateDropdownsAsync(customerId, leadId, opportunityId, null);

            var followUp = new FollowUp
            {
                DueDate = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
                CustomerId = customerId,
                LeadId = leadId,
                OpportunityId = opportunityId
            };

            return View(followUp);
        }

        // POST: FollowUps/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUp followUp)
        {
            if (followUp.DueDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("DueDate", "New follow-up date cannot be before today.");
            }

            if (ModelState.IsValid)
            {
                followUp.CreatedAt = DateTime.UtcNow;
                _context.FollowUps.Add(followUp);
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("FollowUp", followUp.Id.ToString(), "Create",
                    $"Scheduled {followUp.Type} task '{followUp.Subject}' due on {followUp.DueDate:yyyy-MM-dd}.");

                TempData["SuccessMessage"] = $"Follow-up task '{followUp.Subject}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedToUserId);
            return View(followUp);
        }

        // GET: FollowUps/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null)
            {
                return NotFound();
            }

            await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedToUserId);
            return View(followUp);
        }

        // POST: FollowUps/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FollowUp followUp)
        {
            if (id != followUp.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (followUp.Status == FollowUpStatus.Completed && !followUp.CompletedAt.HasValue)
                    {
                        followUp.CompletedAt = DateTime.UtcNow;
                    }
                    else if (followUp.Status == FollowUpStatus.Pending)
                    {
                        followUp.CompletedAt = null;
                    }

                    _context.Update(followUp);
                    await _context.SaveChangesAsync();

                    await _auditLog.LogAsync("FollowUp", followUp.Id.ToString(), "Update",
                        $"Updated follow-up task '{followUp.Subject}'.");

                    TempData["SuccessMessage"] = $"Follow-up task '{followUp.Subject}' updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = followUp.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await FollowUpExistsAsync(followUp.Id))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }

            await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedToUserId);
            return View(followUp);
        }

        // POST: FollowUps/ToggleComplete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleComplete(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null)
            {
                return NotFound();
            }

            if (followUp.Status == FollowUpStatus.Completed)
            {
                followUp.Status = FollowUpStatus.Pending;
                followUp.CompletedAt = null;
                await _auditLog.LogAsync("FollowUp", followUp.Id.ToString(), "StatusChange",
                    $"Reopened follow-up task '{followUp.Subject}'.");
                TempData["SuccessMessage"] = $"Task '{followUp.Subject}' marked as Pending.";
            }
            else
            {
                followUp.Status = FollowUpStatus.Completed;
                followUp.CompletedAt = DateTime.UtcNow;
                await _auditLog.LogAsync("FollowUp", followUp.Id.ToString(), "StatusChange",
                    $"Completed follow-up task '{followUp.Subject}'.");
                TempData["SuccessMessage"] = $"Task '{followUp.Subject}' marked as Completed!";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: FollowUps/Reschedule/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? notes)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null)
            {
                return NotFound();
            }

            if (newDate.Date < DateTime.UtcNow.Date)
            {
                TempData["ErrorMessage"] = "Rescheduled follow-up date cannot be before today.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var oldDate = followUp.DueDate;
            followUp.DueDate = newDate;
            followUp.Status = FollowUpStatus.Pending;
            followUp.CompletedAt = null;

            if (!string.IsNullOrWhiteSpace(notes))
            {
                followUp.Notes = string.IsNullOrEmpty(followUp.Notes) 
                    ? $"[Rescheduled]: {notes.Trim()}" 
                    : $"{followUp.Notes}\n[Rescheduled]: {notes.Trim()}";
            }

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("FollowUp", followUp.Id.ToString(), "Reschedule",
                $"Rescheduled follow-up task '{followUp.Subject}' from {oldDate:yyyy-MM-dd} to {newDate:yyyy-MM-dd}.");

            TempData["SuccessMessage"] = $"Task '{followUp.Subject}' successfully rescheduled to {newDate:MMM dd, yyyy}.";
            return RedirectToAction(nameof(Details), new { id = followUp.Id });
        }

        // GET: FollowUps/Delete/5
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var followUp = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (followUp == null)
            {
                return NotFound();
            }

            return View(followUp);
        }

        // POST: FollowUps/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp != null)
            {
                _context.FollowUps.Remove(followUp);
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("FollowUp", id.ToString(), "Delete",
                    $"Deleted follow-up task '{followUp.Subject}'.");

                TempData["SuccessMessage"] = $"Follow-up task '{followUp.Subject}' was deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: FollowUps/ExportCsv
        public async Task<IActionResult> ExportCsv()
        {
            var followUps = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedToUser)
                .OrderBy(f => f.DueDate)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Id,Subject,Type,DueDate,Status,Priority,Customer,Lead,Opportunity,AssignedTo,CompletedAt");

            foreach (var f in followUps)
            {
                var leadName = f.Lead != null ? $"{f.Lead.FirstName} {f.Lead.LastName}" : "";
                csv.AppendLine($"\"{f.Id}\",\"{f.Subject}\",\"{f.Type}\",\"{f.DueDate:yyyy-MM-dd HH:mm}\",\"{f.Status}\",\"{f.Priority}\",\"{f.Customer?.Name}\",\"{leadName}\",\"{f.Opportunity?.Title}\",\"{f.AssignedToUser?.FullName}\",\"{f.CompletedAt:yyyy-MM-dd HH:mm}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"FollowUps_Export_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private async Task PopulateDropdownsAsync(int? selectedCustomerId = null, int? selectedLeadId = null, int? selectedOpportunityId = null, string? selectedUserId = null)
        {
            var customers = await _context.Customers.OrderBy(c => c.Name).ToListAsync();
            var leads = await _context.Leads.Where(l => l.Status != LeadStatus.Converted).OrderBy(l => l.FirstName).ToListAsync();
            var opps = await _context.Opportunities.OrderBy(o => o.Title).ToListAsync();
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();

            ViewBag.CustomerId = new SelectList(customers, "Id", "Name", selectedCustomerId);
            ViewBag.LeadId = new SelectList(leads, "Id", "FullName", selectedLeadId);
            ViewBag.OpportunityId = new SelectList(opps, "Id", "Title", selectedOpportunityId);
            ViewBag.AssignedToUserId = new SelectList(users, "Id", "FullName", selectedUserId);
        }

        private async Task<bool> FollowUpExistsAsync(int id)
        {
            return await _context.FollowUps.AnyAsync(e => e.Id == id);
        }
    }
}
