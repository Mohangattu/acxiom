using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Models;

namespace AcxiomCrm.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Lead> Leads => Set<Lead>();
        public DbSet<Opportunity> Opportunities => Set<Opportunity>();
        public DbSet<FollowUp> FollowUps => Set<FollowUp>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Customer -> AssignedUser
            builder.Entity<Customer>()
                .HasOne(c => c.AssignedToUser)
                .WithMany()
                .HasForeignKey(c => c.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Lead -> AssignedUser
            builder.Entity<Lead>()
                .HasOne(l => l.AssignedToUser)
                .WithMany()
                .HasForeignKey(l => l.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Lead -> ConvertedCustomer
            builder.Entity<Lead>()
                .HasOne(l => l.ConvertedCustomer)
                .WithMany()
                .HasForeignKey(l => l.ConvertedCustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Opportunity -> Customer
            builder.Entity<Opportunity>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Opportunities)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Opportunity -> AssignedUser
            builder.Entity<Opportunity>()
                .HasOne(o => o.AssignedToUser)
                .WithMany()
                .HasForeignKey(o => o.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // FollowUp -> Customer
            builder.Entity<FollowUp>()
                .HasOne(f => f.Customer)
                .WithMany(c => c.FollowUps)
                .HasForeignKey(f => f.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // FollowUp -> Lead
            builder.Entity<FollowUp>()
                .HasOne(f => f.Lead)
                .WithMany(l => l.FollowUps)
                .HasForeignKey(f => f.LeadId)
                .OnDelete(DeleteBehavior.Cascade);

            // FollowUp -> Opportunity
            builder.Entity<FollowUp>()
                .HasOne(f => f.Opportunity)
                .WithMany(o => o.FollowUps)
                .HasForeignKey(f => f.OpportunityId)
                .OnDelete(DeleteBehavior.Cascade);

            // FollowUp -> AssignedUser
            builder.Entity<FollowUp>()
                .HasOne(f => f.AssignedToUser)
                .WithMany()
                .HasForeignKey(f => f.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
