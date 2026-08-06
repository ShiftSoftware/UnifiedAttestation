using ShiftSoftware.UnifiedAttestation.Enums;
using ShiftSoftware.UnifiedAttestation.Models;
using ShiftSoftware.UnifiedAttestation.Services.Interfaces;
using System.Threading.Tasks;

namespace ShiftSoftware.UnifiedAttestation.Services
{
    public class UnifiedAttestationService : IUnifiedAttestationService
    {
        private readonly AttestationOptions options;
        private readonly FirebaseAppCheckService? firebaseAppCheckService;
        private readonly HMSUserDetectService? hmsUserDetectService;
        private readonly HMSSysIntegrityService? hmsSysIntegrityService;
        public UnifiedAttestationService(AttestationOptions options,
            FirebaseAppCheckService? firebaseAppCheckService = null,
            HMSUserDetectService? hmsUserDetectService = null,
            HMSSysIntegrityService? hmsSysIntegrityService = null)
        {
            this.options = options;
            this.firebaseAppCheckService = firebaseAppCheckService;
            this.hmsUserDetectService = hmsUserDetectService;
            this.hmsSysIntegrityService = hmsSysIntegrityService;
        }

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
        public async ValueTask<bool> VerifyTokenAsync(string token, AttestationPlatform platform, bool? withReplayProtection = false, string? nonce = null, HMSAttestationApi? hmsApi = null)
        {
            if (platform is (AttestationPlatform.Android or AttestationPlatform.iOS) && options.Firebase.Enabled && firebaseAppCheckService != null)
            {
                if (withReplayProtection is true)
                    return await firebaseAppCheckService.VerifyTokenWithReplayProtectionAsync(token);

                return await firebaseAppCheckService.VerifyTokenAsync(token);
            }
            else if (platform is AttestationPlatform.Huawei && options.HMS.Enabled)
            {
                // The caller selects the HMS API. When it does not, default to UserDetect so Huawei clients that predate
                // SysIntegrity keep working, except when SysIntegrity is the only enabled API. Routing is fail-closed: a
                // request for a verifier that is not registered is rejected, never allowed through or cross-checked.
                var api = hmsApi
                    ?? (options.HMS.SysIntegrity.Enabled && !options.HMS.UserDetect.Enabled
                        ? HMSAttestationApi.SysIntegrity
                        : HMSAttestationApi.UserDetect);

                return api switch
                {
                    HMSAttestationApi.SysIntegrity => hmsSysIntegrityService != null && await hmsSysIntegrityService.VerifyTokenAsync(token, nonce),
                    _ => hmsUserDetectService != null && await hmsUserDetectService.VerifyTokenAsync(token),
                };
            }
            return true;
        }
    }
}
