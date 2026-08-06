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
        /// Huawei Mobile Services configuration options. This is required to enable HMS UserDetect or HMS SysIntegrity token verification.
        /// </summary>
        public HMSOptions HMS { get; set; } = new();

        /// <summary>
        /// The HTTP header key used to pass the attestation token. Defaults to "Verification-Token".
        /// </summary>
        public string TokenHeaderKey { get; set; } = "Verification-Token";
        /// <summary>
        /// The HTTP header key used to pass the attestation platform. Defaults to "Platform".
        /// </summary>
        public string PlatformHeaderKey { get; set; } = "Platform";
        /// <summary>
        /// The HTTP header key used to select which HMS SafetyDetect API a Huawei token is verified with. Defaults to "HMS-Api".
        /// </summary>
        /// <remarks>
        /// The value is parsed as <see cref="ShiftSoftware.UnifiedAttestation.Enums.HMSAttestationApi"/> ("UserDetect" or "SysIntegrity"), case-insensitively.
        /// When the header is absent or unrecognised the request defaults to UserDetect, unless SysIntegrity is the only
        /// enabled HMS API. Only used by the Huawei path; ignored by every other provider.
        /// </remarks>
        public string HMSApiHeaderKey { get; set; } = "HMS-Api";
        /// <summary>
        /// The HTTP header key used to pass the nonce the client supplied to the HMS SysIntegrity API. Defaults to "Verification-Nonce".
        /// </summary>
        /// <remarks>
        /// Only used by the HMS SysIntegrity path, and only when <see cref="HMS.HMSSysIntegrityOptions.RequireNonce"/> is
        /// enabled. Ignored by every other provider.
        /// </remarks>
        public string NonceHeaderKey { get; set; } = "Verification-Nonce";

        /// <summary>
        /// Indicates whether to use a fake attestation service for testing purposes. Defaults to false.
        /// </summary>
        public bool UseFakeServices { get; set; } = false;
    }
}
