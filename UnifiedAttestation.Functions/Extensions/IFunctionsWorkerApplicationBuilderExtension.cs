using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShiftSoftware.UnifiedAttestation.Attributes;
using ShiftSoftware.UnifiedAttestation.Extensions;
using ShiftSoftware.UnifiedAttestation.Functions.Middlewares;
using ShiftSoftware.UnifiedAttestation.Models;
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

            // 3. Register the services, shared with the plain IServiceCollection entry point
            AttestationRegistration.RegisterCore(builder.Services, rootOptions);

            // 4. Register conditional Middleware with robust null checks
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
