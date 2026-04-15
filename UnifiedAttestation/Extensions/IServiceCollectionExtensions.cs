using Azure.Identity;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using ShiftSoftware.UnifiedAttestation.Models;
using ShiftSoftware.UnifiedAttestation.Models.HMS;
using ShiftSoftware.UnifiedAttestation.Services;
using ShiftSoftware.UnifiedAttestation.Services.Interfaces;
using System;

namespace ShiftSoftware.UnifiedAttestation.Extensions
{
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the Unified Attestation Service (Firebase App Check and Huawei HMS) and its required configurations and dependencies into the dependency injection container. This method also performs fail-fast validation of the provided options to ensure that all required parameters are present and valid before the application starts 
        /// </summary>
        /// <param name="services">The dependency injection container.</param>
        /// <param name="configureOptions">The delegate used to configure attestation options.</param>
        /// <returns>The builder instance to allow method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
        /// <exception cref="ArgumentException">Thrown when any of the required string parameters are null or whitespace, or if the Key Vault URI is invalid.</exception>
        public static IServiceCollection AddAttestationVerificationServices(
        this IServiceCollection services,
         Action<AttestationOptions> configureOptions
        )
        {
            // 1. Fail-Fast Parameter Validation
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            // 2. Create a temporary instance to wire up internal services that need the data at startup
            var rootOptions = new AttestationOptions();
            configureOptions(rootOptions);

            if (rootOptions.UseFakeServices)
            {
                services.AddSingleton<IUnifiedAttestationService, FakeAttestationService>();
            }
            else
            {
                // 3. Register Firebase Services
                if (rootOptions.Firebase.Enabled)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.Firebase.ProjectNumber, nameof(rootOptions.Firebase.ProjectNumber));
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.Firebase.ServiceAccountKeyVaultCertificate, nameof(rootOptions.Firebase.ServiceAccountKeyVaultCertificate));
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.Firebase.ServiceAccountEmail, nameof(rootOptions.Firebase.ServiceAccountEmail));
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.Firebase.KeyVaultURI, nameof(rootOptions.Firebase.KeyVaultURI));

                    if (!Uri.TryCreate(rootOptions.Firebase.KeyVaultURI, UriKind.Absolute, out Uri parsedKeyVaultUri))
                    {
                        throw new ArgumentException($"The provided Key Vault URI '{rootOptions.Firebase.KeyVaultURI}' is not a valid absolute URI.", nameof(rootOptions.Firebase.KeyVaultURI));
                    }

                    services.AddSingleton<FirebaseAppCheckService>();
                    services.Configure<FirebaseAppCheckOptions>(options =>
                    {
                        options.ProjectNumber = rootOptions.Firebase.ProjectNumber;
                        options.ServiceAccountEmail = rootOptions.Firebase.ServiceAccountEmail;
                        options.ServiceAccountKeyVaultCertificate = rootOptions.Firebase.ServiceAccountKeyVaultCertificate;
                        options.KeyVaultURI = rootOptions.Firebase.KeyVaultURI;
                    });

                    // 3. Register Azure Key Vault Client safely using the validated URI
                    services.AddAzureClients(clientBuilder =>
                    {
                        clientBuilder.AddCertificateClient(parsedKeyVaultUri)
                                     .WithCredential(new DefaultAzureCredential());
                    });
                }

                // 4. Register HMS Services
                if (rootOptions.HMS != null && rootOptions.HMS.Enabled)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.HMS.AppId, nameof(rootOptions.HMS.AppId));
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.HMS.ClientId, nameof(rootOptions.HMS.ClientId));
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.HMS.ClientSecret, nameof(rootOptions.HMS.ClientSecret));

                    services.AddSingleton<HMSUserDetectService>();
                    services.Configure<HMSUserDetectOptions>(options =>
                    {
                        options.AppId = rootOptions.HMS.AppId;
                        options.ClientId = rootOptions.HMS.ClientId;
                        options.ClientSecret = rootOptions.HMS.ClientSecret;
                    });
                }
                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.PlatformHeaderKey, nameof(rootOptions.PlatformHeaderKey));

                // 5. Register the Unified Service
                services.AddSingleton<IUnifiedAttestationService, UnifiedAttestationService>();
            }

            // Used by both real and fake service for middleware configuration
            services.AddSingleton(rootOptions);

            return services;
        }
    }
}
