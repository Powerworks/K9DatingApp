using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.SubmitAdditionalSurrenderDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SubmitAdditionalSurrenderDetailsHandler
/// only calls FetchForWriting/AppendOne/SaveChangesAsync, so
/// IDocumentSession mocks cleanly here (ADR-031).
/// </summary>
public class SubmitAdditionalSurrenderDetailsHandlerTests
{
    private static readonly Guid RequestedByOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static DogSurrenderRequest BuildAdditionalDetailsRequested()
    {
        var request = DogSurrenderRequest.RequestNew(
            RequestedByOwnerId, "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy").DogSurrenderRequest;
        request.Review();
        request.RequestAdditionalDetails("Please confirm vaccination records");
        return request;
    }

    [Fact]
    public async Task Handle_WhenAdditionalDetailsRequestedAndCallerOwnsIt_SubmitsAndPersists()
    {
        var surrenderRequest = BuildAdditionalDetailsRequested();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out var stream);

        var result = await SubmitAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, BuildUser(RequestedByOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SubmitAdditionalSurrenderDetailsResponse>>();
        surrenderRequest.Status.Should().Be(SurrenderRequestStatus.UnderReview);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.AdditionalSurrenderDetailsSubmittedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var surrenderRequestId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogSurrenderRequest>(surrenderRequestId, null, out _);

        var result = await SubmitAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequestId, BuildUser(RequestedByOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDidNotRequestTheSurrender_ReturnsForbid()
    {
        var surrenderRequest = BuildAdditionalDetailsRequested();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out _);

        var result = await SubmitAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenNotInAdditionalDetailsRequestedStatus_ReturnsConflict()
    {
        var surrenderRequest = DogSurrenderRequest.RequestNew(
            RequestedByOwnerId, "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy").DogSurrenderRequest; // Requested
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out _);

        var result = await SubmitAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, BuildUser(RequestedByOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
