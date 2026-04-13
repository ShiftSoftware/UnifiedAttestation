namespace ShiftSoftware.UnifiedAttestation.Models
{
    public class FirebaseAppCheckOptions
    {
        /// <summary>
        /// Gets or sets the Firebase project number used to identify the Firebase project.
        /// This value is used to construct the issuer and audience claims during App Check token validation.
        /// </summary>
        /// <remarks>
        /// This must match the numeric project number (not the project ID) found in the Firebase console
        /// under <c>Project Settings &gt; General</c>. It is used to build the <c>projects/{number}</c>
        /// resource path required by the Firebase App Check API.
        /// </remarks>
        public string FirebaseProjectNumber { get; set; } = default!;

        /// <summary>
        /// Gets or sets the email address of the Firebase service account used to authenticate with the Firebase App Check API.
        /// </summary>
        /// <remarks>
        /// This is the <c>client_email</c> field from the Firebase service account JSON key file,
        /// typically in the format <c>{account-name}@{project-id}.iam.gserviceaccount.com</c>.
        /// It is used together with <see cref="ServiceAccountKeyVaultCertificate"/> to create
        /// the <see cref="Google.Apis.Auth.OAuth2.ServiceAccountCredential"/> for API authentication.
        /// </remarks>
        public string ServiceAccountEmail { get; set; } = default!;
        /// <summary>
        /// Gets or sets the name or identifier of the Azure Key Vault certificate containing the Firebase service account credentials.
        /// This certificate is used to authenticate with Firebase App Check API for token verification operations.
        /// </summary>
        /// <remarks>
        /// The certificate should be in PKCS#12 format and contain the private key for the Firebase service account.
        /// This provides a secure alternative to storing service account credentials directly in configuration.
        /// </remarks>
        public string ServiceAccountKeyVaultCertificate { get; set; } = default!;
    }
}
