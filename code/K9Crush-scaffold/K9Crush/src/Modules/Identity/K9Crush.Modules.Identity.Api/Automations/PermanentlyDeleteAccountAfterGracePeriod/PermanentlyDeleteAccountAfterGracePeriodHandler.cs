using Marten;
using K9Crush.Modules.Identity.Domain;

namespace K9Crush.Modules.Identity.Api.Automations.PermanentlyDeleteAccountAfterGracePeriod;

/// <summary>
/// Automation slice (ADR-026): the emlang yaml's "Permanently Delete
/// Account After Grace Period" -> "Account Permanently Deleted", given
/// "Account Deleted". Document-store module, so there is no domain event
/// to subscribe to - the trigger is the scheduled message
/// ConfirmAccountDeletionHandler fires 30 days out, same shape as
/// ShelterAdoption's MarkApplicationStaleHandler/CloseStaleApplicationHandler.
///
/// Re-checks the account's current state rather than trusting the
/// scheduled message alone: if the owner recovered during the grace
/// period (GracePeriodEndsAt cleared by RecoverAccountHandler), or the
/// account is already permanently deleted, or the grace period
/// somehow hasn't actually elapsed yet, this is a no-op - same
/// idempotency-guard shape as every other automation in this codebase.
/// </summary>
public static class PermanentlyDeleteAccountAfterGracePeriodHandler
{
    public static async Task Handle(
        CheckAccountGracePeriodExpired message,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerAccount = await session.LoadAsync<OwnerAccount>(message.OwnerId, cancellationToken);
        if (ownerAccount is null || ownerAccount.IsPermanentlyDeleted)
            return;

        if (ownerAccount.GracePeriodEndsAt is null || ownerAccount.GracePeriodEndsAt > DateTimeOffset.UtcNow)
            return;

        ownerAccount.PermanentlyDelete();
        session.Store(ownerAccount);
        await session.SaveChangesAsync(cancellationToken);
    }
}
