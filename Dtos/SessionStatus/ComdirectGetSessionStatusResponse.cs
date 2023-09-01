using Newtonsoft.Json;

namespace ComdirectTransactionTracker.Dtos
{
    public class ComdirectGetSessionStatusResponse
    {
        [JsonProperty("identifier")]
        public string Identifier { get; set; }

        [JsonProperty("sessionTanActive")]
        public bool SessionTanActive { get; set; }

        [JsonProperty("activated2FA")]
        public string Activated2FA { get; set; }
    }
}
