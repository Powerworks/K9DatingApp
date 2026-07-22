namespace K9Crush.Modules.Identity.Api.Automations.PermanentlyDeleteAccountAfterGracePeriod;

/// <summary>
/// Scheduled message (ADR-026, Wolverine <c>IMessageBus.ScheduleAsync</c>) -
/// the "please check now" trigger for the emlang yaml's "Permanently
/// Delete Account After Grace Period" command, fired gracePeriodDays (30)
/// out by ConfirmAccountDeletionHandler when it appends "Account Deleted".
/// Same shape as ShelterAdoption's CheckApplicationStale/
/// CheckApplicationClosed - same-module only, never published
/// cross-module.
/// </summary>
public sealed record CheckAccountGracePeriodExpired(Guid OwnerId);
