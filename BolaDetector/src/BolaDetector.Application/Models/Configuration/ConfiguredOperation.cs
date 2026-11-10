using BolaDetector.Domain;

namespace BolaDetector.Application;

public record ConfiguredOperation(ApiOperation Operation, string ResourceType, string Action);
