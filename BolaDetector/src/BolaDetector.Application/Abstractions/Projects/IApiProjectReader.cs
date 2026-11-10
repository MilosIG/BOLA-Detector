using BolaDetector.Domain;
namespace BolaDetector.Application;

public interface IApiProjectReader
{
    Task<IReadOnlyList<ApiProject>> ReadAsync(CancellationToken cancellationToken);
}
