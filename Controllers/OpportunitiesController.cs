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
    public class OpportunitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditLog;

        public OpportunitiesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditLogService auditLog)
        {
            _context = context;
            _userManager = userManager;
            _auditLog = auditLog;
        }

        // GET: Opportunities
        public async Task<IActionResult> Index(
            string? searchTerm, 
            OpportunityStage? stageFilter, 
            int? customerId, 
            string? assignedToFilter, 
            int page = 1)
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .AsQueryable();

            // Role Scope: SalesExecutive only sees assigned opportunities
            if (isSalesExecutive && currentUser != null)
            {
                query = query.Where(o => o.AssignedToUserId == currentUser.Id);
            }
            else if (!string.IsNullOrWhiteSpace(assignedToFilter))
            {
                query = query.Where(o => o.AssignedToUserId == assignedToFilter);
            }

            // Search filter: Name / Title, Code, Customer Name
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(o =>
                    o.Title.ToLower().Contains(term) ||
                    (!string.IsNullOrEmpty(o.OpportunityCode) && o.OpportunityCode.ToLower().Contains(term)) ||
                    (o.Customer != null && o.Customer.Name.ToLower().Contains(term)));
            }

            // Stage filter
            if (stageFilter.HasValue)
            {
                query = query.Where(o => o.Stage == stageFilter.Value);
            }

            // Customer filter
            if (customerId.HasValue)
            {
                query = query.Where(o => o.CustomerId == customerId.Value);
            }

            var totalItems = await query.CountAsync();

            // Calculate pipeline metrics in-memory to prevent SQLite decimal aggregate errors
            var allOpportunities = await query.ToListAsync();

            var totalPipeline = allOpportunities
                .Where(o => o.Stage != OpportunityStage.ClosedLost && o.Stage != OpportunityStage.ClosedWon)
                .Sum(o => o.Amount);

            var weightedPipeline = allOpportunities
                .Where(o => o.Stage != OpportunityStage.ClosedLost && o.Stage != OpportunityStage.ClosedWon)
                .Sum(o => o.Amount * o.Probability / 100m);

            var wonRevenue = allOpportunities
                .Where(o => o.Stage == OpportunityStage.ClosedWon)
                .Sum(o => o.Amount);

            var wonCount = allOpportunities.Count(o => o.Stage == OpportunityStage.ClosedWon);
            var lostCount = allOpportunities.Count(o => o.Stage == OpportunityStage.ClosedLost);
            var closedTotal = wonCount + lostCount;
            var winRatio = closedTotal > 0 ? ((decimal)wonCount / closedTotal * 100) : 0m;

            var opportunities = allOpportunities
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.StageFilter = stageFilter;
            ViewBag.CustomerId = customerId;
            ViewBag.AssignedToFilter = assignedToFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPipeline = totalPipeline;
            ViewBag.WeightedPipeline = weightedPipeline;
            ViewBag.WonRevenue = wonRevenue;
            ViewBag.WinRatio = winRatio;
            ViewBag.IsSalesExecutive = isSalesExecutive;

            await PopulateDropdownsAsync(customerId, assignedToFilter);

            return View(opportunities);
        }

        // GET: Opportunities/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var opportunity = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .Include(o => o.FollowUps.OrderByDescending(f => f.DueDate))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (opportunity == null) return NotFound();

            if (!await IsAuthorizedForOpportunityAsync(opportunity))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");
            ViewBag.IsSalesExecutive = isSalesExecutive;

            return View(opportunity);
        }

        // GET: Opportunities/Create
        public async Task<IActionResult> Create(int? customerId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            await PopulateDropdownsAsync(customerId, isSalesExecutive ? currentUser?.Id : null);

            var opp = new Opportunity
            {
                Stage = OpportunityStage.Qualification,
                Probability = 30,
                ExpectedCloseDate = DateTime.UtcNow.AddMonths(1)
            };

            if (customerId.HasValue)
            {
                opp.CustomerId = customerId.Value;
            }

            if (isSalesExecutive && currentUser != null)
            {
                opp.AssignedToUserId = currentUser.Id;
            }

            ViewBag.IsSalesExecutive = isSalesExecutive;
            return View(opp);
        }

        // POST: Opportunities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Opportunity opportunity)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            if (isSalesExecutive && currentUser != null)
            {
                opportunity.AssignedToUserId = currentUser.Id;
            }

            // Validations
            if (opportunity.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity amount must be greater than 0.");
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100%.");
            }

            if (opportunity.CustomerId <= 0 || !await _context.Customers.AnyAsync(c => c.Id == opportunity.CustomerId))
            {
                ModelState.AddModelError("CustomerId", "Please select a valid customer organization.");
            }

            if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date && 
                opportunity.Stage != OpportunityStage.ClosedWon && 
                opportunity.Stage != OpportunityStage.ClosedLost)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected close date cannot be in the past for active opportunities.");
            }

            if (ModelState.IsValid)
            {
                opportunity.CreatedAt = DateTime.UtcNow;
                opportunity.CreatedBy = currentUser?.FullName ?? User.Identity?.Name ?? "System";

                _context.Opportunities.Add(opportunity);
                await _context.SaveChangesAsync();

                // Generate formatted OpportunityCode
                opportunity.OpportunityCode = $"OPP-{opportunity.Id:D4}";
                await _context.SaveChangesAsync();

                var customer = await _context.Customers.FindAsync(opportunity.CustomerId);
                await _auditLog.LogAsync("Opportunity", opportunity.Id.ToString(), "Create",
                    $"Created opportunity '{opportunity.Title}' ({opportunity.OpportunityCode}) for '{customer?.Name}' worth {opportunity.Amount:C0} (Stage: {opportunity.Stage}, Probability: {opportunity.Probability}%).");

                TempData["SuccessMessage"] = $"Opportunity '{opportunity.Title}' ({opportunity.OpportunityCode}) created successfully.";
                return RedirectToAction(nameof(Details), new { id = opportunity.Id });
            }

            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.AssignedToUserId);
            ViewBag.IsSalesExecutive = isSalesExecutive;
            return View(opportunity);
        }

        // GET: Opportunities/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var opportunity = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (opportunity == null) return NotFound();

            if (!await IsAuthorizedForOpportunityAsync(opportunity))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");
            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.AssignedToUserId);
            ViewBag.IsSalesExecutive = isSalesExecutive;

            return View(opportunity);
        }

        // POST: Opportunities/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Opportunity opportunity)
        {
            if (id != opportunity.Id) return NotFound();

            var existingOpp = await _context.Opportunities
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (existingOpp == null) return NotFound();

            if (!await IsAuthorizedForOpportunityAsync(existingOpp))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            // Role restriction: SalesExecutive cannot reassign to other users
            if (isSalesExecutive)
            {
                opportunity.AssignedToUserId = existingOpp.AssignedToUserId;
            }

            // Validations
            if (opportunity.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity amount must be greater than 0.");
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100%.");
            }

            if (opportunity.CustomerId <= 0 || !await _context.Customers.AnyAsync(c => c.Id == opportunity.CustomerId))
            {
                ModelState.AddModelError("CustomerId", "Please select a valid customer organization.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    opportunity.OpportunityCode = existingOpp.OpportunityCode ?? $"OPP-{opportunity.Id:D4}";
                    opportunity.CreatedAt = existingOpp.CreatedAt;
                    opportunity.CreatedBy = existingOpp.CreatedBy ?? "System";
                    opportunity.UpdatedAt = DateTime.UtcNow;

                    // Auto sync probability on Won/Lost
                    if (opportunity.Stage == OpportunityStage.ClosedWon)
                    {
                        opportunity.Probability = 100;
                    }
                    else if (opportunity.Stage == OpportunityStage.ClosedLost)
                    {
                        opportunity.Probability = 0;
                    }

                    _context.Update(opportunity);
                    await _context.SaveChangesAsync();

                    // Audit Stage Change if changed
                    if (existingOpp.Stage != opportunity.Stage)
                    {
                        await _auditLog.LogAsync("Opportunity", opportunity.Id.ToString(), "StageChange",
                            $"Opportunity '{opportunity.Title}' ({opportunity.OpportunityCode}) stage changed from '{existingOpp.Stage}' to '{opportunity.Stage}'.");
                    }

                    // Audit General Update
                    await _auditLog.LogAsync("Opportunity", opportunity.Id.ToString(), "Update",
                        $"Updated opportunity '{opportunity.Title}' ({opportunity.OpportunityCode}).");

                    TempData["SuccessMessage"] = $"Opportunity '{opportunity.Title}' updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = opportunity.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await OpportunityExistsAsync(opportunity.Id)) return NotFound();
                    throw;
                }
            }

            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.AssignedToUserId);
            ViewBag.IsSalesExecutive = isSalesExecutive;
            return View(opportunity);
        }

        // POST: Opportunities/UpdateStage/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStage(int id, OpportunityStage newStage, string? winLossReason)
        {
            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();

            if (!await IsAuthorizedForOpportunityAsync(opp))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var oldStage = opp.Stage;
            opp.Stage = newStage;
            opp.UpdatedAt = DateTime.UtcNow;

            // Auto-adjust probability based on stage heuristics
            opp.Probability = newStage switch
            {
                OpportunityStage.Prospecting => 10,
                OpportunityStage.Qualification => 30,
                OpportunityStage.Proposal => 60,
                OpportunityStage.Negotiation => 80,
                OpportunityStage.ClosedWon => 100,
                OpportunityStage.ClosedLost => 0,
                _ => opp.Probability
            };

            if (!string.IsNullOrWhiteSpace(winLossReason))
            {
                opp.WinLossReason = winLossReason.Trim();
            }

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Opportunity", opp.Id.ToString(), "StageChange",
                $"Opportunity '{opp.Title}' ({opp.OpportunityCode}) changed stage from {oldStage} to {newStage} (Probability: {opp.Probability}%).");

            TempData["SuccessMessage"] = $"Opportunity stage updated to {newStage}.";
            return RedirectToAction(nameof(Details), new { id = opp.Id });
        }

        // GET: Opportunities/Delete/5
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var opportunity = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (opportunity == null) return NotFound();

            return View(opportunity);
        }

        // POST: Opportunities/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var opportunity = await _context.Opportunities.FindAsync(id);
            if (opportunity != null)
            {
                var oppTitle = opportunity.Title;
                var oppCode = opportunity.OpportunityCode;
                var oppAmount = opportunity.Amount;

                _context.Opportunities.Remove(opportunity);
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("Opportunity", id.ToString(), "Delete",
                    $"Deleted opportunity '{oppTitle}' ({oppCode}) worth {oppAmount:C0}.");

                TempData["SuccessMessage"] = $"Opportunity '{oppTitle}' was deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Opportunities/ExportCsv
        public async Task<IActionResult> ExportCsv()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .AsQueryable();

            if (isSalesExecutive && currentUser != null)
            {
                query = query.Where(o => o.AssignedToUserId == currentUser.Id);
            }

            var opps = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("OpportunityCode,Title,Customer,Amount,WeightedValue,Stage,Probability,ExpectedCloseDate,AssignedTo,CreatedBy,CreatedAt,WinLossReason");

            foreach (var o in opps)
            {
                csv.AppendLine($"\"{o.OpportunityCode}\",\"{o.Title.Replace("\"", "\"\"")}\",\"{o.Customer?.Name?.Replace("\"", "\"\"")}\",\"{o.Amount}\",\"{o.WeightedValue}\",\"{o.Stage}\",\"{o.Probability}%\",\"{o.ExpectedCloseDate:yyyy-MM-dd}\",\"{o.AssignedToUser?.FullName}\",\"{o.CreatedBy}\",\"{o.CreatedAt:yyyy-MM-dd HH:mm}\",\"{o.WinLossReason?.Replace("\"", "\"\"")}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Opportunities_Export_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private async Task<bool> IsAuthorizedForOpportunityAsync(Opportunity opp)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Manager"))
            {
                return true;
            }

            var currentUser = await _userManager.GetUserAsync(User);
            return currentUser != null && opp.AssignedToUserId == currentUser.Id;
        }

        private async Task PopulateDropdownsAsync(int? selectedCustomerId = null, string? selectedUserId = null)
        {
            var customers = await _context.Customers
                .OrderBy(c => c.Name)
                .ToListAsync();

            var users = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.CustomerId = new SelectList(customers, "Id", "Name", selectedCustomerId);
            ViewBag.AssignedToUserId = new SelectList(users, "Id", "FullName", selectedUserId);
            ViewBag.UsersList = users;
            ViewBag.CustomersList = customers;
        }

        private async Task<bool> OpportunityExistsAsync(int id)
        {
            return await _context.Opportunities.AnyAsync(e => e.Id == id);
        }
    }
}
