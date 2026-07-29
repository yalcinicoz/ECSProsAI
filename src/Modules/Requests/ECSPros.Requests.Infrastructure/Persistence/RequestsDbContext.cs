using ECSPros.Requests.Application.Services;
using ECSPros.Requests.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Infrastructure.Persistence;

public class RequestsDbContext : DbContext, IRequestsDbContext
{
    public RequestsDbContext(DbContextOptions<RequestsDbContext> options) : base(options) { }

    public DbSet<ProjectRequest> ProjectRequests => Set<ProjectRequest>();
    public DbSet<RequestActivity> RequestActivities => Set<RequestActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("requests");

        modelBuilder.Entity<ProjectRequest>(e =>
        {
            e.ToTable("project_requests");
            e.HasIndex(r => r.Code).IsUnique();
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.AssignedTo);
            e.HasIndex(r => r.RequestedBy);
            e.Property(r => r.Code).HasMaxLength(20);
            e.Property(r => r.Title).HasMaxLength(300);
            e.Property(r => r.Category).HasMaxLength(50);
            e.Property(r => r.Priority).HasMaxLength(20);
            e.Property(r => r.Status).HasMaxLength(30);
            e.Property(r => r.RequestedByName).HasMaxLength(200);
            e.Property(r => r.AssignedToName).HasMaxLength(200);
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        modelBuilder.Entity<RequestActivity>(e =>
        {
            e.ToTable("project_request_activities");
            e.HasIndex(a => a.RequestId);
            e.Property(a => a.ActivityType).HasMaxLength(30);
            e.Property(a => a.UserName).HasMaxLength(200);
            e.Property(a => a.Attachments).HasColumnType("jsonb");
            e.HasOne(a => a.Request)
                .WithMany(r => r.Activities)
                .HasForeignKey(a => a.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(a => !a.IsDeleted);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
