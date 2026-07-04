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
            _logger.LogInformation("Rozpoczęto przetwarzanie nowego muralu.");

            try
            {
                var formdata = await req.ReadFormAsync(); // Odczytujemy dane z formularza multipart/form-data
                var file = formdata.Files.GetFile("image"); // Pobieramy plik
                var description = formdata["description"].ToString(); // Pobieramy dodatkowe dane
                var location = formdata["location"].ToString();

                if (file == null || file.Length == 0)
                {
                    return new BadRequestObjectResult("Nie znaleziono pliku obrazu w żądaniu.");
                }

                // --- INTEGRACJA Z BLOB STORAGE ---

                string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage"); // 1. Pobieramy "connection string" do lokalnego emulatora Azurite. // Klucz "AzureWebJobsStorage" jest domyślnie używany przez Azure Functions do zarządzania własnym stanem lokalnie

                var blobServiceClient = new BlobServiceClient(connectionString); // 2. Tworzymy głównego klienta do połączenia z kontem Storage.                

                var containerClient = blobServiceClient.GetBlobContainerClient("murals"); // 3. Wskazujemy kontener (folder) o nazwie "murals".                
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob); // Jeśli ten kontener jeszcze nie istnieje (to pierwsze wywołanie), Azure go utworzy. // Ustawiamy Blob na PublicAccess, dzięki czemu uzyskamy URL, który można otworzyć w przeglądarce.

                string fileExtension = Path.GetExtension(file.FileName);
                string uniqueBlobName = $"{Guid.NewGuid()}{fileExtension}"; // 4. Generujemy unikalną nazwę dla pliku. // Nigdy nie ufaj oryginalnej nazwie pliku, bo użytkownicy mogliby się nawzajem nadpisywać.

                var blobClient = containerClient.GetBlobClient(uniqueBlobName); // 5. Tworzymy klienta reprezentującego ten konkretny plik, który zaraz wgramy                

                _logger.LogInformation("Rozpoczynam wysyłanie pliku do Blob Storage jako: {uniqueBlobName}", uniqueBlobName);
                using (var stream = file.OpenReadStream()) // 6. Otwieramy strumień z odebranego pliku i wysyłamy go do kontenera
                {
                    //await blobClient.UploadAsync(stream, overwrite: true);

                    var blobHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders // 1. Definiujemy nagłówki dla pliku
                    {
                        ContentType = "image/jpeg"
                    };

                    var uploadOptions = new Azure.Storage.Blobs.Models.BlobUploadOptions // 2. Przekazujemy nagłówki w obiekt opcji
                    {
                        HttpHeaders = blobHeaders
                    };

                    await blobClient.UploadAsync(stream, uploadOptions); // 3. Wysyłamy strumień z nowymi opcjami (zastępuje stare UploadAsync)
                }

                string imageUrl = blobClient.Uri.ToString(); // 7. Odczytujemy wygenerowany, publiczny link do naszego obrazka!
                _logger.LogInformation("Ukończono! Obrazek dostępny pod adresem: {imageUrl}", imageUrl);

                // ---------------------------------------------

                // --- INTEGRACJA Z COSMOS DB ---

                string cosmosConnectionString = "AccountEndpoint=https://db-streetart-hunter.documents.azure.com:443/;AccountKey=G1o3G6VxmeuIZmdXgvZOsfNbedDAaweb8PqJ4lx65vez9NJIM0jBnxcH1bZv34rvEsBJPJ8rfPsnACDbUQop9w==;"; // 1.
                var cosmosClient = new CosmosClient(cosmosConnectionString);

                Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync("StreetArtDb"); // 2. Tworzymy bazę danych i kontener (jeśli nie istnieją)
                Container container = await database.CreateContainerIfNotExistsAsync("Murals", "/Location"); // W Cosmos DB każdy kontener musi mieć "Partition Key" (Klucz partycji). // Pomaga to chmurze w dystrybucji danych. Użyjemy "/Location".

                var newMural = new MuralItem // 3. Tworzymy obiekt C# reprezentujący nasz nowy rekord
                {
                    Id = Guid.NewGuid().ToString(), // Unikalne ID dokumentu
                    Description = description,
                    Location = location,
                    ImageUrl = imageUrl
                };

                await container.CreateItemAsync(newMural, new PartitionKey(newMural.Location)); // 4. Zapisujemy obiekt do bazy danych jako dokument JSON

                _logger.LogInformation("Zapisano metadane w Cosmos DB dla ID: {newMural.Id}", newMural.Id);

                // ---------------------------------------------

                return new OkObjectResult(new
                {
                    Message = "Sukces! Zapisano obrazek ORAZ dodano wpis do bazy danych. Pipeline działa",
                    MuralId = newMural.Id,
                    ImageUrl = imageUrl
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przetwarzania: {ex.Message}", ex.Message);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
