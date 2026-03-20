using Wallow.Tests.Common.Factories;

namespace Wallow.Api.Tests.Integration;

[CollectionDefinition(nameof(ApiIntegrationTestCollection))]
public class ApiIntegrationTestCollection : ICollectionFixture<WallowApiFactory>
{
}
