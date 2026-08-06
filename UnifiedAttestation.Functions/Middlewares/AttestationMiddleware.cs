using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using ShiftSoftware.UnifiedAttestation.Attributes;
using ShiftSoftware.UnifiedAttestation.Enums;
using ShiftSoftware.UnifiedAttestation.Functions.Extensions;
using ShiftSoftware.UnifiedAttestation.Functions.Utilities;
using ShiftSoftware.UnifiedAttestation.Models;
using ShiftSoftware.UnifiedAttestation.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;


namespace ShiftSoftware.UnifiedAttestation.Functions.Middlewares
{
    internal class AttestationMiddleware : IFunctionsWorkerMiddleware
    {
        async Task IFunctionsWorkerMiddleware.Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            var methodInfo = context.GetTargetFunctionMethod();
            var attribute = AttributeUtility.GetAttribute<ValidateAttestationAttribute>(methodInfo);

            if (attribute is null)
            {
                await next(context);
            }
            else
            {
                bool withReplayProtection = attribute.Value.attribute?.WithReplayProtection ?? false;

                var httpContext = context.GetHttpContext()!;

                var serviceProvider = context.InstanceServices; 

                var attestationService = serviceProvider.GetRequiredService<IUnifiedAttestationService>();
                var attestationOptions = serviceProvider.GetRequiredService<AttestationOptions>();

                var verificationToken = httpContext.Request.Headers
                    .LastOrDefault(x => x.Key.ToLower().Equals(attestationOptions.TokenHeaderKey, StringComparison.InvariantCultureIgnoreCase))
                    .Value.LastOrDefault();

                var platformHeader = httpContext.Request.Headers.LastOrDefault(x => x.Key.ToLower().Equals(attestationOptions.PlatformHeaderKey, StringComparison.InvariantCultureIgnoreCase)).Value.LastOrDefault();

                // Selects the HMS API for a Huawei request. Optional: a missing or unrecognised value leaves this null,
                // and the verification service defaults it (to UserDetect unless SysIntegrity is the only enabled API).
                var hmsApiHeader = httpContext.Request.Headers
                    .LastOrDefault(x => x.Key.ToLower().Equals(attestationOptions.HMSApiHeaderKey, StringComparison.InvariantCultureIgnoreCase))
                    .Value.LastOrDefault();

                HMSAttestationApi? hmsApi = Enum.TryParse<HMSAttestationApi>(hmsApiHeader, ignoreCase: true, out var parsedHmsApi)
                    && Enum.IsDefined(parsedHmsApi)
                    ? parsedHmsApi
                    : null;

                // Only the HMS SysIntegrity path uses this, so a missing nonce is not rejected here. That service
                // decides whether the nonce was required, which surfaces as a 403 rather than a 401.
                var nonceHeader = httpContext.Request.Headers
                    .LastOrDefault(x => x.Key.ToLower().Equals(attestationOptions.NonceHeaderKey, StringComparison.InvariantCultureIgnoreCase))
                    .Value.LastOrDefault();

                if (string.IsNullOrWhiteSpace(verificationToken) || string.IsNullOrWhiteSpace(platformHeader))
                {
                    await new UnauthorizedResult().ExecuteResultAsync(new ActionContext
                    {
                        HttpContext = httpContext
                    });
                    return;
                }
                AttestationPlatform platform;

                Enum.TryParse(platformHeader,ignoreCase: true, out platform);

                var validToken = await attestationService.VerifyTokenAsync(verificationToken, platform, withReplayProtection, nonceHeader, hmsApi);

                if (!validToken)
                {
                    await new StatusCodeResult(StatusCodes.Status403Forbidden).ExecuteResultAsync(new ActionContext
                    {
                        HttpContext = httpContext
                    });
                    return;
                }

                await next(context);
            }
        }
    }
}
