using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
            _logger.LogInformation("HTTP POST received at /murals. Starting upload process.");

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

                // --- INTEGRATION WITH SERVICE BUS ---

                var muralId = Guid.NewGuid().ToString();
                var messagePayload = new
                {
                    Id = muralId,
                    Description = description,
                    Location = location,
                    ImageUrl = imageUrl
                };

                string jsonPayload = JsonSerializer.Serialize(messagePayload);

                string serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection");

                await using var client = new ServiceBusClient(serviceBusConnectionString);
                ServiceBusSender sender = client.CreateSender("mural-tasks"); // Name of a queue

                ServiceBusMessage message = new ServiceBusMessage(jsonPayload);
                await sender.SendMessageAsync(message);

                _logger.LogInformation("Successfully queued metadata processing for Mural ID: {muralId}", muralId);

                // ---------------------------------------------

                return new AcceptedResult(string.Empty, new
                {
                    Message = "Awesome! We've received your mural. It will appear in the gallery shortly.",
                    MuralId = muralId,
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