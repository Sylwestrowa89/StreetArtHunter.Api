using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace StreetArtHunter.Api
{
    public class UploadMuralFunction
    {
        private readonly ILogger<UploadMuralFunction> _logger;

        public UploadMuralFunction(ILogger<UploadMuralFunction> logger)
        {
            _logger = logger;
        }

        [Function("UploadMural")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "murals")] HttpRequest req)
        {
            _logger.LogInformation("Started processing a new mural.");

            try
            {
                var formdata = await req.ReadFormAsync();
                var file = formdata.Files.GetFile("image");
                var description = formdata["description"].ToString();
                var location = formdata["location"].ToString();

                if (file == null || file.Length == 0)
                {
                    return new BadRequestObjectResult("Image file not found in the request.");
                }

                // --- INTEGRATION WITH BLOB STORAGE ---

                string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

                var blobServiceClient = new BlobServiceClient(connectionString);

                var containerClient = blobServiceClient.GetBlobContainerClient("murals");
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                string fileExtension = Path.GetExtension(file.FileName);
                string uniqueBlobName = $"{Guid.NewGuid()}{fileExtension}";

                var blobClient = containerClient.GetBlobClient(uniqueBlobName);

                _logger.LogInformation("Starting to upload the file to Blob Storage as: {uniqueBlobName}", uniqueBlobName);
                using (var stream = file.OpenReadStream())
                {
                    //await blobClient.UploadAsync(stream, overwrite: true);

                    var blobHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = "image/jpeg"
                    };

                    var uploadOptions = new Azure.Storage.Blobs.Models.BlobUploadOptions
                    {
                        HttpHeaders = blobHeaders
                    };

                    await blobClient.UploadAsync(stream, uploadOptions);
                }

                string imageUrl = blobClient.Uri.ToString();
                _logger.LogInformation("Completed! The image is available at: {imageUrl}", imageUrl);

                // ---------------------------------------------

                // --- INTEGRATION WITH COSMOS DB ---

                string cosmosConnectionString = "AccountEndpoint=https://db-streetart-hunter.documents.azure.com:443/;AccountKey=G1o3G6VxmeuIZmdXgvZOsfNbedDAaweb8PqJ4lx65vez9NJIM0jBnxcH1bZv34rvEsBJPJ8rfPsnACDbUQop9w==;";
                var cosmosClient = new CosmosClient(cosmosConnectionString);

                Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync("StreetArtDb");
                Container container = await database.CreateContainerIfNotExistsAsync("Murals", "/Location");

                var newMural = new MuralItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Description = description,
                    Location = location,
                    ImageUrl = imageUrl
                };

                await container.CreateItemAsync(newMural, new PartitionKey(newMural.Location));

                _logger.LogInformation("Saved metadata in Cosmos DB for ID: {newMural.Id}", newMural.Id);

                // ---------------------------------------------

                return new OkObjectResult(new
                {
                    Message = "Success! Image saved AND database entry added.",
                    MuralId = newMural.Id,
                    ImageUrl = imageUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during processing: {ex.Message}", ex.Message);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}