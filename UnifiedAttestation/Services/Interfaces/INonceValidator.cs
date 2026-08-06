using System.Threading.Tasks;

namespace ShiftSoftware.UnifiedAttestation.Services.Interfaces
{
    /// <summary>
    /// Validates the nonce carried inside an attestation payload.
    /// </summary>
    /// <remarks>
    /// The default registration is <see cref="ShiftSoftware.UnifiedAttestation.Services.EchoNonceValidator"/>, which compares the nonce the client echoed back
    /// with the nonce found inside the verified payload. Register a different implementation before calling
    /// <c>AddAttestationVerification</c> to move to server issued, single use nonces without any other code change.
    /// </remarks>
    public interface INonceValidator
    {
        /// <summary>
        /// Determines whether the nonce inside a verified attestation payload is acceptable.
        /// </summary>
        /// <param name="expectedNonce">The nonce supplied by the client through the nonce header, or issued by the server.</param>
        /// <param name="payloadNonce">The nonce found inside the verified attestation payload.</param>
        /// <returns>True when the nonce is accepted; otherwise, false.</returns>
        ValueTask<bool> ValidateAsync(string? expectedNonce, string? payloadNonce);
    }
}
