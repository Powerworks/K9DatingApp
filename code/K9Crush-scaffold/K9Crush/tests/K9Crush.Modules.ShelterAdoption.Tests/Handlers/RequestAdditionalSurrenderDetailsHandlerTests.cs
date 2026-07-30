using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RequestAdditionalSurrenderDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RequestAdditionalSurrenderDetailsHandler
/// only calls FetchForWriting/AppendOne/SaveChangesAsync, so
/// IDocumentSession mocks cleanly here (ADR-031).
/// </summary>
public class RequestAdditionalSurrenderDetailsHandlerTests
{
    private static DogSurrenderRequest BuildUnderReview()
    {
        var request = DogSurrenderRequest.RequestNew(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy").DogSurrenderRequest;
        request.Review();
        return request;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_RequestsAdditionalDetailsAndPersists()
    {
        var surrenderRequest = BuildUnderReview();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out var stream);

        var result = await RequestAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, new RequestAdditionalSurrenderDetailsRequest("Please confirm vaccination records"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RequestAdditionalSurrenderDetailsResponse>>();
        surrenderRequest.Status.Should().Be(SurrenderRequestStatus.AdditionalDetailsRequested);
        surrenderRequest.AdditionalDetailsRequestReason.Should().Be("Please confirm vaccination records");
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.AdditionalSurrenderDetailsRequestedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var surrenderRequestId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogSurrenderRequest>(surrenderRequestId, null, out _);

        var result = await RequestAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequestId, new RequestAdditionalSurrenderDetailsRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var surrenderRequest = DogSurrenderRequest.RequestNew(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy").DogSurrenderRequest; // Requested, not UnderReview
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out _);

        var result = await RequestAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, new RequestAdditionalSurrenderDetailsRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
