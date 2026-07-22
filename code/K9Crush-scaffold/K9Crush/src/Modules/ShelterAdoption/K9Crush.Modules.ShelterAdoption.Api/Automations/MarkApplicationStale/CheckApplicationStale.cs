namespace K9Crush.Modules.ShelterAdoption.Api.Automations.MarkApplicationStale;

/// <summary>
/// Scheduled message (ADR-026, Wolverine <c>IMessageBus.ScheduleAsync</c>) -
/// the "please check now" trigger for the emlang yaml's "Mark Application
/// Stale" command, fired 15 days out by RequestAdditionalDetailsHandler
/// when it appends "Additional Details Requested". Same-module only, never
/// published cross-module.
/// </summary>
public sealed record CheckApplicationStale(Guid ApplicationId);
