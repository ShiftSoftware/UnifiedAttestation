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

                if (string.IsNullOrWhiteSpace(verificationToken) || string.IsNullOrWhiteSpace(platformHeader))
                {
                    await new UnauthorizedResult().ExecuteResultAsync(new ActionContext
                    {
                        HttpContext = httpContext
                    });
                    return;
                }
                AttestationPlatform platform;

                Enum.TryParse(platformHeader, out platform);

                var validToken = await attestationService.VerifyTokenAsync(verificationToken, platform, withReplayProtection);

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
