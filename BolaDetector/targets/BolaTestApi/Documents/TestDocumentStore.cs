namespace BolaTestApi.Documents;

public sealed class TestDocumentStore
{
    private readonly TestDocument[] documents =
    [
        new("doc-1", "alice", "org-a", "Alice private document"),
        new("doc-2", "bob", "org-a", "Bob private document")
    ];

    public TestDocument? Find(string id) => documents.SingleOrDefault(d => d.Id == id);
}
