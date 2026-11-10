using System.Net;
using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;
namespace BolaDetector.UnitTests;

public class OrganizationAuthorizationTests
{
    [Test]
    public void DifferentOrganizationIsDeniedEvenForOwner()
    {
        var identity = Config().Identities[0].Identity;
        var resource = Config().Resources[0] with { OrganizationId = Guid.NewGuid() };
        Assert.That(new AuthorizationOracle().IsAllowed(identity, resource, Config().Rules[0]), Is.False);
    }
}
