using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Data;
using AcxiomCrm.Models;
using AcxiomCrm.Models.Dtos;
using AcxiomCrm.Services;

namespace AcxiomCrm.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/customers")]
    public class CustomersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLog;

        public CustomersApiController(ApplicationDbContext context, IAuditLogService auditLog)
        {
            _context = context;
            _auditLog = auditLog;
        }

        // GET: api/CustomersApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers([FromQuery] string? search, [FromQuery] CustomerStatus? status)
        {
            var query = _context.Customers.Include(c => c.AssignedToUser).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(term) || c.Email.ToLower().Contains(term));
            }

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            var list = await query.Select(c => new CustomerDto
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email,
                Phone = c.Phone,
                Company = c.Company,
                Address = c.Address,
                City = c.City,
                Industry = c.Industry,
                Status = c.Status,
                AssignedToUserId = c.AssignedToUserId,
                AssignedToUserName = c.AssignedToUser != null ? c.AssignedToUser.FullName : null,
                CreatedAt = c.CreatedAt
            }).ToListAsync();

            return Ok(list);
        }

        // GET: api/CustomersApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
        {
            var c = await _context.Customers
                .Include(c => c.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (c == null)
            {
                return NotFound(new { message = $"Customer with ID {id} not found." });
            }

            var dto = new CustomerDto
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email,
                Phone = c.Phone,
                Company = c.Company,
                Address = c.Address,
                City = c.City,
                Industry = c.Industry,
                Status = c.Status,
                AssignedToUserId = c.AssignedToUserId,
                AssignedToUserName = c.AssignedToUser?.FullName,
                CreatedAt = c.CreatedAt
            };

            return Ok(dto);
        }

        // POST: api/CustomersApi
        [HttpPost]
        public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
            {
                return BadRequest(new { message = "Customer email already in use." });
            }

            var customer = new Customer
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                Company = dto.Company,
                Address = dto.Address,
                City = dto.City,
                Industry = dto.Industry,
                Status = dto.Status,
                AssignedToUserId = dto.AssignedToUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Customer", customer.Id.ToString(), "ApiCreate",
                $"API created customer '{customer.Name}' ({customer.Email}).");

            dto.Id = customer.Id;
            dto.CreatedAt = customer.CreatedAt;

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, dto);
        }

        // PUT: api/CustomersApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CustomerDto dto)
        {
            if (id != dto.Id)
            {
                return BadRequest(new { message = "ID mismatch." });
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound(new { message = $"Customer with ID {id} not found." });
            }

            customer.Name = dto.Name;
            customer.Email = dto.Email;
            customer.Phone = dto.Phone;
            customer.Company = dto.Company;
            customer.Address = dto.Address;
            customer.City = dto.City;
            customer.Industry = dto.Industry;
            customer.Status = dto.Status;
            customer.AssignedToUserId = dto.AssignedToUserId;
            customer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Customer", id.ToString(), "ApiUpdate",
                $"API updated customer '{customer.Name}'.");

            return NoContent();
        }

        // DELETE: api/CustomersApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound(new { message = $"Customer with ID {id} not found." });
            }

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Customer", id.ToString(), "ApiDelete",
                $"API deleted customer '{customer.Name}'.");

            return NoContent();
        }
    }
}
