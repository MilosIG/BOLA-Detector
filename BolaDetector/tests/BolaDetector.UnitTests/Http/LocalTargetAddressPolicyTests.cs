using System.Net;
using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;
namespace BolaDetector.UnitTests;

public class LocalTargetAddressPolicyTests
{
    [TestCase("http://example.com:5081")]
    [TestCase("http://127.0.0.1:5082")]
    [TestCase("http://localhost:5081")]
    [TestCase("https://127.0.0.1:5081")]
    public void NonApprovedOriginRejected(string origin) =>
        Assert.Throws<ArgumentException>(() => new LocalTargetAddressPolicy().Build(origin, "/documents/{id}", "a"));
    [TestCase("//example.com/{id}")]
    [TestCase("/documents/{id}?next=http://example.com")]
    [TestCase("/documents/{other}")]
    public void UnsafePathsRejected(string path) =>
        Assert.Throws<ArgumentException>(() => new LocalTargetAddressPolicy().Build("http://127.0.0.1:5081", path, "a"));
    [Test]
    public void IdIsUrlEncoded() => Assert.That(
        new LocalTargetAddressPolicy().Build("http://127.0.0.1:5081", "/documents/{id}", "a/b").AbsolutePath,
        Is.EqualTo("/documents/a%2Fb"));

}
