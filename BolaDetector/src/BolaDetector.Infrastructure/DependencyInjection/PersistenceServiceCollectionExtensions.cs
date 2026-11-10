using BolaDetector.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace BolaDetector.Infrastructure;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddBolaPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BolaDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddIdentityApiEndpoints<IdentityUser>().AddEntityFrameworkStores<BolaDbContext>();
        services.AddScoped<IApiProjectReader, EfApiProjectReader>();
        return services;
    }
}
