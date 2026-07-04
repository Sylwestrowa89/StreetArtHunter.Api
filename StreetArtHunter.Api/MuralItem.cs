using Newtonsoft.Json;

namespace StreetArtHunter.Api
{
    internal class MuralItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string ImageUrl { get; set; }
    }
}
