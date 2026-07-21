namespace K9Crush.Modules.ShelterAdoption.Api.Automations.CloseStaleApplication;

/// <summary>
/// Scheduled message (ADR-026, Wolverine <c>IMessageBus.ScheduleAsync</c>) -
/// the "please check now" trigger for the emlang yaml's "Close Stale
/// Application" command, fired 30 days out by MarkApplicationStaleHandler
/// when it appends "Application Marked Stale". Same-module only, never
/// published cross-module.
/// </summary>
public sealed record CheckApplicationClosed(Guid ApplicationId);
