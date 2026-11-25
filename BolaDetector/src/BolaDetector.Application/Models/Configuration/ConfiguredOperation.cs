using BolaDetector.Domain;

namespace BolaDetector.Application;

public sealed record ConfiguredOperation(ApiOperation Operation, string ResourceType, string Action);
