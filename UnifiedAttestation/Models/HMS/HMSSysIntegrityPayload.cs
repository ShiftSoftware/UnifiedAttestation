using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShiftSoftware.UnifiedAttestation.Models.HMS
{
    internal class HMSSysIntegrityPayload
    {
        /// <summary>
        /// Suggested action when <see cref="BasicIntegrity"/> is false, for example <c>RESTORE_TO_FACTORY_ROM</c>.
        /// </summary>
        [JsonPropertyName("advice")]
        public string? Advice { get; set; }

        /// <summary>
        /// Base64 encoded SHA-256 digests of the calling application's signing certificates.
        /// </summary>
        [JsonPropertyName("apkCertificateDigestSha256")]
        public List<string>? APKCertificateDigestSha256 { get; set; }

        /// <summary>
        /// Base64 encoded SHA-256 digest of the calling application package.
        /// </summary>
        [JsonPropertyName("apkDigestSha256")]
        public string? APKDigestSha256 { get; set; }

        [JsonPropertyName("apkPackageName")]
        public string? APKPackageName { get; set; }

        /// <summary>
        /// False when the device is rooted, running an unlocked bootloader or a tampered ROM.
        /// </summary>
        [JsonPropertyName("basicIntegrity")]
        public bool BasicIntegrity { get; set; }

        /// <summary>
        /// The nonce the client passed to the SysIntegrity API, echoed back by Huawei.
        /// </summary>
        [JsonPropertyName("nonce")]
        public string? Nonce { get; set; }

        /// <summary>
        /// The time the result was produced, in milliseconds since the Unix epoch.
        /// </summary>
        [JsonPropertyName("timestampMs")]
        public long TimestampMs { get; set; }
    }
}
