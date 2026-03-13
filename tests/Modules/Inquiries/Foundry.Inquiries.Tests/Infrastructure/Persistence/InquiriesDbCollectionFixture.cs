using Foundry.Tests.Common.Fixtures;

namespace Foundry.Inquiries.Tests.Infrastructure.Persistence;

[CollectionDefinition("InquiriesPostgresDatabase")]
public class InquiriesDbCollection : ICollectionFixture<PostgresContainerFixture> { }
