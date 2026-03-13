using Foundry.Tests.Common.Fixtures;

namespace Foundry.Messaging.Tests.Infrastructure.Persistence;

[CollectionDefinition("PostgresDatabase")]
public class MessagingPostgresDatabaseCollection : ICollectionFixture<PostgresContainerFixture>;
