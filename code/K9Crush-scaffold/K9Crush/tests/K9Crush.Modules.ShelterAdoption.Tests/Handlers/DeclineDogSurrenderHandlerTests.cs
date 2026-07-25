using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.DeclineDogSurrender;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - DeclineDogSurrenderHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class DeclineDogSurrenderHandlerTests
{
    private static DogSurrenderRequest BuildUnderReview()
    {
        var request = DogSurrenderRequest.RequestNew(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy").DogSurrenderRequest;
        request.Review();
        return request;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_DeclinesAndPersists()
    {
        var surrenderRequest = BuildUnderReview();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out var stream);

        var result = await DeclineDogSurrenderHandler.Handle(
            surrenderRequest.Id, new DeclineDogSurrenderRequest("Outside current intake capacity"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<DeclineDogSurrenderResponse>>();
        surrenderRequest.Status.Should().Be(SurrenderRequestStatus.Declined);
        surrenderRequest.DeclineReason.Should().Be("Outside current intake capacity");
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.DogSurrenderDeclinedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var surrenderRequestId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogSurrenderRequest>(surrenderRequestId, null, out _);

        var result = await DeclineDogSurrenderHandler.Handle(
            surrenderRequestId, new DeclineDogSurrenderRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var surrenderRequest = DogSurrenderRequest.RequestNew(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy").DogSurrenderRequest; // Requested
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out _);

        var result = await DeclineDogSurrenderHandler.Handle(
            surrenderRequest.Id, new DeclineDogSurrenderRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
