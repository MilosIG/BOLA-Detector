using BolaDetector.Application;
using BolaDetector.Domain;
using Microsoft.EntityFrameworkCore;
namespace BolaDetector.Infrastructure;

public sealed class EfApiProjectReader(BolaDbContext database) : IApiProjectReader
{
    public async Task<IReadOnlyList<ApiProject>> ReadAsync(CancellationToken cancellationToken) =>
        await database.ApiProjects.AsNoTracking().ToListAsync(cancellationToken);
}
