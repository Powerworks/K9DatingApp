using FluentAssertions;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Domain;

/// <summary>Layer 1 (TestingApproach.md) - pure unit tests of UserModerationRecord's factory method and domain methods. No mocks, no infra.</summary>
public class UserModerationRecordTests
{
    [Fact]
    public void CreateFor_WhenCalled_CreatesRecordKeyedByOwnerIdWithZeroWarnings()
    {
        var ownerId = Guid.NewGuid();

        var record = UserModerationRecord.CreateFor(ownerId);

        record.Id.Should().Be(ownerId);
        record.WarningCount.Should().Be(0);
        record.IsSuspended.Should().BeFalse();
        record.IsBanned.Should().BeFalse();
    }

    [Fact]
    public void Warn_WhenCalledMultipleTimes_IncrementsWarningCount()
    {
        var record = UserModerationRecord.CreateFor(Guid.NewGuid());

        record.Warn();
        record.Warn();

        record.WarningCount.Should().Be(2);
    }

    [Fact]
    public void Suspend_WhenCalled_SetsIsSuspended()
    {
        var record = UserModerationRecord.CreateFor(Guid.NewGuid());

        record.Suspend();

        record.IsSuspended.Should().BeTrue();
    }

    [Fact]
    public void Ban_WhenCalled_SetsIsBanned()
    {
        var record = UserModerationRecord.CreateFor(Guid.NewGuid());

        record.Ban();

        record.IsBanned.Should().BeTrue();
    }
}
