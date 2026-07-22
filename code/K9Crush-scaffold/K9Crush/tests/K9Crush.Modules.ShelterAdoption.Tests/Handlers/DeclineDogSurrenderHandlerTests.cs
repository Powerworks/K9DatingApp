using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.DeclineDogSurrender;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - DeclineDogSurrenderHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class DeclineDogSurrenderHandlerTests
{
    private static DogSurrenderRequest BuildUnderReview()
    {
        var request = DogSurrenderRequest.Request(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy");
        request.Review();
        return request;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_DeclinesAndPersists()
    {
        var surrenderRequest = BuildUnderReview();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await DeclineDogSurrenderHandler.Handle(
            surrenderRequest.Id, new DeclineDogSurrenderRequest("Outside current intake capacity"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<DeclineDogSurrenderResponse>>();
        session.Received(1).Store(Arg.Is<DogSurrenderRequest[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == SurrenderRequestStatus.Declined &&
            arr[0].DeclineReason == "Outside current intake capacity"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var surrenderRequestId = Guid.NewGuid();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequestId, Arg.Any<CancellationToken>()).Returns((DogSurrenderRequest?)null);

        var result = await DeclineDogSurrenderHandler.Handle(
            surrenderRequestId, new DeclineDogSurrenderRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var surrenderRequest = DogSurrenderRequest.Request(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy"); // Requested
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await DeclineDogSurrenderHandler.Handle(
            surrenderRequest.Id, new DeclineDogSurrenderRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
