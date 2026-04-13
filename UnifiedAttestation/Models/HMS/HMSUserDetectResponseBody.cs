using System.Text.Json.Serialization;

namespace ShiftSoftware.UnifiedAttestation.Models.HMS
{
    internal class HMSUserDetectResponseBody
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; } = default!;
        [JsonPropertyName("error-codes")]
        public string ErrorCodes { get; set; } = default!;
        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTS { get; set; }
        [JsonPropertyName("apk_package_name")]
        public string? APKPackageName { get; set; }
    }
}
