using Wallow.Tests.Common.Fixtures;

namespace Wallow.Api.Tests.Integration;

[CollectionDefinition(nameof(RedisTestCollection))]
public class RedisTestCollection : ICollectionFixture<RedisFixture>
{
}
