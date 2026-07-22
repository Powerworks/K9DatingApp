using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.AcceptDogSurrender;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - AcceptDogSurrenderHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class AcceptDogSurrenderHandlerTests
{
    private static DogSurrenderRequest BuildUnderReview()
    {
        var request = DogSurrenderRequest.Request(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle, a little shy", "Healthy");
        request.Review();
        return request;
    }

    private static ShelterAccount BuildActivatedShelterAccount()
    {
        var shelterAccount = ShelterAccount.Create(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        shelterAccount.Verify();
        shelterAccount.Activate();
        return shelterAccount;
    }

    [Fact]
    public async Task Handle_WhenUnderReviewAndShelterIsActivated_AcceptsAddsListingAndPersistsBoth()
    {
        var surrenderRequest = BuildUnderReview();
        var shelterAccount = BuildActivatedShelterAccount();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequest.Id, new AcceptDogSurrenderRequest(shelterAccount.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<AcceptDogSurrenderResponse>>();
        var response = ((Ok<AcceptDogSurrenderResponse>)result.Result).Value!;
        response.Status.Should().Be(nameof(SurrenderRequestStatus.Accepted));
        response.DogListingId.Should().NotBeEmpty();

        session.Received(1).Store(Arg.Is<DogSurrenderRequest[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == SurrenderRequestStatus.Accepted));
        session.Received(1).Store(Arg.Is<DogListing[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].ShelterAccountId == shelterAccount.Id &&
            arr[0].Name == "Cooper" && arr[0].Breed == "Terrier mix" && arr[0].Bio == "Gentle, a little shy" &&
            arr[0].Status == DogListingStatus.NotReadyYet));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSurrenderRequestDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var surrenderRequestId = Guid.NewGuid();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequestId, Arg.Any<CancellationToken>()).Returns((DogSurrenderRequest?)null);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequestId, new AcceptDogSurrenderRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var surrenderRequest = DogSurrenderRequest.Request(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy"); // Requested
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequest.Id, new AcceptDogSurrenderRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var surrenderRequest = BuildUnderReview();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);
        session.LoadAsync<ShelterAccount>(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ShelterAccount?)null);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequest.Id, new AcceptDogSurrenderRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenShelterAccountIsNotActivated_ReturnsConflict()
    {
        var surrenderRequest = BuildUnderReview();
        var shelterAccount = ShelterAccount.Create(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()); // Requested, not Created
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequest.Id, new AcceptDogSurrenderRequest(shelterAccount.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
