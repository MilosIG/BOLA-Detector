using BolaDetector.Domain;

namespace BolaDetector.Application;

public interface IRecordedWriteEvidencePreparer
{
    RecordedWriteEvidence Prepare(string resourceId, ResourceSnapshot before, ResourceSnapshot after,
        string protectedField, string intendedValue);
}
