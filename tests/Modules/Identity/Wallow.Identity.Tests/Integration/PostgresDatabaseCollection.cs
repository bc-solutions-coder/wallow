using Wallow.Tests.Common.Fixtures;

namespace Wallow.Identity.Tests.Integration;

[CollectionDefinition("PostgresDatabase")]
public class PostgresDatabaseCollection : ICollectionFixture<PostgresContainerFixture>;
