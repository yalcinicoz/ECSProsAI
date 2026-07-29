using ECSPros.Requests.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Services;

public interface IRequestsDbContext
{
    DbSet<ProjectRequest> ProjectRequests { get; }
    DbSet<RequestActivity> RequestActivities { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
