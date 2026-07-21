using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Moderation.Domain;

/// <summary>
/// Current-state Marten document, one per owner who has ever had
/// moderation action taken against them - the emlang yaml's "Warn User"
/// -> "Suspend User" -> "Ban User" escalation ladder (per the yaml's own
/// GWT tests: suspension requires a prior warning, a ban requires both a
/// prior warning and a prior suspension). Id is the owner's own OwnerId
/// (Identity's OwnerAccount.Id), same FK-by-convention as every other
/// cross-module owner reference in this codebase - created lazily on
/// first warning, not provisioned per-owner up front.
///
/// Deliberately does NOT enforce anything - IsSuspended/IsBanned are
/// recorded facts only. Actually blocking a suspended/banned owner from
/// using the app would mean every module's authorization checks
/// consulting this record, a cross-cutting change far bigger than this
/// one moderation-queue chapter; a real, separately-scoped follow-up,
/// not attempted here.
/// </summary>
public class UserModerationRecord : Entity
{
    [JsonInclude] public int WarningCount { get; private set; }
    [JsonInclude] public bool IsSuspended { get; private set; }
    [JsonInclude] public bool IsBanned { get; private set; }

    [JsonConstructor]
    private UserModerationRecord() { }

    public static UserModerationRecord CreateFor(Guid ownerId)
    {
        return new UserModerationRecord { Id = ownerId };
    }

    /// <summary>The emlang yaml's "Warn User" -> "User Warned". State-guard (none - always valid) lives in the handler per this codebase's convention.</summary>
    public void Warn() => WarningCount++;

    /// <summary>
    /// The emlang yaml's "Suspend User" -> "User Suspended" - only valid
    /// after at least one prior warning (RepeatOffenderSuspendedAfterAPriorWarning).
    /// State-guard lives in the handler.
    /// </summary>
    public void Suspend() => IsSuspended = true;

    /// <summary>
    /// The emlang yaml's "Ban User" -> "User Banned" - only valid after a
    /// prior warning AND a prior suspension (RepeatOffenderBannedAfterWarningAndSuspension).
    /// State-guard lives in the handler.
    /// </summary>
    public void Ban() => IsBanned = true;
}
