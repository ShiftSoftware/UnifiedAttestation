using System;
using System.Collections.Generic;

namespace ShiftSoftware.UnifiedAttestation.Models.HMS
{
    /// <summary>
    /// Represents configuration options for Huawei Mobile Services SafetyDetect SysIntegrity verification.
    /// </summary>
    /// <remarks>
    /// SysIntegrity results are verified entirely on the server without calling Huawei, so no OAuth credentials are
    /// needed. The HUAWEI CBG Root CA certificate is required instead, supplied through
    /// <see cref="RootCertificatePem"/>.
    /// </remarks>
    public class HMSSysIntegrityOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether SysIntegrity verification is enabled. Defaults to false.
        /// </summary>
        /// <remarks>
        /// SysIntegrity can run alongside UserDetect. The API used for a Huawei request is selected by the caller (the
        /// HMS API header at the middleware level, see <see cref="AttestationOptions.HMSApiHeaderKey"/>), defaulting to
        /// UserDetect unless SysIntegrity is the only enabled HMS API, which keeps existing Huawei clients working.
        /// </remarks>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the HUAWEI CBG Root CA, as either PEM text (<c>-----BEGIN CERTIFICATE-----</c>) or Base64
        /// encoded DER bytes. Required.
        /// </summary>
        /// <remarks>
        /// Supply the certificate through configuration. On App Service or Functions this pairs with an app setting that
        /// is a Key Vault reference (<c>@Microsoft.KeyVault(SecretUri=...)</c>): the platform resolves the reference
        /// before the app starts, so nothing is read from the vault at runtime. Download the root CA from the Huawei
        /// SafetyDetect SysIntegrity development guide.
        /// </remarks>
        public string? RootCertificatePem { get; set; }

        /// <summary>
        /// Gets or sets the package names that are accepted in the <c>apkPackageName</c> field of the SysIntegrity payload.
        /// </summary>
        /// <remarks>
        /// At least one entry is required. Without this, a valid JWS obtained by any other Huawei application could be
        /// replayed against this API.
        /// </remarks>
        public List<string> AllowedPackageNames { get; set; } = new();

        /// <summary>
        /// Gets or sets the Base64 encoded SHA-256 digests of the app signing certificates that are accepted in the
        /// <c>apkCertificateDigestSha256</c> field of the SysIntegrity payload.
        /// </summary>
        /// <remarks>
        /// At least one entry is required. This is what stops a repackaged or re-signed build of your own application
        /// from passing verification.
        /// </remarks>
        public List<string> AllowedApkCertificateDigests { get; set; } = new();

        /// <summary>
        /// Gets or sets the maximum accepted age of the SysIntegrity payload, measured from its Huawei-signed
        /// <c>timestampMs</c> field. Defaults to 90 seconds. This window is the primary replay bound, so keep it short.
        /// </summary>
        public TimeSpan MaxTokenAge { get; set; } = TimeSpan.FromSeconds(90);

        /// <summary>
        /// Gets or sets the tolerated clock skew when a payload's <c>timestampMs</c> is in the future. Defaults to 1 minute.
        /// </summary>
        public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets a value indicating whether a nonce must be validated against the <c>nonce</c> field of the
        /// payload. Defaults to false.
        /// </summary>
        /// <remarks>
        /// Left off by default because the payload's <c>timestampMs</c> is signed by Huawei, so <see cref="MaxTokenAge"/>
        /// already bounds how long a result stays usable. Turn this on only together with a server issued nonce: the
        /// nonce is validated through <see cref="Services.Interfaces.INonceValidator"/>, whose default implementation
        /// merely echo-compares the client supplied nonce with the payload nonce and so adds no replay protection on its
        /// own. Register your own implementation for server issued, single use nonces before enabling this.
        /// </remarks>
        public bool RequireNonce { get; set; } = false;
    }
}
