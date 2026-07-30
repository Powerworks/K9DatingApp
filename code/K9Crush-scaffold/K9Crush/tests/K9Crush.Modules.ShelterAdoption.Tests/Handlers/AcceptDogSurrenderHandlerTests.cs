using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.AcceptDogSurrender;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - AcceptDogSurrenderHandler is a genuine
/// two-stream write: FetchForWriting/AppendOne against DogSurrenderRequest
/// plus Events.StartStream for a brand-new DogListing, and a plain
/// LoadAsync against ShelterAccount for the activation check (ADR-031).
/// </summary>
public class AcceptDogSurrenderHandlerTests
{
    private static DogSurrenderRequest BuildUnderReview()
    {
        var request = DogSurrenderRequest.RequestNew(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle, a little shy", "Healthy").DogSurrenderRequest;
        request.Review();
        return request;
    }

    private static ShelterAccount BuildActivatedShelterAccount()
    {
        var shelterAccount = ShelterAccount.RequestNew(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        shelterAccount.Verify();
        shelterAccount.Activate();
        return shelterAccount;
    }

    [Fact]
    public async Task Handle_WhenUnderReviewAndShelterIsActivated_AcceptsAddsListingAndPersistsBoth()
    {
        var surrenderRequest = BuildUnderReview();
        var shelterAccount = BuildActivatedShelterAccount();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out var stream);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequest.Id, new AcceptDogSurrenderRequest(shelterAccount.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<AcceptDogSurrenderResponse>>();
        var response = ((Ok<AcceptDogSurrenderResponse>)result.Result).Value!;
        response.Status.Should().Be(nameof(SurrenderRequestStatus.Accepted));
        response.DogListingId.Should().NotBeEmpty();

        surrenderRequest.Status.Should().Be(SurrenderRequestStatus.Accepted);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.DogSurrenderAcceptedV1)));

        session.Events.Received(1).StartStream<DogListing>(
            response.DogListingId,
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingAddedV1)events[0]).ShelterAccountId == shelterAccount.Id
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingAddedV1)events[0]).Name == "Cooper"
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingAddedV1)events[0]).Breed == "Terrier mix"
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingAddedV1)events[0]).Bio == "Gentle, a little shy"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSurrenderRequestDoesNotExist_ReturnsNotFound()
    {
        var surrenderRequestId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogSurrenderRequest>(surrenderRequestId, null, out _);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequestId, new AcceptDogSurrenderRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var surrenderRequest = DogSurrenderRequest.RequestNew(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy").DogSurrenderRequest; // Requested
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out _);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequest.Id, new AcceptDogSurrenderRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var surrenderRequest = BuildUnderReview();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out _);
        session.LoadAsync<ShelterAccount>(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ShelterAccount?)null);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequest.Id, new AcceptDogSurrenderRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenShelterAccountIsNotActivated_ReturnsConflict()
    {
        var surrenderRequest = BuildUnderReview();
        var shelterAccount = ShelterAccount.RequestNew(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount; // Requested, not Created
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out _);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AcceptDogSurrenderHandler.Handle(
            surrenderRequest.Id, new AcceptDogSurrenderRequest(shelterAccount.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
