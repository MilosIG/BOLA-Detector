using BolaDetector.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace BolaDetector.Infrastructure;

public sealed class BolaDbContext(DbContextOptions<BolaDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<ApiProject> ApiProjects => Set<ApiProject>();
}
