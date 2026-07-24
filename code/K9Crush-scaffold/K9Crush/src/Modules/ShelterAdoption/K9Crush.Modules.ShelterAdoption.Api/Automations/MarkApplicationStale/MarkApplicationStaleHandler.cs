using Marten;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Api.Automations.CloseStaleApplication;
using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Automations.MarkApplicationStale;

/// <summary>
/// Automation slice (ADR-026): the emlang yaml's "Mark Application Stale"
/// -> "Application Marked Stale" (staleAfterDays: 15), given "Additional
/// Details Requested". Document-store module, so there is no domain event
/// to subscribe to - the trigger is the scheduled message
/// RequestAdditionalDetailsHandler fires 15 days out, not an immediate
/// reaction to an event stream.
///
/// Re-checks the application's current status rather than trusting the
/// scheduled message alone: if the applicant already responded (status
/// moved back to UnderReview via SubmitAdditionalDetailsHandler) by the
/// time this fires, or the application no longer exists, this is a no-op -
/// the same idempotency-guard shape as every other automation in this
/// codebase (e.g. DetectMutualMatchHandler's isNewMutualMatch check).
/// </summary>
public static class MarkApplicationStaleHandler
{
    public static async Task Handle(
        CheckApplicationStale message,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<Application>(message.ApplicationId, cancellationToken);
        var application = stream.Aggregate;
        if (application is null || application.Status != ApplicationStatus.ReturnedForAlteration)
            return;

        var @event = application.MarkStale();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        await bus.ScheduleAsync(new CheckApplicationClosed(application.Id), TimeSpan.FromDays(30));
    }
}
