using Wallow.Tests.Common.Factories;

namespace Wallow.Tests.Common.Fixtures;

[CollectionDefinition(nameof(WallowTestCollection))]
public class WallowTestCollection : ICollectionFixture<WallowApiFactory>
{
}
