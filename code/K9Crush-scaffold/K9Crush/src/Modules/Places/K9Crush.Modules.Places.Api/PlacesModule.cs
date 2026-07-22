using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Places.Domain;

namespace K9Crush.Modules.Places.Api;

/// <summary>
/// Composition root for the Places module. Api.Host discovers this via
/// assembly scanning (see Program.cs) - nothing else references this
/// type.
///
/// First increment covers the emlang yaml's LeaveAReviewRestaurantOrDogPark
/// chapter (Write/Publish/Edit/Remove/Respond/Report Review) plus a
/// disclosed CreatePlaceListing gap-fill (see Place.cs's own doc comment
/// for why). Deliberately does NOT cover ClaimABusinessListing (the
/// request/verify/admin-override/ownership-transfer workflow - a real,
/// separately-scoped feature) or ManagingSavedDogsSpots (entangled with
/// dog-to-dog matching/video-chat concepts that don't exist anywhere in
/// this codebase yet). No IntegrationEventQueueName - this module only
/// ever publishes, it doesn't consume any other module's events yet,
/// same as Profiles/Media before Moderation needed one from Media.
/// </summary>
public sealed class PlacesModule : IModule
{
    public string Name => "Places";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new PlacesMartenConfiguration();

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nothing beyond Wolverine's auto-discovered handlers for this
        // module yet.
    }

    private sealed class PlacesMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "places";

        public void Configure(StoreOptions options)
        {
            options.Schema.For<Place>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.OwnerId);

            options.Schema.For<Review>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.PlaceId)
                .Index(x => x.ReviewerOwnerId);
        }
    }
}
