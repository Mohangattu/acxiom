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
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditLog;

        public CustomersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditLogService auditLog)
        {
            _context = context;
            _userManager = userManager;
            _auditLog = auditLog;
        }

        // GET: Customers
        public async Task<IActionResult> Index(string? searchTerm, CustomerStatus? statusFilter, int page = 1)
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            var query = _context.Customers
                .Include(c => c.AssignedToUser)
                .AsQueryable();

            // Search filter across Customer Name, Email, Phone, Company, City, State, CustomerCode
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(c =>
                    (c.Name != null && c.Name.ToLower().Contains(term)) ||
                    (c.Email != null && c.Email.ToLower().Contains(term)) ||
                    (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                    (c.Company != null && c.Company.ToLower().Contains(term)) ||
                    (c.CustomerCode != null && c.CustomerCode.ToLower().Contains(term)) ||
                    (c.City != null && c.City.ToLower().Contains(term)) ||
                    (c.State != null && c.State.ToLower().Contains(term)));
            }

            // Status filter
            if (statusFilter.HasValue)
            {
                query = query.Where(c => c.Status == statusFilter.Value);
            }

            var totalItems = await query.CountAsync();
            var customers = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.TotalItems = totalItems;

            return View(customers);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.AssignedToUser)
                .Include(c => c.Opportunities)
                .Include(c => c.FollowUps.OrderByDescending(f => f.DueDate))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Customers/Create
        public async Task<IActionResult> Create()
        {
            await PopulateUsersDropdownAsync();
            return View(new Customer());
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            // Trim inputs
            customer.Name = customer.Name?.Trim() ?? string.Empty;
            customer.Email = customer.Email?.Trim() ?? string.Empty;
            customer.Phone = customer.Phone?.Trim() ?? string.Empty;

            // 1. Validate Email uniqueness
            if (!string.IsNullOrWhiteSpace(customer.Email) &&
                await _context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "A customer with this email already exists.");
            }

            // 2. Validate Phone uniqueness
            if (!string.IsNullOrWhiteSpace(customer.Phone) &&
                await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
            {
                ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
            }

            if (ModelState.IsValid)
            {
                customer.CreatedAt = DateTime.UtcNow;
                customer.CreatedBy = User.Identity?.Name ?? "System";

                // Auto-generate CustomerCode if blank
                if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                {
                    var maxId = await _context.Customers.MaxAsync(c => (int?)c.Id) ?? 0;
                    customer.CustomerCode = $"CUST-{(maxId + 1):D4}";
                }

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("Customer", customer.Id.ToString(), "Create",
                    $"Created customer '{customer.Name}' ({customer.CustomerCode}) with email {customer.Email}.");

                TempData["SuccessMessage"] = $"Customer '{customer.Name}' created successfully with code {customer.CustomerCode}.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUsersDropdownAsync(customer.AssignedToUserId);
            return View(customer);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            // Role check: SalesExecutive can only edit their assigned customers (or unassigned)
            if (!await CanUserModifyCustomerAsync(customer))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            await PopulateUsersDropdownAsync(customer.AssignedToUserId);
            return View(customer);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            var existingCustomer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (existingCustomer == null)
            {
                return NotFound();
            }

            // Role check: SalesExecutive can only edit their assigned customers (or unassigned)
            if (!await CanUserModifyCustomerAsync(existingCustomer))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            customer.Name = customer.Name?.Trim() ?? string.Empty;
            customer.Email = customer.Email?.Trim() ?? string.Empty;
            customer.Phone = customer.Phone?.Trim() ?? string.Empty;

            // 1. Check duplicate email against other records
            if (!string.IsNullOrWhiteSpace(customer.Email) &&
                await _context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower() && c.Id != id))
            {
                ModelState.AddModelError("Email", "A customer with this email already exists.");
            }

            // 2. Check duplicate phone against other records
            if (!string.IsNullOrWhiteSpace(customer.Phone) &&
                await _context.Customers.AnyAsync(c => c.Phone == customer.Phone && c.Id != id))
            {
                ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    customer.UpdatedAt = DateTime.UtcNow;
                    customer.CreatedAt = existingCustomer.CreatedAt;
                    customer.CreatedBy = existingCustomer.CreatedBy;

                    if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                    {
                        customer.CustomerCode = existingCustomer.CustomerCode ?? $"CUST-{id:D4}";
                    }

                    _context.Update(customer);
                    await _context.SaveChangesAsync();

                    await _auditLog.LogAsync("Customer", customer.Id.ToString(), "Update",
                        $"Updated customer '{customer.Name}' ({customer.CustomerCode}, Status: {customer.Status}).");

                    TempData["SuccessMessage"] = $"Customer '{customer.Name}' updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = customer.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await CustomerExistsAsync(customer.Id))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }

            await PopulateUsersDropdownAsync(customer.AssignedToUserId);
            return View(customer);
        }

        // GET: Customers/Delete/5
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                // Safe deactivation as recommended by the CRM assignment
                customer.Status = CustomerStatus.Inactive;
                customer.UpdatedAt = DateTime.UtcNow;
                _context.Update(customer);
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync("Customer", id.ToString(), "Deactivate",
                    $"Deactivated customer record '{customer.Name}' ({customer.CustomerCode}).");

                TempData["SuccessMessage"] = $"Customer '{customer.Name}' was deactivated successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/ExportCsv
        public async Task<IActionResult> ExportCsv()
        {
            var customers = await _context.Customers
                .Include(c => c.AssignedToUser)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("CustomerId,CustomerCode,CustomerName,Email,Phone,CompanyName,Address,City,State,Industry,Status,CreatedBy,CreatedAt");

            foreach (var c in customers)
            {
                csv.AppendLine($"\"{c.Id}\",\"{c.CustomerCode}\",\"{c.Name}\",\"{c.Email}\",\"{c.Phone}\",\"{c.Company}\",\"{c.Address}\",\"{c.City}\",\"{c.State}\",\"{c.Industry}\",\"{c.Status}\",\"{c.CreatedBy}\",\"{c.CreatedAt:yyyy-MM-dd HH:mm}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Customers_Export_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private async Task<bool> CanUserModifyCustomerAsync(Customer customer)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Manager"))
            {
                return true;
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return false;

            // SalesExecutive can edit unassigned customers or customers assigned to themselves
            if (string.IsNullOrEmpty(customer.AssignedToUserId) || customer.AssignedToUserId == currentUser.Id)
            {
                return true;
            }

            return false;
        }

        private async Task PopulateUsersDropdownAsync(string? selectedUserId = null)
        {
            var users = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.AssignedToUserId = new SelectList(users, "Id", "FullName", selectedUserId);
        }

        private async Task<bool> CustomerExistsAsync(int id)
        {
            return await _context.Customers.AnyAsync(e => e.Id == id);
        }
    }
}
