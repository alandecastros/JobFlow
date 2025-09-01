using JobFlow.Core;
using JobFlow.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace JobFlow.MongoDb;

public static class JobFlowOptionsExtensions
{
    public static void UseMongoDb(this JobFlowOptions options, MongoDbOptions mongoDbOptions)
    {
        options.Services.TryAddKeyedSingleton<IMongoClient>(
            "mongodb-job-queue",
            (_, _) => new MongoClient(mongoDbOptions.ConnectionString)
        );

        BsonClassMap.TryRegisterClassMap<Job>(cm =>
        {
            cm.AutoMap();
            cm.MapIdProperty(c => c.Id)
                .SetIdGenerator(StringObjectIdGenerator.Instance)
                .SetSerializer(new StringSerializer(BsonType.ObjectId))
                .SetElementName("_id");
        });

        options.Services.TryAddSingleton(mongoDbOptions);
        options.Services.TryAddScoped<IStorageService, MongoDbStorageService>();
    }
}
