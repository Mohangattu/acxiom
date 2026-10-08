using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Models;

namespace AcxiomCrm.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Ensure SQLite Customers table has CustomerCode, State, CreatedBy columns if upgraded from Stage 1
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Customers ADD COLUMN CustomerCode TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Customers ADD COLUMN State TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Customers ADD COLUMN CreatedBy TEXT NULL;"); } catch { }

            // Ensure SQLite Leads table has LeadCode and LeadName columns
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Leads ADD COLUMN LeadCode TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Leads ADD COLUMN LeadName TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE Leads SET LeadCode = 'LEAD-000' || Id WHERE LeadCode IS NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE Leads SET LeadName = TRIM(FirstName || ' ' || LastName) WHERE LeadName IS NULL;"); } catch { }

            // Ensure SQLite Opportunities table has OpportunityCode, CreatedBy, WinLossReason, Source columns
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Opportunities ADD COLUMN OpportunityCode TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Opportunities ADD COLUMN CreatedBy TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Opportunities ADD COLUMN WinLossReason TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Opportunities ADD COLUMN Source TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE Opportunities SET OpportunityCode = 'OPP-000' || Id WHERE OpportunityCode IS NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE Opportunities SET CreatedBy = 'System' WHERE CreatedBy IS NULL;"); } catch { }

            // Backfill existing customers if columns were just added
            try
            {
                var existingCustomers = await context.Customers.ToListAsync();
                bool modified = false;
                foreach (var c in existingCustomers)
                {
                    if (string.IsNullOrEmpty(c.CustomerCode))
                    {
                        c.CustomerCode = $"CUST-{c.Id:D4}";
                        modified = true;
                    }
                    if (string.IsNullOrEmpty(c.CreatedBy))
                    {
                        c.CreatedBy = "System";
                        modified = true;
                    }
                    if (string.IsNullOrEmpty(c.State))
                    {
                        c.State = "CA";
                        modified = true;
                    }
                }
                if (modified)
                {
                    await context.SaveChangesAsync();
                }
            }
            catch { }

            // Backfill existing leads if columns were just added
            try
            {
                var existingLeads = await context.Leads.ToListAsync();
                bool modified = false;
                foreach (var l in existingLeads)
                {
                    if (string.IsNullOrEmpty(l.LeadCode))
                    {
                        l.LeadCode = $"LEAD-{l.Id:D4}";
                        modified = true;
                    }
                    if (string.IsNullOrEmpty(l.LeadName))
                    {
                        l.LeadName = $"{l.FirstName} {l.LastName}".Trim();
                        modified = true;
                    }
                }
                if (modified)
                {
                    await context.SaveChangesAsync();
                }
            }
            catch { }

            // 1. Seed Roles: Exactly Admin, Manager, SalesExecutive
            string[] roleNames = { "Admin", "Manager", "SalesExecutive" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Default Test Users (One for each role)
            var adminEmail = "admin@crm.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "John Admin",
                    Department = "Administration",
                    EmailConfirmed = true,
                    IsActive = true,
                    LockoutEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            var managerEmail = "manager@crm.com";
            var managerUser = await userManager.FindByEmailAsync(managerEmail);
            if (managerUser == null)
            {
                managerUser = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    FullName = "Sarah Connor",
                    Department = "Sales Management",
                    EmailConfirmed = true,
                    IsActive = true,
                    LockoutEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(managerUser, "Manager@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(managerUser, "Manager");
                }
            }

            var salesEmail = "sales@crm.com";
            var salesUser = await userManager.FindByEmailAsync(salesEmail);
            if (salesUser == null)
            {
                salesUser = new ApplicationUser
                {
                    UserName = salesEmail,
                    Email = salesEmail,
                    FullName = "Michael Scott",
                    Department = "Direct Sales",
                    EmailConfirmed = true,
                    IsActive = true,
                    LockoutEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(salesUser, "Sales@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(salesUser, "SalesExecutive");
                }
            }

            // 3. Seed Sample Customers if empty
            if (!context.Customers.Any())
            {
                var customers = new List<Customer>
                {
                    new Customer
                    {
                        Name = "TechCorp Solutions",
                        Email = "contact@techcorp.com",
                        Phone = "+1 (555) 234-5678",
                        Company = "TechCorp Inc.",
                        Address = "100 Tech Blvd, Suite 400",
                        City = "San Francisco",
                        Industry = "Software & IT",
                        Status = CustomerStatus.Active,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddMonths(-3)
                    },
                    new Customer
                    {
                        Name = "Apex Financial Group",
                        Email = "info@apexfinancial.com",
                        Phone = "+1 (555) 876-5432",
                        Company = "Apex Capital LLC",
                        Address = "750 Wall Street",
                        City = "New York",
                        Industry = "Finance & Banking",
                        Status = CustomerStatus.Active,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    },
                    new Customer
                    {
                        Name = "Horizon Healthcare",
                        Email = "partners@horizonhealth.org",
                        Phone = "+1 (555) 432-1098",
                        Company = "Horizon Health Systems",
                        Address = "420 Medical Parkway",
                        City = "Boston",
                        Industry = "Healthcare",
                        Status = CustomerStatus.Active,
                        AssignedToUserId = managerUser.Id,
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    },
                    new Customer
                    {
                        Name = "Nexus Global Logistics",
                        Email = "operations@nexuslogistics.com",
                        Phone = "+1 (555) 345-6789",
                        Company = "Nexus Freight Co.",
                        Address = "99 Supply Chain Way",
                        City = "Chicago",
                        Industry = "Logistics",
                        Status = CustomerStatus.Pending,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    },
                    new Customer
                    {
                        Name = "Vertex Retailers",
                        Email = "purchasing@vertexretail.com",
                        Phone = "+1 (555) 901-2345",
                        Company = "Vertex Chain Stores",
                        Address = "88 Market Square",
                        City = "Seattle",
                        Industry = "Retail & Ecommerce",
                        Status = CustomerStatus.Inactive,
                        AssignedToUserId = adminUser.Id,
                        CreatedAt = DateTime.UtcNow.AddMonths(-4)
                    }
                };

                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();

                // 4. Seed Sample Leads
                var leads = new List<Lead>
                {
                    new Lead
                    {
                        FirstName = "Alice",
                        LastName = "Johnson",
                        Email = "alice.j@innovatetech.io",
                        Phone = "+1 (555) 111-2222",
                        Company = "InnovateTech",
                        Source = "Website Form",
                        Status = LeadStatus.New,
                        Notes = "Interested in CRM enterprise integration and multi-user license.",
                        EstimatedValue = 25000,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-4)
                    },
                    new Lead
                    {
                        FirstName = "Robert",
                        LastName = "Davis",
                        Email = "rdavis@blueskyventures.com",
                        Phone = "+1 (555) 333-4444",
                        Company = "BlueSky Ventures",
                        Source = "Referral",
                        Status = LeadStatus.Contacted,
                        Notes = "Had initial introductory call. Demo scheduled for next Tuesday.",
                        EstimatedValue = 50000,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    },
                    new Lead
                    {
                        FirstName = "Emily",
                        LastName = "Clark",
                        Email = "emily.clark@pioneermedia.com",
                        Phone = "+1 (555) 555-6666",
                        Company = "Pioneer Media Group",
                        Source = "Exhibition / Conference",
                        Status = LeadStatus.Qualified,
                        Notes = "Budget approved, evaluating technical specifications.",
                        EstimatedValue = 40000,
                        AssignedToUserId = managerUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    },
                    new Lead
                    {
                        FirstName = "Carlos",
                        LastName = "Rodriguez",
                        Email = "carlos@solardynamics.org",
                        Phone = "+1 (555) 777-8888",
                        Company = "Solar Dynamics",
                        Source = "Cold Call",
                        Status = LeadStatus.Lost,
                        Notes = "Decided to postpone technology upgrades to Q4 next year.",
                        EstimatedValue = 18000,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-30)
                    }
                };

                context.Leads.AddRange(leads);
                await context.SaveChangesAsync();

                // 5. Seed Sample Opportunities
                var cTechCorp = customers[0];
                var cApex = customers[1];
                var cHorizon = customers[2];

                var opportunities = new List<Opportunity>
                {
                    new Opportunity
                    {
                        Title = "Cloud Infrastructure Modernization",
                        CustomerId = cTechCorp.Id,
                        Amount = 45000,
                        Stage = OpportunityStage.Proposal,
                        Probability = 60,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(14),
                        Notes = "Proposal submitted. Awaiting feedback from CTO.",
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-25)
                    },
                    new Opportunity
                    {
                        Title = "Core Banking Analytics Engine",
                        CustomerId = cApex.Id,
                        Amount = 120000,
                        Stage = OpportunityStage.Negotiation,
                        Probability = 80,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(7),
                        Notes = "Final contract terms under legal review.",
                        AssignedToUserId = managerUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-40)
                    },
                    new Opportunity
                    {
                        Title = "Annual Support & Maintenance Renewal",
                        CustomerId = cTechCorp.Id,
                        Amount = 15000,
                        Stage = OpportunityStage.ClosedWon,
                        Probability = 100,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(-5),
                        Notes = "Deal closed successfully and contract signed.",
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    },
                    new Opportunity
                    {
                        Title = "EMR Integration Software Package",
                        CustomerId = cHorizon.Id,
                        Amount = 85000,
                        Stage = OpportunityStage.Prospecting,
                        Probability = 20,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(45),
                        Notes = "Initial requirements gathering in progress.",
                        AssignedToUserId = managerUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-12)
                    }
                };

                context.Opportunities.AddRange(opportunities);
                await context.SaveChangesAsync();

                // 6. Seed FollowUps
                var followUps = new List<FollowUp>
                {
                    new FollowUp
                    {
                        Subject = "Conduct Technical Demo for blueSky team",
                        Type = FollowUpType.Meeting,
                        DueDate = DateTime.UtcNow.AddDays(2).Date.AddHours(14),
                        Status = FollowUpStatus.Pending,
                        Priority = PriorityLevel.High,
                        Notes = "Prepare presentation slide deck on API integrations.",
                        LeadId = leads[1].Id,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    },
                    new FollowUp
                    {
                        Subject = "Contract Legal Terms Review Call",
                        Type = FollowUpType.Call,
                        DueDate = DateTime.UtcNow.AddDays(1).Date.AddHours(11),
                        Status = FollowUpStatus.Pending,
                        Priority = PriorityLevel.Urgent,
                        Notes = "Discuss Section 4 indemnity clause with Apex General Counsel.",
                        OpportunityId = opportunities[1].Id,
                        CustomerId = cApex.Id,
                        AssignedToUserId = managerUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    },
                    new FollowUp
                    {
                        Subject = "Send updated proposal pricing table",
                        Type = FollowUpType.Email,
                        DueDate = DateTime.UtcNow.AddHours(4),
                        Status = FollowUpStatus.Pending,
                        Priority = PriorityLevel.Medium,
                        Notes = "Include 5% volume discount as agreed in previous meeting.",
                        OpportunityId = opportunities[0].Id,
                        CustomerId = cTechCorp.Id,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    },
                    new FollowUp
                    {
                        Subject = "Quarterly Check-in Call",
                        Type = FollowUpType.Call,
                        DueDate = DateTime.UtcNow.AddDays(-3).Date.AddHours(10),
                        Status = FollowUpStatus.Completed,
                        Priority = PriorityLevel.Low,
                        Notes = "Client is satisfied with current support service.",
                        CustomerId = cTechCorp.Id,
                        AssignedToUserId = salesUser.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-10),
                        CompletedAt = DateTime.UtcNow.AddDays(-3).Date.AddHours(11)
                    }
                };

                context.FollowUps.AddRange(followUps);
                await context.SaveChangesAsync();

                // 7. Seed Initial Audit Logs
                var auditLogs = new List<AuditLog>
                {
                    new AuditLog
                    {
                        EntityName = "System",
                        EntityId = "Init",
                        Action = "DatabaseSeed",
                        Details = "System database initialized with default roles and sample data.",
                        UserName = "System",
                        Timestamp = DateTime.UtcNow.AddDays(-30)
                    },
                    new AuditLog
                    {
                        EntityName = "Customer",
                        EntityId = cTechCorp.Id.ToString(),
                        Action = "Create",
                        Details = "Customer TechCorp Solutions created by Michael Scott.",
                        UserId = salesUser.Id,
                        UserName = salesUser.FullName,
                        Timestamp = DateTime.UtcNow.AddMonths(-3)
                    },
                    new AuditLog
                    {
                        EntityName = "Opportunity",
                        EntityId = opportunities[2].Id.ToString(),
                        Action = "StageChange",
                        Details = "Opportunity 'Annual Support & Maintenance Renewal' changed stage to ClosedWon.",
                        UserId = salesUser.Id,
                        UserName = salesUser.FullName,
                        Timestamp = DateTime.UtcNow.AddDays(-5)
                    }
                };

                context.AuditLogs.AddRange(auditLogs);
                await context.SaveChangesAsync();
            }
        }
    }
}
