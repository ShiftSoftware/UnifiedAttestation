using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShiftSoftware.UnifiedAttestation.Attributes;
using ShiftSoftware.UnifiedAttestation.Functions.Middlewares;
using ShiftSoftware.UnifiedAttestation.Models;
using ShiftSoftware.UnifiedAttestation.Models.HMS;
using ShiftSoftware.UnifiedAttestation.Services;
using ShiftSoftware.UnifiedAttestation.Services.Interfaces;
using System;
using System.Linq;

namespace ShiftSoftware.UnifiedAttestation.Functions.Extensions
{
    public static class IFunctionsWorkerApplicationBuilderExtension
    {
        /// <summary>
        /// Registers the Unified Attestation Service (Firebase App Check and Huawei HMS) and its required middleware 
        /// into the Azure Functions worker pipeline.
        /// </summary>
        /// <param name="builder">The functions worker application builder.</param>
        /// <param name="firebaseProjectNumber">The Google Cloud project number associated with Firebase.</param>
        /// <param name="serviceAccountKeyVaultCertificate">The name of the certificate stored in Azure Key Vault for the Google Service Account.</param>
        /// <param name="serviceAccountEmail">The email address of the Google Service Account.</param>
        /// <param name="keyVaultUri">The absolute URI of the Azure Key Vault containing the certificate.</param>
        /// <param name="hmsAppId">The Huawei Mobile Services (HMS) Application ID.</param>
        /// <param name="hmsClientId">The Huawei Mobile Services (HMS) Client ID.</param>
        /// <param name="hmsClientSecret">The Huawei Mobile Services (HMS) Client Secret.</param>
        /// <param name="headerKey">The HTTP header key used to pass the attestation token. Defaults to "Verification-Token".</param>
        /// <param name="useFakeService">Indicates whether to use a fake attestation service for testing purposes. Defaults to false.</param>
        /// <returns>The builder instance to allow method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
        /// <exception cref="ArgumentException">Thrown when any of the required string parameters are null or whitespace, or if the Key Vault URI is invalid.</exception>
        public static IFunctionsWorkerApplicationBuilder AddAttestationVerification(
            this IFunctionsWorkerApplicationBuilder builder,
            string firebaseProjectNumber,
            string serviceAccountKeyVaultCertificate,
            string serviceAccountEmail,
            string keyVaultUri,
            string hmsAppId,
            string hmsClientId,
            string hmsClientSecret,
            string headerKey = "Verification-Token",
            bool useFakeService = false)
        {
            if (useFakeService)
            {
                builder.Services.AddSingleton<IUnifiedAttestationService, FakeAttestationService>();
            }
            else
            {
                // 1. Fail-Fast Parameter Validation
                ArgumentNullException.ThrowIfNull(builder);

                ArgumentException.ThrowIfNullOrWhiteSpace(firebaseProjectNumber, nameof(firebaseProjectNumber));
                ArgumentException.ThrowIfNullOrWhiteSpace(serviceAccountKeyVaultCertificate, nameof(serviceAccountKeyVaultCertificate));
                ArgumentException.ThrowIfNullOrWhiteSpace(serviceAccountEmail, nameof(serviceAccountEmail));
                ArgumentException.ThrowIfNullOrWhiteSpace(hmsAppId, nameof(hmsAppId));
                ArgumentException.ThrowIfNullOrWhiteSpace(hmsClientId, nameof(hmsClientId));
                ArgumentException.ThrowIfNullOrWhiteSpace(hmsClientSecret, nameof(hmsClientSecret));
                ArgumentException.ThrowIfNullOrWhiteSpace(headerKey, nameof(headerKey));


                if (!Uri.TryCreate(keyVaultUri, UriKind.Absolute, out Uri parsedKeyVaultUri))
                {
                    throw new ArgumentException($"The provided Key Vault URI '{keyVaultUri}' is not a valid absolute URI.", nameof(keyVaultUri));
                }

                // 2. Register Firebase Services
                builder.Services.AddSingleton<FirebaseAppCheckService>();
                builder.Services.Configure<FirebaseAppCheckOptions>(options =>
                {
                    options.FirebaseProjectNumber = firebaseProjectNumber;
                    options.ServiceAccountEmail = serviceAccountEmail;
                    options.ServiceAccountKeyVaultCertificate = serviceAccountKeyVaultCertificate;
                });

                // 3. Register Azure Key Vault Client safely using the validated URI
                builder.Services.AddAzureClients(clientBuilder =>
                {
                    clientBuilder.AddCertificateClient(parsedKeyVaultUri)
                                 .WithCredential(new DefaultAzureCredential());
                });

                // 4. Register HMS Services
                builder.Services.AddSingleton<HMSUserDetectService>();
                builder.Services.Configure<HMSUserDetectOptions>(options =>
                {
                    options.HMSAppId = hmsAppId;
                    options.HMSClientId = hmsClientId;
                    options.HMSClientSecret = hmsClientSecret;
                });

                // 5. Register the Unified Service
                builder.Services.AddSingleton<IUnifiedAttestationService, UnifiedAttestationService>();
            }
            // Used by both real and fake service for middleware configuration
            builder.Services.AddSingleton(options => new AttestationOptions()
            {
                HeaderKey = headerKey
            });

            // 6. Register conditional Middleware with robust null checks
            builder.UseWhen<AttestationMiddleware>(context =>
            {
                var targetMethod = context.GetTargetFunctionMethod();

                // Safety check: GetTargetFunctionMethod() can sometimes return null in the pipeline
                if (targetMethod == null) return false;

                bool hasValidateAttestationAttribute = targetMethod
                    .GetCustomAttributes(typeof(ValidateAttestationAttribute), inherit: true)
                    .Any();

                // Safety check: Ensure FunctionDefinition and InputBindings are not null before evaluating
                bool isHttpTrigger = context.FunctionDefinition?.InputBindings?.Values
                    .Any(binding => binding.Type.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase)) ?? false;

                return isHttpTrigger && hasValidateAttestationAttribute;
            });

            return builder;
        }
    }
}
