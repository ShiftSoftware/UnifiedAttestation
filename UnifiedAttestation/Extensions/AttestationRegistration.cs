using Azure.Identity;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShiftSoftware.UnifiedAttestation.Models;
using ShiftSoftware.UnifiedAttestation.Models.HMS;
using ShiftSoftware.UnifiedAttestation.Services;
using ShiftSoftware.UnifiedAttestation.Services.Interfaces;
using System;

namespace ShiftSoftware.UnifiedAttestation.Extensions
{
    /// <summary>
    /// Shared registration logic used by both the plain <see cref="IServiceCollection"/> entry point and the Azure
    /// Functions worker entry point, so the two cannot drift apart.
    /// </summary>
    internal static class AttestationRegistration
    {
        /// <summary>
        /// Validates the supplied options and registers every attestation service they call for.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a required option is missing or invalid.</exception>
        internal static void RegisterCore(IServiceCollection services, AttestationOptions rootOptions)
        {
            if (rootOptions.UseFakeServices)
            {
                services.AddSingleton<IUnifiedAttestationService, FakeAttestationService>();

                // Used by both real and fake service for middleware configuration
                services.AddSingleton(rootOptions);

                return;
            }

            var firebaseEnabled = rootOptions.Firebase != null && rootOptions.Firebase.Enabled;
            var hmsEnabled = rootOptions.HMS != null && rootOptions.HMS.Enabled;

            // UserDetect and SysIntegrity are independent and may both be enabled. At request time the caller selects one
            // (via the HMS API header), so nothing here needs to pick one over the other.
            var userDetectEnabled = hmsEnabled && rootOptions.HMS!.UserDetect.Enabled;
            var sysIntegrityEnabled = hmsEnabled && rootOptions.HMS!.SysIntegrity.Enabled;

            // The HMS API header selects the verifier for a Huawei request, so its key must be non-empty whenever HMS is on.
            if (hmsEnabled)
                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.HMSApiHeaderKey, nameof(rootOptions.HMSApiHeaderKey));

            // 1. Key Vault. Only Firebase reads from it, for its service account certificate. HMS SysIntegrity takes its
            // root CA directly from configuration (typically an app setting that is a Key Vault reference resolved by
            // the platform before startup), so it needs no client here.
            if (firebaseEnabled)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.Firebase!.KeyVaultURI, nameof(rootOptions.Firebase.KeyVaultURI));

                if (!Uri.TryCreate(rootOptions.Firebase!.KeyVaultURI, UriKind.Absolute, out Uri? parsedKeyVaultUri))
                {
                    throw new ArgumentException($"The provided Key Vault URI '{rootOptions.Firebase!.KeyVaultURI}' is not a valid absolute URI.", nameof(rootOptions.Firebase.KeyVaultURI));
                }

                services.AddAzureClients(clientBuilder =>
                {
                    // Firebase needs the service account certificate together with its private key.
                    clientBuilder.AddCertificateClient(parsedKeyVaultUri)
                                 .WithCredential(new DefaultAzureCredential());
                });
            }

            // 2. Register Firebase Services
            if (firebaseEnabled)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.Firebase!.ProjectNumber, nameof(rootOptions.Firebase.ProjectNumber));
                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.Firebase!.ServiceAccountKeyVaultCertificate, nameof(rootOptions.Firebase.ServiceAccountKeyVaultCertificate));
                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.Firebase!.ServiceAccountEmail, nameof(rootOptions.Firebase.ServiceAccountEmail));

                services.AddSingleton<FirebaseAppCheckService>();
                services.Configure<FirebaseAppCheckOptions>(options =>
                {
                    options.ProjectNumber = rootOptions.Firebase.ProjectNumber;
                    options.ServiceAccountEmail = rootOptions.Firebase.ServiceAccountEmail;
                    options.ServiceAccountKeyVaultCertificate = rootOptions.Firebase.ServiceAccountKeyVaultCertificate;
                });
            }

            // 3. Register HMS Services. Each enabled API is wired up independently, and both can be on at once. They need
            // completely different configuration: UserDetect needs OAuth credentials, SysIntegrity needs a root CA.
            if (userDetectEnabled)
            {
                var userDetect = rootOptions.HMS!.UserDetect;

                ArgumentException.ThrowIfNullOrWhiteSpace(userDetect.AppId, nameof(userDetect.AppId));
                ArgumentException.ThrowIfNullOrWhiteSpace(userDetect.ClientId, nameof(userDetect.ClientId));
                ArgumentException.ThrowIfNullOrWhiteSpace(userDetect.ClientSecret, nameof(userDetect.ClientSecret));

                services.AddSingleton<HMSUserDetectService>();
                services.Configure<HMSUserDetectOptions>(options =>
                {
                    options.AppId = userDetect.AppId;
                    options.ClientId = userDetect.ClientId;
                    options.ClientSecret = userDetect.ClientSecret;
                });
            }

            if (sysIntegrityEnabled)
            {
                var sysIntegrity = rootOptions.HMS!.SysIntegrity;

                ArgumentException.ThrowIfNullOrWhiteSpace(sysIntegrity.RootCertificatePem, nameof(sysIntegrity.RootCertificatePem));

                // An empty allow list would accept nothing at runtime, which is a silent misconfiguration. Fail here instead.
                if (sysIntegrity.AllowedPackageNames is null || sysIntegrity.AllowedPackageNames.Count == 0)
                {
                    throw new ArgumentException("At least one allowed package name must be configured for HMS SysIntegrity.", nameof(sysIntegrity.AllowedPackageNames));
                }

                if (sysIntegrity.AllowedApkCertificateDigests is null || sysIntegrity.AllowedApkCertificateDigests.Count == 0)
                {
                    throw new ArgumentException("At least one allowed app signing certificate digest must be configured for HMS SysIntegrity.", nameof(sysIntegrity.AllowedApkCertificateDigests));
                }

                // The nonce header carries the value validated when RequireNonce is on, so its key must be non-empty.
                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.NonceHeaderKey, nameof(rootOptions.NonceHeaderKey));

                services.AddSingleton<HMSSysIntegrityService>();
                services.TryAddSingleton<INonceValidator, EchoNonceValidator>();
                services.Configure<HMSSysIntegrityOptions>(options =>
                {
                    options.RootCertificatePem = sysIntegrity.RootCertificatePem;
                    options.AllowedPackageNames = sysIntegrity.AllowedPackageNames;
                    options.AllowedApkCertificateDigests = sysIntegrity.AllowedApkCertificateDigests;
                    options.MaxTokenAge = sysIntegrity.MaxTokenAge;
                    options.ClockSkew = sysIntegrity.ClockSkew;
                    options.RequireNonce = sysIntegrity.RequireNonce;
                });
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.TokenHeaderKey, nameof(rootOptions.TokenHeaderKey));
            ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.PlatformHeaderKey, nameof(rootOptions.PlatformHeaderKey));

            // 4. Register the Unified Service
            services.AddSingleton<IUnifiedAttestationService, UnifiedAttestationService>();

            // Used by both real and fake service for middleware configuration
            services.AddSingleton(rootOptions);
        }
    }
}
