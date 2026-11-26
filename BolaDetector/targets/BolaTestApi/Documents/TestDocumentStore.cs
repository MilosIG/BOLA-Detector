namespace BolaTestApi.Documents;

internal sealed record CreationRecord(Guid Key, string Id, string Owner, string Organization, string IntentDigest);

public sealed class TestDocumentStore
{
    internal const string Organization = "org-a";
    private readonly TestDocument[] fixtures =
    [
        new("doc-1", "alice", "org-a", "Alice private document"),
        new("doc-2", "bob", "org-a", "Bob private document")
    ];

    private readonly Dictionary<string, TestDocument> documents = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Owner, string Organization, Guid Key), CreationRecord> creations = new();
    private readonly object gate = new();

    public TestDocument? Find(string id) => fixtures.SingleOrDefault(document => document.Id == id);

    public TestDocument Create(string owner, string title)
    {
        lock (gate)
            return CreateDocument(owner, title);
    }

    internal (CreationRecord Record, bool Conflict, bool Deleted) CreateCorrelated(string owner, Guid key, string title)
    {
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(title)));
        lock (gate)
        {
            var scope = (owner, Organization, key);
            if (creations.TryGetValue(scope, out var existing))
                return (existing, existing.IntentDigest != digest, !documents.ContainsKey(existing.Id));

            var document = CreateDocument(owner, title);
            var record = new CreationRecord(key, document.Id, owner, document.Organization, digest);
            creations.Add(scope, record);
            return (record, false, false);
        }
    }

    internal (CreationRecord? Record, bool Deleted) Recover(string owner, Guid key)
    {
        lock (gate)
        {
            var record = creations.GetValueOrDefault((owner, Organization, key));
            return (record, record is not null && !documents.ContainsKey(record.Id));
        }
    }

    private TestDocument CreateDocument(string owner, string title)
    {
        string id;
        do
        {
            id = Guid.NewGuid().ToString("N");
        } while (documents.ContainsKey(id) || creations.Values.Any(record => record.Id == id));

        var document = new TestDocument(id, owner, Organization, title);
        documents.Add(id, document);
        return document;
    }

    public TestDocument? FindCreated(string id)
    {
        lock (gate)
            return documents.GetValueOrDefault(id);
    }

    public DocumentMutationResult Update(string id, string owner, string title)
    {
        lock (gate)
        {
            if (!documents.TryGetValue(id, out var document))
                return DocumentMutationResult.NotFound;
            if (document.Owner != owner)
                return DocumentMutationResult.Forbidden;

            documents[id] = document with { Title = title };
            return DocumentMutationResult.Applied;
        }
    }

    public DocumentMutationResult Delete(string id, string owner)
    {
        lock (gate)
        {
            if (!documents.TryGetValue(id, out var document))
                return DocumentMutationResult.NotFound;
            if (document.Owner != owner)
                return DocumentMutationResult.Forbidden;

            documents.Remove(id);
            return DocumentMutationResult.Applied;
        }
    }
}
