using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Data;
using AcxiomCrm.Models;
using AcxiomCrm.Services;
using AcxiomCrm.ViewModels;

namespace AcxiomCrm.Controllers
{
    [Authorize]
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditLog;

        public LeadsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditLogService auditLog)
        {
            _context = context;
            _userManager = userManager;
            _auditLog = auditLog;
        }

        // GET: Leads
        public async Task<IActionResult> Index(
            string? searchTerm,
            LeadStatus? statusFilter,
            string? companyFilter,
            string? assignedToFilter,
            int page = 1)
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            var query = _context.Leads
                .Include(l => l.AssignedToUser)
                .Include(l => l.ConvertedCustomer)
                .AsQueryable();

            // Role Scope: SalesExecutive only sees assigned leads
            if (isSalesExecutive && currentUser != null)
            {
                query = query.Where(l => l.AssignedToUserId == currentUser.Id);
            }

            // Search filter (Name, Company, Email, Phone)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(l =>
                    (!string.IsNullOrEmpty(l.LeadName) && l.LeadName.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(l.FirstName) && l.FirstName.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(l.LastName) && l.LastName.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(l.Email) && l.Email.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(l.Phone) && l.Phone.Contains(term)) ||
                    (!string.IsNullOrEmpty(l.Company) && l.Company.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(l.LeadCode) && l.LeadCode.ToLower().Contains(term)));
            }

            // Company filter
            if (!string.IsNullOrWhiteSpace(companyFilter))
            {
                var comp = companyFilter.Trim().ToLower();
                query = query.Where(l => l.Company != null && l.Company.ToLower().Contains(comp));
            }

            // Status filter
            if (statusFilter.HasValue)
            {
                query = query.Where(l => l.Status == statusFilter.Value);
            }

            // Assigned To filter
            if (!string.IsNullOrWhiteSpace(assignedToFilter))
            {
                query = query.Where(l => l.AssignedToUserId == assignedToFilter);
            }

            var totalItems = await query.CountAsync();
            var leads = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            await PopulateUsersDropdownAsync(assignedToFilter);

            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.CompanyFilter = companyFilter;
            ViewBag.AssignedToFilter = assignedToFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.TotalItems = totalItems;
            ViewBag.IsSalesExecutive = isSalesExecutive;

            return View(leads);
        }

        // GET: Leads/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .Include(l => l.AssignedToUser)
                .Include(l => l.ConvertedCustomer)
                .Include(l => l.FollowUps.OrderByDescending(f => f.DueDate))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (lead == null) return NotFound();

            // Authorization check
            if (!await IsAuthorizedForLeadAsync(lead))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(lead);
        }

        // GET: Leads/Create
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            var lead = new Lead
            {
                Status = LeadStatus.New,
                AssignedToUserId = isSalesExecutive ? currentUser?.Id : null
            };

            await PopulateUsersDropdownAsync(lead.AssignedToUserId);
            ViewBag.IsSalesExecutive = isSalesExecutive;
            return View(lead);
        }

        // POST: Leads/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            // Server-side validation: Lead Name
            if (string.IsNullOrWhiteSpace(lead.LeadName) && string.IsNullOrWhiteSpace(lead.FirstName))
            {
                ModelState.AddModelError("LeadName", "Lead name is required.");
            }

            // Sync LeadName and First/LastName
            if (!string.IsNullOrWhiteSpace(lead.LeadName))
            {
                var parts = lead.LeadName.Trim().Split(' ', 2);
                lead.FirstName = parts[0];
                lead.LastName = parts.Length > 1 ? parts[1] : "";
            }
            else if (!string.IsNullOrWhiteSpace(lead.FirstName))
            {
                lead.LeadName = $"{lead.FirstName} {lead.LastName}".Trim();
            }

            // Server-side validation: Status
            if (!Enum.IsDefined(typeof(LeadStatus), lead.Status))
            {
                ModelState.AddModelError("Status", "Invalid lead status.");
            }
            else if (lead.Status == LeadStatus.Converted)
            {
                ModelState.AddModelError("Status", "A new lead cannot be created with Converted status. Use the Convert workflow after qualifying.");
            }

            // Server-side validation: Expected Value
            if (lead.EstimatedValue.HasValue && lead.EstimatedValue.Value < 0)
            {
                ModelState.AddModelError("EstimatedValue", "Expected value must be non-negative.");
            }

            // Server-side validation: Phone format if supplied
            if (!string.IsNullOrWhiteSpace(lead.Phone) && !System.Text.RegularExpressions.Regex.IsMatch(lead.Phone, @"^[0-9+\-\(\) ]{7,20}$"))
            {
                ModelState.AddModelError("Phone", "Invalid phone number format.");
            }

            // Assignment validation
            if (isSalesExecutive && currentUser != null)
            {
                // SalesExecutive must be assigned to themselves
                lead.AssignedToUserId = currentUser.Id;
            }
            else if (!string.IsNullOrEmpty(lead.AssignedToUserId))
            {
                // Validate assigned user exists and is active
                var assignedUser = await _userManager.FindByIdAsync(lead.AssignedToUserId);
                if (assignedUser == null || !assignedUser.IsActive)
                {
                    ModelState.AddModelError("AssignedToUserId", "Selected user is invalid or inactive.");
                }
            }

            if (ModelState.IsValid)
            {
                lead.CreatedAt = DateTime.UtcNow;
                _context.Leads.Add(lead);
                await _context.SaveChangesAsync();

                // Generate LeadCode
                lead.LeadCode = $"LEAD-{lead.Id:D4}";
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("Lead", lead.Id.ToString(), "Create",
                    $"Created new lead '{lead.FullName}' ({lead.LeadCode}) with status '{lead.Status}'.");

                if (!string.IsNullOrEmpty(lead.AssignedToUserId))
                {
                    var assignedUser = await _userManager.FindByIdAsync(lead.AssignedToUserId);
                    if (assignedUser != null)
                    {
                        await _auditLog.LogAsync("Lead", lead.Id.ToString(), "Assign",
                            $"Assigned lead '{lead.FullName}' to {assignedUser.FullName} ({assignedUser.Email}).");
                    }
                }

                TempData["SuccessMessage"] = $"Lead '{lead.FullName}' ({lead.LeadCode}) created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUsersDropdownAsync(lead.AssignedToUserId);
            ViewBag.IsSalesExecutive = isSalesExecutive;
            return View(lead);
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .Include(l => l.AssignedToUser)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lead == null) return NotFound();

            if (!await IsAuthorizedForLeadAsync(lead))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (lead.Status == LeadStatus.Converted)
            {
                TempData["ErrorMessage"] = "Converted leads cannot be directly edited.";
                return RedirectToAction(nameof(Details), new { id = lead.Id });
            }

            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");
            await PopulateUsersDropdownAsync(lead.AssignedToUserId);
            ViewBag.IsSalesExecutive = isSalesExecutive;

            return View(lead);
        }

        // POST: Leads/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lead lead)
        {
            if (id != lead.Id) return NotFound();

            var existingLead = await _context.Leads
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id);

            if (existingLead == null) return NotFound();

            if (!await IsAuthorizedForLeadAsync(existingLead))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (existingLead.Status == LeadStatus.Converted)
            {
                TempData["ErrorMessage"] = "Converted leads cannot be directly edited.";
                return RedirectToAction(nameof(Details), new { id = lead.Id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

            // Server-side validation: Lead Name
            if (string.IsNullOrWhiteSpace(lead.LeadName) && string.IsNullOrWhiteSpace(lead.FirstName))
            {
                ModelState.AddModelError("LeadName", "Lead name is required.");
            }

            // Sync LeadName and First/LastName
            if (!string.IsNullOrWhiteSpace(lead.LeadName))
            {
                var parts = lead.LeadName.Trim().Split(' ', 2);
                lead.FirstName = parts[0];
                lead.LastName = parts.Length > 1 ? parts[1] : "";
            }
            else if (!string.IsNullOrWhiteSpace(lead.FirstName))
            {
                lead.LeadName = $"{lead.FirstName} {lead.LastName}".Trim();
            }

            // Server-side validation: Status
            if (!Enum.IsDefined(typeof(LeadStatus), lead.Status))
            {
                ModelState.AddModelError("Status", "Invalid lead status.");
            }
            else if (lead.Status == LeadStatus.Converted && existingLead.Status != LeadStatus.Converted)
            {
                ModelState.AddModelError("Status", "Direct status change to Converted is not allowed. Please use the Convert action to convert a qualified lead into a customer.");
            }

            // Server-side validation: Expected Value
            if (lead.EstimatedValue.HasValue && lead.EstimatedValue.Value < 0)
            {
                ModelState.AddModelError("EstimatedValue", "Expected value must be non-negative.");
            }

            // Server-side validation: Phone format if supplied
            if (!string.IsNullOrWhiteSpace(lead.Phone) && !System.Text.RegularExpressions.Regex.IsMatch(lead.Phone, @"^[0-9+\-\(\) ]{7,20}$"))
            {
                ModelState.AddModelError("Phone", "Invalid phone number format.");
            }

            // Assignment protection: SalesExecutive cannot reassign
            if (isSalesExecutive)
            {
                lead.AssignedToUserId = existingLead.AssignedToUserId;
            }
            else if (!string.IsNullOrEmpty(lead.AssignedToUserId))
            {
                var assignedUser = await _userManager.FindByIdAsync(lead.AssignedToUserId);
                if (assignedUser == null || !assignedUser.IsActive)
                {
                    ModelState.AddModelError("AssignedToUserId", "Selected user is invalid or inactive.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Preserve LeadCode and CreatedAt
                    lead.LeadCode = existingLead.LeadCode ?? $"LEAD-{lead.Id:D4}";
                    lead.CreatedAt = existingLead.CreatedAt;
                    lead.ConvertedCustomerId = existingLead.ConvertedCustomerId;
                    lead.ConvertedAt = existingLead.ConvertedAt;

                    _context.Update(lead);
                    await _context.SaveChangesAsync();

                    // Audit Status Change if changed
                    if (existingLead.Status != lead.Status)
                    {
                        await _auditLog.LogAsync("Lead", lead.Id.ToString(), "StatusChange",
                            $"Changed status of lead '{lead.FullName}' from '{existingLead.Status}' to '{lead.Status}'.");
                    }

                    // Audit Assignment Change if changed
                    if (existingLead.AssignedToUserId != lead.AssignedToUserId)
                    {
                        var newAssignedUser = !string.IsNullOrEmpty(lead.AssignedToUserId)
                            ? await _userManager.FindByIdAsync(lead.AssignedToUserId)
                            : null;
                        await _auditLog.LogAsync("Lead", lead.Id.ToString(), "Assign",
                            $"Reassigned lead '{lead.FullName}' to {(newAssignedUser != null ? newAssignedUser.FullName : "Unassigned")}.");
                    }

                    // Audit General Update
                    await _auditLog.LogAsync("Lead", lead.Id.ToString(), "Update",
                        $"Updated details for lead '{lead.FullName}' ({lead.LeadCode}).");

                    TempData["SuccessMessage"] = $"Lead '{lead.FullName}' updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = lead.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await LeadExistsAsync(lead.Id)) return NotFound();
                    throw;
                }
            }

            await PopulateUsersDropdownAsync(lead.AssignedToUserId);
            ViewBag.IsSalesExecutive = isSalesExecutive;
            return View(lead);
        }

        // POST: Leads/Assign (Quick assignment by Admin/Manager)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Assign(int id, string? assignedToUserId)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!string.IsNullOrEmpty(assignedToUserId))
            {
                var user = await _userManager.FindByIdAsync(assignedToUserId);
                if (user == null || !user.IsActive)
                {
                    TempData["ErrorMessage"] = "Selected user is invalid or inactive.";
                    return RedirectToAction(nameof(Details), new { id = lead.Id });
                }
                lead.AssignedToUserId = assignedToUserId;
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("Lead", lead.Id.ToString(), "Assign",
                    $"Assigned lead '{lead.FullName}' to {user.FullName} ({user.Email}).");

                TempData["SuccessMessage"] = $"Lead '{lead.FullName}' assigned to {user.FullName}.";
            }
            else
            {
                lead.AssignedToUserId = null;
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("Lead", lead.Id.ToString(), "Assign",
                    $"Unassigned lead '{lead.FullName}'.");

                TempData["SuccessMessage"] = $"Lead '{lead.FullName}' is now unassigned.";
            }

            return RedirectToAction(nameof(Details), new { id = lead.Id });
        }

        // GET: Leads/Convert/5
        public async Task<IActionResult> Convert(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!await IsAuthorizedForLeadAsync(lead))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (lead.Status == LeadStatus.Converted)
            {
                TempData["ErrorMessage"] = "This lead has already been converted into a customer.";
                return RedirectToAction(nameof(Details), new { id = lead.Id });
            }

            var model = new ConvertLeadViewModel
            {
                LeadId = lead.Id,
                LeadFullName = lead.FullName,
                CustomerName = !string.IsNullOrEmpty(lead.Company) ? lead.Company : lead.FullName,
                Email = lead.Email,
                Phone = lead.Phone,
                Company = lead.Company,
                AssignedToUserId = lead.AssignedToUserId,
                CreateOpportunity = true,
                OpportunityTitle = $"{(!string.IsNullOrEmpty(lead.Company) ? lead.Company : lead.FullName)} - Initial Deal",
                OpportunityAmount = lead.EstimatedValue ?? 10000,
                OpportunityStage = OpportunityStage.Qualification,
                ExpectedCloseDate = DateTime.UtcNow.AddMonths(1)
            };

            await PopulateUsersDropdownAsync(lead.AssignedToUserId);
            return View(model);
        }

        // POST: Leads/Convert/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(ConvertLeadViewModel model)
        {
            var lead = await _context.Leads
                .Include(l => l.FollowUps)
                .FirstOrDefaultAsync(l => l.Id == model.LeadId);

            if (lead == null) return NotFound();

            if (!await IsAuthorizedForLeadAsync(lead))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (lead.Status == LeadStatus.Converted)
            {
                TempData["ErrorMessage"] = "This lead has already been converted into a customer.";
                return RedirectToAction(nameof(Details), new { id = lead.Id });
            }

            if (model.CreateOpportunity && (!model.OpportunityAmount.HasValue || model.OpportunityAmount.Value <= 0))
            {
                ModelState.AddModelError("OpportunityAmount", "Opportunity amount must be greater than 0.");
            }

            if (ModelState.IsValid)
            {
                // Duplicate prevention: check if customer already exists by Email or Phone
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c =>
                        (!string.IsNullOrEmpty(model.Email) && c.Email.ToLower() == model.Email.ToLower()) ||
                        (!string.IsNullOrEmpty(model.Phone) && c.Phone == model.Phone));

                Customer customer;
                if (existingCustomer != null)
                {
                    // Reuse existing customer to prevent duplicates
                    customer = existingCustomer;
                    if (string.IsNullOrEmpty(customer.Company) && !string.IsNullOrEmpty(model.Company))
                    {
                        customer.Company = model.Company;
                    }
                }
                else
                {
                    // Create new customer
                    customer = new Customer
                    {
                        Name = model.CustomerName,
                        Email = model.Email,
                        Phone = model.Phone ?? "",
                        Company = model.Company,
                        Industry = model.Industry,
                        Status = CustomerStatus.Active,
                        AssignedToUserId = model.AssignedToUserId,
                        CreatedBy = User.Identity?.Name ?? "System",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();

                    customer.CustomerCode = $"CUST-{customer.Id:D4}";
                    await _context.SaveChangesAsync();

                    await _auditLog.LogAsync("Customer", customer.Id.ToString(), "Create",
                        $"Created customer '{customer.Name}' ({customer.CustomerCode}) from converted lead '{lead.FullName}'.");
                }

                // Optionally create initial Opportunity
                Opportunity? createdOpportunity = null;
                if (model.CreateOpportunity && !string.IsNullOrEmpty(model.OpportunityTitle))
                {
                    createdOpportunity = new Opportunity
                    {
                        Title = model.OpportunityTitle,
                        CustomerId = customer.Id,
                        Amount = model.OpportunityAmount ?? 0,
                        Stage = model.OpportunityStage,
                        Probability = model.OpportunityStage == OpportunityStage.Prospecting ? 20 : 40,
                        ExpectedCloseDate = model.ExpectedCloseDate,
                        AssignedToUserId = model.AssignedToUserId,
                        CreatedBy = User.Identity?.Name ?? "System",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Opportunities.Add(createdOpportunity);
                    await _context.SaveChangesAsync();

                    createdOpportunity.OpportunityCode = $"OPP-{createdOpportunity.Id:D4}";
                    await _context.SaveChangesAsync();
                }

                // Update Lead status to Converted
                lead.Status = LeadStatus.Converted;
                lead.ConvertedCustomerId = customer.Id;
                lead.ConvertedAt = DateTime.UtcNow;

                // Link existing follow-ups if any
                foreach (var followUp in lead.FollowUps)
                {
                    followUp.CustomerId = customer.Id;
                    if (createdOpportunity != null && followUp.OpportunityId == null)
                    {
                        followUp.OpportunityId = createdOpportunity.Id;
                    }
                }

                await _context.SaveChangesAsync();

                // Audit Conversion
                await _auditLog.LogAsync("Lead", lead.Id.ToString(), "Convert",
                    $"Converted lead '{lead.FullName}' into customer '{customer.Name}' (ID: {customer.Id}).");

                TempData["SuccessMessage"] = $"Lead '{lead.FullName}' successfully converted into customer '{customer.Name}'!";
                return RedirectToAction("Details", "Customers", new { id = customer.Id });
            }

            await PopulateUsersDropdownAsync(model.AssignedToUserId);
            return View(model);
        }

        // GET: Leads/Delete/5
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .Include(l => l.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (lead == null) return NotFound();

            return View(lead);
        }

        // POST: Leads/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead != null)
            {
                var leadName = lead.FullName;
                var leadCode = lead.LeadCode;

                _context.Leads.Remove(lead);
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("Lead", id.ToString(), "Delete",
                    $"Deleted lead '{leadName}' ({leadCode}).");

                TempData["SuccessMessage"] = $"Lead '{leadName}' was deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Leads/ExportCsv
        public async Task<IActionResult> ExportCsv()
        {
            var query = _context.Leads
                .Include(l => l.AssignedToUser)
                .AsQueryable();

            var isSalesExecutive = User.IsInRole("SalesExecutive") && !User.IsInRole("Admin") && !User.IsInRole("Manager");
            if (isSalesExecutive)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    query = query.Where(l => l.AssignedToUserId == currentUser.Id);
                }
            }

            var leads = await query.OrderBy(l => l.LeadName).ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("LeadCode,LeadName,Email,Phone,Company,Source,Status,ExpectedValue,AssignedTo,CreatedAt");

            foreach (var l in leads)
            {
                csv.AppendLine($"\"{l.LeadCode}\",\"{l.FullName}\",\"{l.Email}\",\"{l.Phone}\",\"{l.Company}\",\"{l.Source}\",\"{l.Status}\",\"{l.EstimatedValue}\",\"{l.AssignedToUser?.FullName}\",\"{l.CreatedAt:yyyy-MM-dd HH:mm}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Leads_Export_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private async Task<bool> IsAuthorizedForLeadAsync(Lead lead)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Manager"))
            {
                return true;
            }

            if (User.IsInRole("SalesExecutive"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                return currentUser != null && lead.AssignedToUserId == currentUser.Id;
            }

            return false;
        }

        private async Task PopulateUsersDropdownAsync(string? selectedUserId = null)
        {
            var salesUsers = await _userManager.GetUsersInRoleAsync("SalesExecutive");
            var managers = await _userManager.GetUsersInRoleAsync("Manager");
            var admins = await _userManager.GetUsersInRoleAsync("Admin");

            var allStaff = salesUsers
                .Concat(managers)
                .Concat(admins)
                .Where(u => u.IsActive)
                .DistinctBy(u => u.Id)
                .OrderBy(u => u.FullName)
                .ToList();

            ViewBag.AssignedToUserId = new SelectList(allStaff, "Id", "FullName", selectedUserId);
        }

        private async Task<bool> LeadExistsAsync(int id)
        {
            return await _context.Leads.AnyAsync(e => e.Id == id);
        }
    }
}
