using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ShiftSoftware.UnifiedAttestation.Enums;
using ShiftSoftware.UnifiedAttestation.Models;
using ShiftSoftware.UnifiedAttestation.Services.Interfaces;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ShiftSoftware.UnifiedAttestation.Services
{
    public class UnifiedAttestationService : IUnifiedAttestationService
    {
        private readonly AttestationOptions options;
        private readonly FirebaseAppCheckService? firebaseAppCheckService;
        private readonly HMSUserDetectService? hmsUserDetectService;
        public UnifiedAttestationService(AttestationOptions options, FirebaseAppCheckService? firebaseAppCheckService = null, HMSUserDetectService? hmsUserDetectService = null)
        {
            this.options = options;
            this.firebaseAppCheckService = firebaseAppCheckService;
            this.hmsUserDetectService = hmsUserDetectService;
        }

        /// <summary>
        /// Routes the attestation token to the appropriate verification service based on the client's platform.
        /// </summary>
        /// <param name="token">The attestation token provided by the client request mainly Firebase App Check Token for iOS and Android, HMS UserDetect Response Token for HMS/Huawei.</param>
        /// <param name="platform">The OS or attestation provider the token originated from.</param>
        /// <param name="withReplayProtection">
        /// If true, performs a strict, one-time-use network verification for Firebase App Check Token only to prevent replay attacks. 
        /// Defaults to false (which uses fast, offline JWT validation where supported).
        /// </param>
        /// <returns>True if the token is valid, trusted, otherwise, false.</returns>
        public async ValueTask<bool> VerifyTokenAsync(string token, AttestationPlatform platform, bool? withReplayProtection = false)
        {
            if (platform is (AttestationPlatform.Android or AttestationPlatform.iOS) && options.Firebase.Enabled && firebaseAppCheckService != null)
            {
                if (withReplayProtection is true)
                    return await firebaseAppCheckService.VerifyTokenWithReplayProtectionAsync(token);

                return await firebaseAppCheckService.VerifyTokenAsync(token);
            }
            else if (platform is AttestationPlatform.Huawei && options.HMS.Enabled && hmsUserDetectService != null) { 
                return await hmsUserDetectService.VerifyTokenAsync(token);
            }
            return true;
        }
    }
}
