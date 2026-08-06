using ShiftSoftware.UnifiedAttestation.Enums;
using System.Threading.Tasks;

namespace ShiftSoftware.UnifiedAttestation.Services.Interfaces
{
    public interface IUnifiedAttestationService
    {
        /// <summary>
        /// Routes the attestation token to the appropriate verification service based on the client's platform.
        /// </summary>
        /// <param name="token">
        /// The attestation token provided by the client request. This is a Firebase App Check Token for iOS and Android,
        /// and for Huawei it is either an HMS UserDetect response token or an HMS SysIntegrity JWS, selected by
        /// <paramref name="hmsApi"/>.
        /// </param>
        /// <param name="platform">The OS or attestation provider the token originated from.</param>
        /// <param name="withReplayProtection">
        /// If true, performs a strict, one-time-use network verification for Firebase App Check Token only to prevent replay attacks.
        /// Defaults to false (which uses fast, offline JWT validation where supported).
        /// </param>
        /// <param name="nonce">
        /// The nonce the client passed to the HMS SysIntegrity API. Only used by the HMS SysIntegrity path, and only when
        /// <see cref="Models.HMS.HMSSysIntegrityOptions.RequireNonce"/> is enabled. Ignored by every other provider.
        /// </param>
        /// <param name="hmsApi">
        /// Which HMS SafetyDetect API to verify a Huawei token with. When null (the default), UserDetect is used unless
        /// SysIntegrity is the only enabled HMS API, so existing Huawei clients that predate SysIntegrity keep working
        /// unchanged. Ignored for non-Huawei platforms.
        /// </param>
        /// <returns>True if the token is valid, trusted, otherwise, false.</returns>
        public ValueTask<bool> VerifyTokenAsync(string token, AttestationPlatform platform, bool? withReplayProtection = false, string? nonce = null, HMSAttestationApi? hmsApi = null);
    }
}
