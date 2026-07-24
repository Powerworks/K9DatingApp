using System.Reflection;
using FluentAssertions;
using Marten;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace K9Crush.ArchitectureTests;

/// <summary>
/// Mechanizes ADR-019/ADR-031 (see docs/03-solution-architecture.md Section
/// 2.2 and docs/05-event-modeling-blueprint.md Section 6): a command or
/// automation deciding about an entity's own state must load that state live
/// via AggregateStreamAsync/FetchForWriting, never via LoadAsync/Query
/// against a persisted snapshot of the same type - that's the exact mistake
/// the original MatchAggregate incident made.
///
/// Since ADR-031, this can no longer be caught by a plain NetArchTest
/// type-dependency check: the write-side aggregate and a read-side Inline
/// snapshot are commonly the *same class*, so both the legitimate
/// `session.Events.FetchForWriting&lt;Application&gt;()` call and the
/// illegitimate `session.LoadAsync&lt;Application&gt;()` call reference an
/// identical type - a declarative dependency graph can't tell them apart.
/// This needs an actual IL-level scan (Mono.Cecil, already a transitive
/// dependency of NetArchTest.Rules - see Directory.Packages.props): look for
/// LoadAsync/Query call instructions whose generic type argument is a
/// registered snapshot type, from within a type under a Commands/** or
/// Automations/** namespace.
/// </summary>
public class CommandStateFitnessTests
{
    /// <summary>
    /// Types registered as an Inline snapshot (`Projections.Snapshot&lt;T&gt;`)
    /// somewhere in the solution's Module.cs files - add one entry per entity
    /// as each ADR-031 retrofit phase lands it. Empty right now: no module
    /// has been retrofitted yet (Phase 0 only). This list intentionally does
    /// NOT include ADR-019 `[CommandName]State` types - those are never
    /// persisted/registered as snapshots at all, so LoadAsync against one
    /// isn't even possible; the risk this test guards against is specific to
    /// dual-use self-aggregating types.
    /// </summary>
    private static readonly HashSet<string> SnapshotRegisteredTypeFullNames = new()
    {
        // Phase 2 (Admin): FeedbackInboxItem is genuinely queried by
        // ReadModels/** (GetFeedbackInboxHandler/GetFeedbackDetailHandler),
        // so it's registered as its own Inline snapshot in AdminModule.cs.
        "K9Crush.Modules.Admin.Domain.FeedbackInboxItem",
        // Phase 3 (Notifications): both queried by a ReadModels/** handler
        // (ViewNotificationPreferences/ViewNotificationTemplates).
        "K9Crush.Modules.Notifications.Domain.NotificationPreference",
        "K9Crush.Modules.Notifications.Domain.NotificationTemplate",
        // Phase 4 (Identity): queried by OwnerAccountView/ViewProfileSettings
        // and by MartenOwnerRoleLookup (ADR-017).
        "K9Crush.Modules.Identity.Domain.OwnerAccount",
    };

    private static readonly Assembly[] ApiAssembliesToScan =
    [
        typeof(K9Crush.Modules.Media.Api.MediaModule).Assembly,
        typeof(K9Crush.Modules.Admin.Api.AdminModule).Assembly,
        typeof(K9Crush.Modules.Notifications.Api.NotificationsModule).Assembly,
        typeof(K9Crush.Modules.Identity.Api.IdentityModule).Assembly,
        typeof(K9Crush.Modules.ShelterAdoption.Api.ShelterAdoptionModule).Assembly
    ];

    /// <summary>
    /// Reviewed, deliberate exceptions - ONLY for `Query&lt;T&gt;()` (never
    /// `LoadAsync&lt;T&gt;()`, which has no legitimate use case here: a
    /// command that needs a specific id's current state has
    /// FetchForWriting/AggregateStreamAsync for exactly that, so a
    /// by-id LoadAsync against a snapshot type is always the accidental
    /// MatchAggregate-shaped mistake, never a population check). A
    /// `Query&lt;T&gt;()` call is different in kind: it's a cross-entity
    /// population check (e.g. "does any OTHER OwnerAccount have Role
    /// Admin", "how many other Applications does this applicant have
    /// open") - the same pattern this codebase's read models already use,
    /// not a command loading a persisted snapshot of the one entity
    /// instance it's about to decide about. Each entry here has been
    /// read and judged legitimate; add a new one only with the same
    /// scrutiny, not to silence a real finding.
    /// </summary>
    private static readonly HashSet<(string CallingType, string SnapshotType)> ReviewedCrossPopulationQueryExceptions = new()
    {
        // BootstrapAdminHandler checks "does any admin exist at all" across
        // every OwnerAccount before separately FetchForWriting-ing the
        // CALLER's own account - the query and the mutation target are
        // different instances of the same type.
        ("K9Crush.Modules.Identity.Api.Commands.BootstrapAdmin.BootstrapAdminHandler", "K9Crush.Modules.Identity.Domain.OwnerAccount"),
    };

    [Fact]
    public void CommandsAndAutomations_MustNotLoadOrQueryARegisteredSnapshotType()
    {
        if (SnapshotRegisteredTypeFullNames.Count == 0)
            return; // no module retrofitted yet - nothing to check until Phase 1 adds its first entry

        var violations = ApiAssembliesToScan
            .SelectMany(a => FindSnapshotSessionCalls(a.Location, SnapshotRegisteredTypeFullNames))
            .Where(v => v.CalledMethod != "Query" || !ReviewedCrossPopulationQueryExceptions.Contains((v.CallingType, v.GenericArgument)))
            .ToList();

        violations.Should().BeEmpty(
            "a Commands/**/Automations/** type must load its own decision state live via " +
            "AggregateStreamAsync/FetchForWriting, never LoadAsync/Query against a persisted " +
            "snapshot of the same type (ADR-019/ADR-031), unless explicitly allowlisted above as a " +
            "reviewed cross-population check - violations found: " +
            string.Join(", ", violations.Select(v => $"{v.CallingType}.{v.CallingMethod} calls {v.CalledMethod}<{v.GenericArgument}>")));
    }

    /// <summary>
    /// Self-test proving the scanner actually distinguishes the two call
    /// shapes, using real Marten types compiled into this test assembly -
    /// not a synthetic stand-in for Marten's API. FakeCommandHandler
    /// (illegitimate - under a namespace containing ".Commands", calls
    /// LoadAsync against SelfTestSnapshotEntity) must be flagged;
    /// FakeAutomationHandler (legitimate - calls FetchForWriting against the
    /// same type) must not.
    /// </summary>
    [Fact]
    public void Scanner_DistinguishesLoadAsyncFromFetchForWriting_AgainstTheSameType()
    {
        var watchlist = new HashSet<string> { typeof(SelfTestFixture.SelfTestSnapshotEntity).FullName! };

        var violations = FindSnapshotSessionCalls(typeof(CommandStateFitnessTests).Assembly.Location, watchlist)
            .ToList();

        violations.Should().ContainSingle(v => v.CallingType == typeof(SelfTestFixture.Commands.FakeCommandHandler).FullName)
            .Which.CalledMethod.Should().Be("LoadAsync");

        violations.Should().NotContain(v => v.CallingType == typeof(SelfTestFixture.Automations.FakeAutomationHandler).FullName);
    }

    private static IEnumerable<(string CallingType, string CallingMethod, string CalledMethod, string GenericArgument)> FindSnapshotSessionCalls(
        string assemblyPath, IReadOnlySet<string> snapshotTypeFullNames)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

        foreach (var type in assembly.MainModule.Types)
        {
            var ns = type.Namespace ?? string.Empty;
            if (!ns.Contains(".Commands", StringComparison.Ordinal) && !ns.Contains(".Automations", StringComparison.Ordinal))
                continue;

            // async Handle methods get compiler-rewritten into a nested
            // state-machine type (e.g. FakeCommandHandler+<Handle>d__0) -
            // the actual LoadAsync/FetchForWriting call lives in THAT
            // type's MoveNext, not in FakeCommandHandler.Handle itself.
            // Must recurse into nested types or every async handler in the
            // whole codebase would be silently invisible to this scan.
            foreach (var method in AllMethodsIncludingNestedTypes(type))
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Call)
                        continue;

                    if (instruction.Operand is not GenericInstanceMethod genericMethod)
                        continue;

                    var declaringTypeName = genericMethod.ElementMethod.DeclaringType.FullName;
                    var methodName = genericMethod.ElementMethod.Name;

                    var isSessionLoadOrQuery =
                        (declaringTypeName is "Marten.IQuerySession" or "Marten.IDocumentSession")
                        && methodName is "LoadAsync" or "Query";

                    if (!isSessionLoadOrQuery)
                        continue;

                    foreach (var genericArgument in genericMethod.GenericArguments)
                    {
                        if (snapshotTypeFullNames.Contains(genericArgument.FullName))
                        {
                            yield return (type.FullName, method.Name, methodName, genericArgument.FullName);
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<MethodDefinition> AllMethodsIncludingNestedTypes(TypeDefinition type)
    {
        foreach (var method in type.Methods.Where(m => m.HasBody))
            yield return method;

        foreach (var nestedType in type.NestedTypes)
        {
            foreach (var method in AllMethodsIncludingNestedTypes(nestedType))
                yield return method;
        }
    }
}

