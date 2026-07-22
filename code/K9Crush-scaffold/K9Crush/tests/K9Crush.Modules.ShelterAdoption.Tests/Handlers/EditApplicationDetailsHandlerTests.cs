using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.EditApplicationDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - EditApplicationDetailsHandler only calls
/// LoadAsync/Store/SaveChangesAsync (no session.Query&lt;T&gt;() LINQ), so
/// IDocumentSession mocks cleanly here.
/// </summary>
public class EditApplicationDetailsHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        var result = await EditApplicationDetailsHandler.Handle(
            applicationId,
            new EditApplicationDetailsRequest("new details"),
            BuildUser(ApplicantOwnerId),
            session,
            CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheApplicant_ReturnsForbid()
    {
        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await EditApplicationDetailsHandler.Handle(
            application.Id,
            new EditApplicationDetailsRequest("new details"),
            BuildUser(Guid.NewGuid()), // a different owner than ApplicantOwnerId
            session,
            CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenApplicationIsNotADraft_ReturnsConflict()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default); // Status = Pending, not Draft
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await EditApplicationDetailsHandler.Handle(
            application.Id,
            new EditApplicationDetailsRequest("new details"),
            BuildUser(ApplicantOwnerId),
            session,
            CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenDraftOwnedByCaller_EditsDetailsAndPersists()
    {
        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await EditApplicationDetailsHandler.Handle(
            application.Id,
            new EditApplicationDetailsRequest("we have a fenced yard"),
            BuildUser(ApplicantOwnerId),
            session,
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditApplicationDetailsResponse>>();
        var ok = (Ok<EditApplicationDetailsResponse>)result.Result;
        ok.Value!.ApplicationId.Should().Be(application.Id);
        ok.Value.LastEditedAt.Should().Be(application.LastEditedAt);

        application.Details.Should().Be("we have a fenced yard");
        session.Received(1).Store(Arg.Is<Application[]>(arr => arr != null && arr.Length == 1 && arr[0] == application));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
