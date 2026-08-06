using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShiftSoftware.UnifiedAttestation.Models.HMS
{
    internal class HMSSysIntegrityJwsHeader
    {
        [JsonPropertyName("alg")]
        public string Algorithm { get; set; } = default!;

        /// <summary>
        /// The certificate chain, leaf first, each entry a standard Base64 encoded DER certificate.
        /// </summary>
        [JsonPropertyName("x5c")]
        public List<string> X5C { get; set; } = default!;
    }
}
