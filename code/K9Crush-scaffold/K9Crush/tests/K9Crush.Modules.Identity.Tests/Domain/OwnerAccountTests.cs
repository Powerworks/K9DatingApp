using FluentAssertions;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of OwnerAccount's factory
/// method and the AccountProfileSettings deletion-saga methods. No mocks,
/// no infra: these only prove "calling this method produces this state
/// change." Deliberately does NOT test calling a method from the "wrong"
/// state (e.g. ConfirmDeletion before RequestDeletion) - OwnerAccount's
/// domain methods don't guard their own preconditions in this codebase,
/// same as every other entity here (see Application.cs's own doc
/// comments). That guarantee belongs to the handler tests instead.
/// </summary>
public class OwnerAccountTests
{
    private static OwnerAccount CreateOwner() =>
        OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow).OwnerAccount;

    [Fact]
    public void CreateNew_WhenCalled_CreatesUnverifiedOwnerWithOwnerRoleAndReturnsTheEvent()
    {
        var supabaseUserId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var (owner, @event) = OwnerAccount.CreateNew(supabaseUserId, " owner@example.com ", createdAt);

        owner.Id.Should().Be(supabaseUserId);
        owner.Email.Should().Be("owner@example.com");
        owner.IsVerified.Should().BeFalse();
        owner.Role.Should().Be(OwnerRole.Owner);
        owner.CreatedAt.Should().Be(createdAt);
        owner.DisplayName.Should().BeNull();
        owner.DeletionRequestedAt.Should().BeNull();
        owner.GracePeriodEndsAt.Should().BeNull();
        owner.IsPermanentlyDeleted.Should().BeFalse();

        @event.SupabaseUserId.Should().Be(supabaseUserId);
        @event.Email.Should().Be(" owner@example.com ");
    }

    [Fact]
    public void UpdateDisplayName_WhenCalled_TrimsAndSetsDisplayName()
    {
        var owner = CreateOwner();

        owner.UpdateDisplayName("  Alex  ");

        owner.DisplayName.Should().Be("Alex");
    }

    [Fact]
    public void RequestDeletion_WhenCalled_SetsDeletionRequestedAt()
    {
        var owner = CreateOwner();
        var before = DateTimeOffset.UtcNow;

        owner.RequestDeletion();

        var after = DateTimeOffset.UtcNow;
        owner.DeletionRequestedAt.Should().NotBeNull();
        owner.DeletionRequestedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        owner.GracePeriodEndsAt.Should().BeNull("only ConfirmDeletion starts the grace period");
    }

    [Fact]
    public void ConfirmDeletion_WhenCalled_SetsGracePeriodEndsAtGracePeriodDaysOut()
    {
        var owner = CreateOwner();
        owner.RequestDeletion();
        var before = DateTimeOffset.UtcNow;

        owner.ConfirmDeletion(30);

        var after = DateTimeOffset.UtcNow;
        owner.GracePeriodEndsAt.Should().NotBeNull();
        owner.GracePeriodEndsAt!.Value.Should().BeOnOrAfter(before.AddDays(30)).And.BeOnOrBefore(after.AddDays(30));
    }

    [Fact]
    public void RecoverAccount_WhenCalled_ClearsDeletionRequestedAtAndGracePeriodEndsAt()
    {
        var owner = CreateOwner();
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);

        owner.RecoverAccount();

        owner.DeletionRequestedAt.Should().BeNull();
        owner.GracePeriodEndsAt.Should().BeNull();
    }

    [Fact]
    public void PermanentlyDelete_WhenCalled_SetsIsPermanentlyDeleted()
    {
        var owner = CreateOwner();
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);

        owner.PermanentlyDelete();

        owner.IsPermanentlyDeleted.Should().BeTrue();
        owner.GracePeriodEndsAt.Should().NotBeNull("permanent deletion is historical, not a reset of the saga's own timestamps");
    }
}
