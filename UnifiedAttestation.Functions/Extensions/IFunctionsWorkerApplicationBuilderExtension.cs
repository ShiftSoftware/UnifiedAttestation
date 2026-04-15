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
        /// <param name="configureOptions">The delegate used to configure attestation options.</param>
        /// <returns>The builder instance to allow method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
        /// <exception cref="ArgumentException">Thrown when required configured values are null or whitespace, or if the Key Vault URI is invalid.</exception>
        public static IFunctionsWorkerApplicationBuilder AddAttestationVerification(
            this IFunctionsWorkerApplicationBuilder builder,
            Action<AttestationOptions> configureOptions)
        {
            // 1. Fail-Fast Parameter Validation
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureOptions);

            // 2. Create a temporary instance to wire up internal services that need the data at startup
            var rootOptions = new AttestationOptions();
            configureOptions(rootOptions);

            if (rootOptions.UseFakeServices)
            {
                builder.Services.AddSingleton<IUnifiedAttestationService, FakeAttestationService>();
            }
            else
            {
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

                    // 3. Register Firebase Services
                    builder.Services.AddSingleton<FirebaseAppCheckService>();
                    builder.Services.Configure<FirebaseAppCheckOptions>(options =>
                    {
                        options.ProjectNumber = rootOptions.Firebase.ProjectNumber;
                        options.ServiceAccountEmail = rootOptions.Firebase.ServiceAccountEmail;
                        options.ServiceAccountKeyVaultCertificate = rootOptions.Firebase.ServiceAccountKeyVaultCertificate;
                        options.KeyVaultURI = rootOptions.Firebase.KeyVaultURI;
                    });

                    // 4. Register Azure Key Vault Client safely using the validated URI
                    builder.Services.AddAzureClients(clientBuilder =>
                    {
                        clientBuilder.AddCertificateClient(parsedKeyVaultUri)
                                     .WithCredential(new DefaultAzureCredential());
                    });
                }

                if (rootOptions.HMS != null && rootOptions.HMS.Enabled)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.HMS.AppId, nameof(rootOptions.HMS.AppId));
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.HMS.ClientId, nameof(rootOptions.HMS.ClientId));
                    ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.HMS.ClientSecret, nameof(rootOptions.HMS.ClientSecret));

                    // 5. Register HMS Services
                    builder.Services.AddSingleton<HMSUserDetectService>();
                    builder.Services.Configure<HMSUserDetectOptions>(options =>
                    {
                        options.AppId = rootOptions.HMS.AppId;
                        options.ClientId = rootOptions.HMS.ClientId;
                        options.ClientSecret = rootOptions.HMS.ClientSecret;
                    });
                }

                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.TokenHeaderKey, nameof(rootOptions.TokenHeaderKey));
                ArgumentException.ThrowIfNullOrWhiteSpace(rootOptions.PlatformHeaderKey, nameof(rootOptions.PlatformHeaderKey));

                // 6. Register the Unified Service
                builder.Services.AddSingleton<IUnifiedAttestationService, UnifiedAttestationService>();
            }

            rootOptions.HMS = null;
            rootOptions.Firebase = null;

            // Used by both real and fake service for middleware configuration
            builder.Services.AddSingleton(rootOptions);

            // 7. Register conditional Middleware with robust null checks
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
