# ShiftSoftware Unified Attestation
A robust, unified security layer for Azure Functions (Isolated Worker Model) designed to verify the integrity of mobile client requests across iOS, Android, and Huawei devices. It ensures incoming requests originate from legitimate, untampered versions of your mobile applications running on genuine devices.

## Features
- **Multi-Platform Support**: Supports Apple App Attest, Google Play Integrity (via Firebase App Check), and Huawei Mobile Services (HMS) SafetyDetect.
- **Two HMS APIs**: Choose between HMS **UserDetect** (fake user detection, verified through Huawei's cloud) and HMS **SysIntegrity** (device/ROM integrity, verified entirely on your own server).
- **Replay Protection**: Optional strict, network-based one-time-use verification to prevent replay attacks.
- **Azure Functions Middleware**: Clean, declarative security using the [ValidateAttestation] attribute.
- **Secure Credential Management**: Built-in integration with Azure Key Vault for certificate management.
- **Development Bypass**: Seamless local development experience using the FakeAttestationService when `UseFakeServices` is set to true.
- **High Performance**: Intelligent caching of server-to-server OAuth tokens and Key Vault certificates to respect API rate limits and minimize latency.

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
using ShiftSoftware.UnifiedAttestation.Functions.Extensions;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.AddAttestationVerification(options =>
        {
            // iOS and Android
            options.Firebase.ProjectNumber = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_NUMBER")!;
            options.Firebase.KeyVaultURI = Environment.GetEnvironmentVariable("KEY_VAULT_URI")!;
            options.Firebase.ServiceAccountKeyVaultCertificate = Environment.GetEnvironmentVariable("FIREBASE_CERT_NAME")!;
            options.Firebase.ServiceAccountEmail = Environment.GetEnvironmentVariable("FIREBASE_CLIENT_EMAIL")!;

            // Huawei, using HMS UserDetect (the default)
            options.HMS.UserDetect.AppId = Environment.GetEnvironmentVariable("HMS_APP_ID")!;
            options.HMS.UserDetect.ClientId = Environment.GetEnvironmentVariable("HMS_CLIENT_ID")!;
            options.HMS.UserDetect.ClientSecret = Environment.GetEnvironmentVariable("HMS_CLIENT_SECRET")!;

            options.TokenHeaderKey = "X-App-Attestation-Token"; // Optional: Defaults to Verification-Token
            options.UseFakeServices = true; // Optional: Defaults to false. Set to true to bypass validation in Development for easier local testing.
        });
    })
    .Build();

host.Run();
```

Outside Azure Functions, the same options are available through `services.AddAttestationVerificationServices(...)`.

Every option is validated at startup, so a missing or malformed value fails the host immediately rather than at the first request.

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

### Request headers

| Header | Default key | Required | Purpose |
| --- | --- | --- | --- |
| Token | `Verification-Token` | Yes | The Firebase App Check token, the HMS UserDetect response token, or the HMS SysIntegrity JWS. |
| Platform | `Platform` | Yes | `iOS`, `Android` or `Huawei`. Case insensitive. |
| HMS API | `HMS-Api` | Huawei only | `UserDetect` or `SysIntegrity`, case insensitive. Selects which HMS API verifies a Huawei token. Absent or unrecognised defaults to UserDetect, unless SysIntegrity is the only enabled API (see [routing](#routing-how-a-huawei-request-picks-an-api)). |
| Nonce | `Verification-Nonce` | Optional | The nonce the client passed to the SysIntegrity API. Only consulted when `RequireNonce` is on (off by default). |

A missing token or platform header is rejected with `401`. A token that fails verification is rejected with `403`.

## Replay Protection

For high-value endpoints (like payments or login), you can enable Replay Protection. This forces the token to be consumed via a network call to the provider, ensuring it can never be used again.

```csharp
[ValidateAttestation(withReplayProtection: true)]
```

> This applies to Firebase App Check only. For HMS SysIntegrity, freshness comes from the Huawei-signed `timestampMs`, so a short `MaxTokenAge` window (described below) bounds how long a captured result stays usable.

## Huawei: UserDetect and SysIntegrity

The two HMS APIs answer different questions and can be enabled at the same time.

|  | UserDetect | SysIntegrity |
| --- | --- | --- |
| Answers | Is this a fake or risky **user**? | Is this device rooted / is the **ROM** tampered with? |
| Verified by | A server-to-server call to Huawei's Risk Management Service | Your own server, offline |
| Needs | OAuth Client ID and Client Secret, App ID | The HUAWEI CBG Root CA certificate |
| Network calls | Yes, on every verification (the OAuth token is cached) | None |
| Enabled | `HMS.UserDetect.Enabled`, default **true** | `HMS.SysIntegrity.Enabled`, default **false** |

### Routing: how a Huawei request picks an API

The caller selects the API. At the middleware level this is the `HMS-Api` header (`UserDetect` or `SysIntegrity`, case insensitive); a direct `VerifyTokenAsync` caller passes the `hmsApi` argument instead. When nothing is selected:

- **UserDetect** is used by default, so existing UserDetect clients — which know nothing about the header — keep working untouched.
- **unless SysIntegrity is the only enabled API**, in which case an unselected request goes to SysIntegrity. This means a SysIntegrity-only deployment needs no client change either.

A new client opts into SysIntegrity by sending `HMS-Api: SysIntegrity`.

> A Huawei request whose selected API is not enabled is **rejected**, never allowed through, and never cross-checked by the other API.

### Enabling SysIntegrity (alongside the existing UserDetect)

```csharp
builder.AddAttestationVerification(options =>
{
    // Leave UserDetect on (the default) so current clients keep working:
    options.HMS.UserDetect.AppId = Environment.GetEnvironmentVariable("HMS_APP_ID")!;
    options.HMS.UserDetect.ClientId = Environment.GetEnvironmentVariable("HMS_CLIENT_ID")!;
    options.HMS.UserDetect.ClientSecret = Environment.GetEnvironmentVariable("HMS_CLIENT_SECRET")!;

    // Turn SysIntegrity on. Clients select it with the HMS-Api: SysIntegrity header.
    options.HMS.SysIntegrity.Enabled = true;

    // The HUAWEI CBG Root CA. See "Supplying the root certificate" below.
    options.HMS.SysIntegrity.RootCertificatePem = Environment.GetEnvironmentVariable("HMS_ROOT_CA")!;

    // At least one of each is required
    options.HMS.SysIntegrity.AllowedPackageNames.Add("com.yourcompany.yourapp");
    options.HMS.SysIntegrity.AllowedApkCertificateDigests.Add("yT5JtXRgeIgXssx1gQTsMA9GzM9ER4xAgCsCC69Fz3I=");

    // Optional. Defaults to 90 seconds; this window is the primary replay bound, so keep it short.
    options.HMS.SysIntegrity.MaxTokenAge = TimeSpan.FromSeconds(90);
});
```

To run **SysIntegrity only**, also set `options.HMS.UserDetect.Enabled = false;`. Clients can then omit the `HMS-Api` header, since SysIntegrity becomes the sole enabled API.

### Supplying the root certificate

The HUAWEI CBG Root CA is **not** shipped with this package. Download it from Huawei's SafetyDetect SysIntegrity development guide, then supply it through `RootCertificatePem`. The value may be PEM text or Base64 encoded DER — both are accepted, and line wrapping is ignored. It is required.

```csharp
options.HMS.SysIntegrity.RootCertificatePem = Environment.GetEnvironmentVariable("HMS_ROOT_CA")!;
```

On App Service or Functions this pairs with a [Key Vault reference](https://learn.microsoft.com/azure/app-service/app-service-key-vault-references) app setting, so the platform resolves the secret before the app starts:

```
HMS_ROOT_CA = @Microsoft.KeyVault(SecretUri=https://<your-vault>.vault.azure.net/secrets/huawei-cbg-root-ca)
```

No vault call happens at runtime, SysIntegrity never touches `Firebase.KeyVaultURI`, and locally you can just paste the PEM into `local.settings.json`.

If you store it in Key Vault, it goes in as a **secret**, never as a certificate. Key Vault certificates must carry a private key, and per Microsoft's own [certificates FAQ](https://learn.microsoft.com/azure/key-vault/certificates/faq#importing-azure-key-vault-certificates) a public-only PEM cannot be imported as one — importing it as a secret is the documented approach.

```bash
# The downloaded file is usually already PEM. Check the first line; if it reads
# -----BEGIN CERTIFICATE----- then upload it as it is:
az keyvault secret set --vault-name <your-vault> --name huawei-cbg-root-ca --file root.cer

# If it is binary DER instead, convert it first:
#   certutil -encode root.cer root.pem
#   openssl x509 -inform der -in root.cer -out root.pem
```

### What is verified

1. The certificate chain in the JWS `x5c` header is validated against the HUAWEI CBG Root CA, which is the only trusted root for this check.
2. The leaf certificate must have been issued for `sysintegrity.platform.hicloud.com`.
3. The signature is verified with the leaf certificate's public key. Only `RS256` and `PS256` are accepted.
4. The payload must then satisfy all of:
   - `basicIntegrity` is `true`
   - `apkPackageName` is in `AllowedPackageNames`
   - one of `apkCertificateDigestSha256` is in `AllowedApkCertificateDigests`
   - `timestampMs` is no older than `MaxTokenAge` and no further ahead than `ClockSkew`
   - the nonce validates, when `RequireNonce` is on (off by default)

### Nonce handling

`RequireNonce` is **off by default**. The payload's `timestampMs` is signed by Huawei, so a short `MaxTokenAge` already bounds how long a captured result stays usable — which is the freshness guarantee most callers want, with no nonce plumbing.

The nonce is worth turning on only when you can issue it **server-side**. The default `INonceValidator` merely echo-compares the client's nonce against the payload nonce; because the client picks that value, echoing it back proves nothing an attacker replaying the whole result couldn't also send, so it adds no replay protection on its own. Real protection comes from a server-issued, single-use nonce. Register your own implementation **before** calling `AddAttestationVerification`, then set `RequireNonce = true`:

```csharp
builder.Services.AddSingleton<INonceValidator, MyServerIssuedNonceValidator>();
```

## Development Mode

When running locally using Azure Functions Core Tools, the `UseFakeServices` option can be used to bypass token validation in the `Development` environment.

The registration logic will automatically swap out the real validation service for the `FakeAttestationService`. This allows front-end developers to test endpoints by simply passing any string (e.g., `Verification-Token: dev-test`) without needing to configure physical devices or real App Check projects.

## Azure Hosting Note (Certificate Loading)

If your Azure App Service app or Azure Functions App (on App Service-based hosting, such as Dedicated/App Service plan) encounters certificate private key import errors (`CryptographicException`) when loading the Firebase service account certificate from Key Vault, set this Environment variable in **app settings**:

- `WEBSITE_LOAD_USER_PROFILE=1`

This enables loading a full user profile and resolves certificate private key loading in that hosting mode.

> This applies to the Firebase service account certificate, which is downloaded with its private key. The HMS SysIntegrity root CA has no private key and is not affected.

## Migrating from 1.x

| 1.x | 2.0 |
| --- | --- |
| `options.HMS.AppId` | `options.HMS.UserDetect.AppId` |
| `options.HMS.ClientId` | `options.HMS.UserDetect.ClientId` |
| `options.HMS.ClientSecret` | `options.HMS.UserDetect.ClientSecret` |

`options.Firebase.KeyVaultURI` and `options.HMS.Enabled` are unchanged. `IUnifiedAttestationService.VerifyTokenAsync` gained trailing optional `nonce` and `hmsApi` parameters, which only affect callers that implement the interface themselves.
