using System.Text.Json.Serialization;

namespace ShiftSoftware.UnifiedAttestation.Models.HMS
{
    internal class HMSUserDetectRequestBody
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = default!;
        [JsonPropertyName("response")]
        public string Response { get; set; } = default!;
    }
}
