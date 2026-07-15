using Wallow.Tests.Common.Fixtures;

namespace Wallow.Inquiries.Tests.Infrastructure.Persistence;

[CollectionDefinition("InquiriesPostgresDatabase")]
public class InquiriesDbCollection : ICollectionFixture<PostgresContainerFixture> { }
