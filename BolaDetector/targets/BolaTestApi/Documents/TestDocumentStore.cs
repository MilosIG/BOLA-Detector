namespace BolaTestApi.Documents;

public sealed class TestDocumentStore
{
    private readonly TestDocument[] fixtures =
    [
        new("doc-1", "alice", "org-a", "Alice private document"),
        new("doc-2", "bob", "org-a", "Bob private document")
    ];

    private readonly Dictionary<string, TestDocument> documents = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public TestDocument? Find(string id) => fixtures.SingleOrDefault(document => document.Id == id);

    public TestDocument Create(string owner, string title)
    {
        lock (gate)
        {
            string id;
            do
            {
                id = Guid.NewGuid().ToString("N");
            } while (documents.ContainsKey(id));

            var document = new TestDocument(id, owner, "org-a", title);
            documents.Add(id, document);
            return document;
        }
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
