using ShiftSoftware.UnifiedAttestation.Services.Interfaces;
using ShiftSoftware.UnifiedAttestation.Utilities;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ShiftSoftware.UnifiedAttestation.Services
{
    /// <summary>
    /// The default <see cref="INonceValidator"/>. Accepts the payload nonce when it matches the nonce the client echoed
    /// back through the nonce header.
    /// </summary>
    /// <remarks>
    /// This proves the payload belongs to the nonce the caller claims to have used, but the nonce is still chosen by the
    /// client, so it does not by itself make a payload single use. Replay is bounded by
    /// <see cref="Models.HMS.HMSSysIntegrityOptions.MaxTokenAge"/>. Replace this with a server issued, single use nonce
    /// store when stronger replay protection is needed.
    /// </remarks>
    public class EchoNonceValidator : INonceValidator
    {
        public ValueTask<bool> ValidateAsync(string? expectedNonce, string? payloadNonce)
        {
            if (string.IsNullOrWhiteSpace(expectedNonce) || string.IsNullOrWhiteSpace(payloadNonce))
                return new ValueTask<bool>(false);

            // Compare the decoded bytes so a client that used the URL safe alphabet (or dropped the padding) still
            // matches the standard Base64 nonce Huawei echoes back in the payload.
            if (Base64Utility.TryDecodeRelaxed(expectedNonce, out var expectedBytes)
                && Base64Utility.TryDecodeRelaxed(payloadNonce, out var payloadBytes))
            {
                return new ValueTask<bool>(CryptographicOperations.FixedTimeEquals(expectedBytes, payloadBytes));
            }

            // Not Base64 on one side or the other. Fall back to a fixed time comparison of the raw values.
            return new ValueTask<bool>(
                CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(expectedNonce),
                    System.Text.Encoding.UTF8.GetBytes(payloadNonce)));
        }
    }
}
