namespace ShiftSoftware.UnifiedAttestation.Models.HMS
{
    /// <summary>
    /// Represents configuration options for Huawei Mobile Services attestation.
    /// </summary>
    /// <remarks>
    /// UserDetect and SysIntegrity can both be enabled at the same time. The API used for a given Huawei request is
    /// selected by the caller (the HMS API header at the middleware level, see
    /// <see cref="AttestationOptions.HMSApiHeaderKey"/>). When none is selected the request defaults to UserDetect,
    /// unless SysIntegrity is the only enabled API, so existing UserDetect clients keep working unchanged while newer
    /// clients opt into SysIntegrity by naming it.
    /// </remarks>
    public class HMSOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether Huawei Mobile Services attestation is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the HMS UserDetect options. Enabled by default; see <see cref="HMSUserDetectOptions.Enabled"/>.
        /// </summary>
        public HMSUserDetectOptions UserDetect { get; set; } = new();

        /// <summary>
        /// Gets or sets the HMS SysIntegrity options. Disabled by default; see <see cref="HMSSysIntegrityOptions.Enabled"/>.
        /// </summary>
        public HMSSysIntegrityOptions SysIntegrity { get; set; } = new();
    }
}
