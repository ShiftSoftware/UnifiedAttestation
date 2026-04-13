# ShiftSoftware Unified Attestation
A robust, unified security layer for Azure Functions (Isolated Worker Model) designed to verify the integrity of mobile client requests across iOS, Android, and Huawei devices. It ensures incoming requests originate from legitimate, untampered versions of your mobile applications running on genuine devices.

## Features
- **Multi-Platform Support**: Supports Apple App Attest, Google Play Integrity (via Firebase App Check), and Huawei Mobile Services (HMS) SysIntegrity.
- **Replay Protection**: Optional strict, network-based one-time-use verification to prevent replay attacks.
- **Azure Functions Middleware**: Clean, declarative security using the [ValidateAttestation] attribute.
- **Secure Credential Management**: Built-in integration with Azure Key Vault for certificate management.
- **Development Bypass**: Seamless local development experience using the AttestationBypassService when AZURE_FUNCTIONS_ENVIRONMENT is set to Development.
- **High Performance**: Intelligent caching of server-to-server OAuth tokens to respect API rate limits and minimize latency.

## Packages

### `ShiftSoftware.UnifiedAttestation`
Core services, models, and abstractions. Contains the platform-agnostic business logic for Firebase and HMS attestation.

### `ShiftSoftware.UnifiedAttestation.Functions`
Azure Functions Isolated Worker integration. Provides the dependency injection extensions and middleware pipeline.

## Installation

Install the packages via the .NET CLI:
```bash
dotnet add package ShiftSoftware.UnifiedAttestation.Functions
```


> Note: Installing the Functions package will automatically pull in the Core package.

## Configuration

In your `Program.cs`, register the attestation services using the `AddAttestationVerification` extension method:
```csharp
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.AddAttestationVerification(
            firebaseProjectNumber: Environment.GetEnvironmentVariable("FIREBASE_PROJECT_NUMBER"),
            serviceAccountKeyVaultCertificate: Environment.GetEnvironmentVariable("FIREBASE_CERT_NAME"),
            serviceAccountEmail: Environment.GetEnvironmentVariable("FIREBASE_CLIENT_EMAIL"),
            keyVaultUri: Environment.GetEnvironmentVariable("KEY_VAULT_URI"),
            hmsAppId: Environment.GetEnvironmentVariable("HMS_APP_ID"),
            hmsClientId: Environment.GetEnvironmentVariable("HMS_CLIENT_ID"),
            hmsClientSecret: Environment.GetEnvironmentVariable("HMS_CLIENT_SECRET"),
            headerKey: "X-App-Attestation-Token" // Optional: Defaults to Verification-Token
            useFakeService: true, // Optional: Defaults to false. Set to true to bypass validation in Development environment for easier local testing.
        );
    })
    .Build();

host.Run();
```

## Usage

### Securing an Endpoint

Apply the `[ValidateAttestation]` attribute to any HTTP trigger function you wish to secure. The middleware will intercept the request, validate the token provided in the header, and reject unauthorized requests before they hit your business logic.

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Http;

public class AttestationEndpoints
{
    [Function(nameof(AttestedPing))]
    [ValidateAttestation(withReplayProtection: false)]
    public IActionResult AttestedPing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "secure/ping")] HttpRequest req)
    {
        return new OkObjectResult(new {
            message = "Success!",
            deviceStatus = "Attested & Trusted"
        });
    }
}
```

> Note: Keep `AuthorizationLevel.Anonymous` on your HTTP triggers so the custom middleware can handle the rejection response.

## Replay Protection

For high-value endpoints (like payments or login), you can enable Replay Protection. This forces the token to be consumed via a network call to the provider, ensuring it can never be used again.

```csharp
[ValidateAttestation(withReplayProtection: true)]
```

## Development Mode

When running locally using Azure Functions Core Tools, the `useFakeService` parameter of the AddAttestationVerification function can be used to bypass token validation in `Development` environment.

The registration logic will automatically swap out the real validation service for the `FakeAttestationService`. This allows front-end developers to test endpoints by simply passing any string (e.g., `Verification-Token: dev-test`) without needing to configure physical devices or real App Check projects.
