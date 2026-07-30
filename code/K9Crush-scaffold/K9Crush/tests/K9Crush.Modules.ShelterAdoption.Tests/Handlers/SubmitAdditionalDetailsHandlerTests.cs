using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.SubmitAdditionalDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SubmitAdditionalDetailsHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class SubmitAdditionalDetailsHandlerTests
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

        var result = await SubmitAdditionalDetailsHandler.Handle(applicationId, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheApplicant_ReturnsForbid()
    {
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application;
        application.Review();
        application.RequestAdditionalDetails("Please provide vet references");
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out _);

        var result = await SubmitAdditionalDetailsHandler.Handle(application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenApplicationIsNotReturnedForAlteration_ReturnsConflict()
    {
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application; // Pending, not ReturnedForAlteration
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out _);

        var result = await SubmitAdditionalDetailsHandler.Handle(application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenReturnedForAlterationAndCallerIsApplicant_SubmitsAndPersists()
    {
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application;
        application.Review();
        application.RequestAdditionalDetails("Please provide vet references");
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

        var result = await SubmitAdditionalDetailsHandler.Handle(application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SubmitAdditionalDetailsResponse>>();
        application.Status.Should().Be(ApplicationStatus.UnderReview);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ApplicationAdditionalDetailsSubmittedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
