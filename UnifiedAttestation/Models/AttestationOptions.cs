using ShiftSoftware.UnifiedAttestation.Models.HMS;

namespace ShiftSoftware.UnifiedAttestation.Models
{
    public class AttestationOptions
    {
        /// <summary>
        /// Firebase App Check configuration options. This is required to enable Firebase App Check token verification.
        /// </summary>
        public FirebaseAppCheckOptions Firebase { get; set; } = new();
        /// <summary>
        /// Huawei Mobile Services User Detect configuration options. This is required to enable HMS User Detect token verification.
        /// </summary>
        public HMSUserDetectOptions HMS { get; set; } = new();

        /// <summary>
        /// The HTTP header key used to pass the attestation token. Defaults to "Verification-Token".
        /// </summary>
        public string HeaderKey { get; set; } = "Verification-Token";

        /// <summary>
        /// Indicates whether to use a fake attestation service for testing purposes. Defaults to false.
        /// </summary>
        public bool UseFakeServices { get; set; } = false;
    }
}
