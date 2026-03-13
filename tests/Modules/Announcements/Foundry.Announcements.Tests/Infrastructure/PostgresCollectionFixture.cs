using Foundry.Tests.Common.Fixtures;

namespace Foundry.Announcements.Tests.Infrastructure;

[CollectionDefinition("PostgresDatabase")]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
}
