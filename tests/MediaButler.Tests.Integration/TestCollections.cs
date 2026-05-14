using Xunit;

namespace MediaButler.Tests.Integration;

[CollectionDefinition("Database Tests")]
public class DatabaseCollection : ICollectionFixture<Infrastructure.DatabaseFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
