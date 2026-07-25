using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.EditApplicationDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - EditApplicationDetailsHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
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
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<Application>(applicationId, null, out _);

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
        var application = Application.StartDraftNew(ApplicantOwnerId, DogListingId, ShelterAccountId).Application;
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out _);

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
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application; // Status = Pending, not Draft
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out _);

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
        var application = Application.StartDraftNew(ApplicantOwnerId, DogListingId, ShelterAccountId).Application;
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

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
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ApplicationDetailsEditedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
