using Marten;
using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Automations.CloseStaleApplication;

/// <summary>
/// Automation slice (ADR-026): the emlang yaml's "Close Stale Application"
/// -> "Application Closed" (closesAfterDays: 30), given "Application
/// Marked Stale". Same scheduled-message trigger shape as
/// MarkApplicationStaleHandler - see that handler's doc comment.
///
/// Re-checks the application is still Stale before acting: if the
/// application no longer exists, or something else already moved it past
/// Stale, this is a no-op.
/// </summary>
public static class CloseStaleApplicationHandler
{
    public static async Task Handle(
        CheckApplicationClosed message,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<Application>(message.ApplicationId, cancellationToken);
        var application = stream.Aggregate;
        if (application is null || application.Status != ApplicationStatus.Stale)
            return;

        var @event = application.Close();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);
    }
}
