using Microsoft.Extensions.DependencyInjection;
using ShiftSoftware.UnifiedAttestation.Models;
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

            AttestationRegistration.RegisterCore(services, rootOptions);

            return services;
        }
    }
}
