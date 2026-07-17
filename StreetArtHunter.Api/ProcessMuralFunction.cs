using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace StreetArtHunter.Api
{
    internal class ProcessMuralFunction
    {
        private readonly ILogger<ProcessMuralFunction> _logger;

        public ProcessMuralFunction(ILogger<ProcessMuralFunction> logger)
        {
            _logger = logger;
        }

        [Function("ProcessMural")]
        public async Task Run([ServiceBusTrigger("mural-tasks", Connection = "ServiceBusConnection")] string myQueueItem)
        {
            _logger.LogInformation("ServiceBusTrigger successfully processed message from 'mural-tasks'. Payload: {myQueueItem}", myQueueItem);

            try
            {
                var muralData = JsonSerializer.Deserialize<MuralItem>(myQueueItem);

                if (muralData == null)
                {
                    _logger.LogError("Failed to deserialize the message payload.");
                    return;
                }

                string cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDbConnection");
                var cosmosClient = new CosmosClient(cosmosConnectionString);

                Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync("StreetArtDb");
                Container container = await database.CreateContainerIfNotExistsAsync("Murals", "/Location");

                await container.CreateItemAsync(muralData, new PartitionKey(muralData.Location));

                _logger.LogInformation("Successfully saved metadata to Cosmos DB for Mural ID: {Id}", muralData.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {Message}", ex.Message);
                throw;
            }
        }
    }
}
