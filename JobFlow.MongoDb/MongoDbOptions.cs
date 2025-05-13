using JobFlow.Core;

namespace JobFlow.MongoDb;

public class MongoDbOptions
{
    public required string ConnectionString { get; init; }
    public required string Database { get; init; }
    public required string Collection { get; init; }
}
