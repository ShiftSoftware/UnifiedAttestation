namespace ShiftSoftware.UnifiedAttestation.Models.HMS
{
    /// <summary>
    /// Represents configuration options for Huawei Mobile Services User Detect integration.
    /// </summary>
    public class HMSUserDetectOptions
    {
        /// <summary>
        /// Gets or sets the HMS client identifier used to authenticate API requests.
        /// </summary>
        /// <remarks>
        /// Read the Huawei Open Platform OAuth documentation to know how to obtain the client id:
        /// <see href="https://developer.huawei.com/consumer/en/doc/HMSCore-Guides/open-platform-oauth-0000001053629189#section12493191334711"/>.
        /// </remarks>
        public string ClientId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the HMS client secret associated with the configured client identifier.
        /// </summary>
        /// <remarks>
        /// Read the Huawei Open Platform OAuth documentation to know how to obtain the client secret:
        /// <see href="https://developer.huawei.com/consumer/en/doc/HMSCore-Guides/open-platform-oauth-0000001053629189#section12493191334711"/>.
        /// </remarks>
        public string ClientSecret { get; set; } = default!;

        /// <summary>
        /// Gets or sets the App ID applied in AppGallery Connect.
        /// </summary>
        /// <remarks>
        /// Obtain this value from AppGallery Connect
        /// </remarks>
        public string AppId { get; set; } = default!;

        /// <summary>
        /// Gets or sets a value indicating whether the HMS User Detect integration is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
