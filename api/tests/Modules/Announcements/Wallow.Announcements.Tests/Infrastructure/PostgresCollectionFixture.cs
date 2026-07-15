using Wallow.Tests.Common.Fixtures;

namespace Wallow.Announcements.Tests.Infrastructure;

[CollectionDefinition("PostgresDatabase")]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
}
