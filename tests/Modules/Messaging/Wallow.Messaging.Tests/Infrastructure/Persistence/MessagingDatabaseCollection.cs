using Wallow.Tests.Common.Fixtures;

namespace Wallow.Messaging.Tests.Infrastructure.Persistence;

[CollectionDefinition("PostgresDatabase")]
public class MessagingPostgresDatabaseCollection : ICollectionFixture<PostgresContainerFixture>;
