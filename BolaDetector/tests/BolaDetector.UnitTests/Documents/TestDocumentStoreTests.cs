using BolaTestApi.Documents;
using NUnit.Framework;

namespace BolaDetector.UnitTests;

public class TestDocumentStoreTests
{
    [Test]
    public void CreatedDocumentsHaveIndependentLifecyclesAndLeaveFixturesIntact()
    {
        var store = new TestDocumentStore();
        var first = store.Create("alice", "First");
        var second = store.Create("alice", "Second");

        Assert.That(first.Id, Is.Not.EqualTo(second.Id));
        Assert.That(store.Find(first.Id), Is.Null);
        Assert.That(store.Update(first.Id, "alice", "Changed"), Is.EqualTo(DocumentMutationResult.Applied));
        Assert.That(store.FindCreated(first.Id), Is.EqualTo(first with { Title = "Changed" }));
        Assert.That(store.Delete(first.Id, "alice"), Is.EqualTo(DocumentMutationResult.Applied));
        Assert.That(store.FindCreated(first.Id), Is.Null);
        Assert.That(store.FindCreated(second.Id), Is.EqualTo(second));
        Assert.That(store.Delete("doc-1", "alice"), Is.EqualTo(DocumentMutationResult.NotFound));
        Assert.That(store.Find("doc-1")?.Title, Is.EqualTo("Alice private document"));
    }

    [Test]
    public void ForeignIdentityCannotMutateCreatedDocument()
    {
        var store = new TestDocumentStore();
        var document = store.Create("alice", "Original");

        Assert.That(store.Update(document.Id, "bob", "Changed"), Is.EqualTo(DocumentMutationResult.Forbidden));
        Assert.That(store.Delete(document.Id, "bob"), Is.EqualTo(DocumentMutationResult.Forbidden));
        Assert.That(store.FindCreated(document.Id), Is.EqualTo(document));
    }
}
