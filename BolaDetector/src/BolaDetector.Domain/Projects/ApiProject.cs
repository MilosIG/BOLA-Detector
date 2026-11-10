namespace BolaDetector.Domain;

public sealed class ApiProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string OpenApiDocument { get; set; } = "";
}
