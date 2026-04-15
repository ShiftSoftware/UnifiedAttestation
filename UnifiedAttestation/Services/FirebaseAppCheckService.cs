using Azure.Security.KeyVault.Certificates;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Firebaseappcheck.v1beta;
using Google.Apis.Firebaseappcheck.v1beta.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShiftSoftware.UnifiedAttestation.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ShiftSoftware.UnifiedAttestation.Services
{
    public class FirebaseAppCheckService
    {
        // The public endpoint where Google publishes the App Check public keys
        private const string JwksEndpoint = "https://firebaseappcheck.googleapis.com/v1beta/jwks";
        private const string JWKSCacheKey = "APPCHECK_JWKS";

        private readonly string firebaseProject;
        private readonly ILogger<FirebaseAppCheckService> logger;
        private readonly FirebaseappcheckService appCheckService;
        private readonly FirebaseAppCheckOptions appCheckOptions;
        private readonly HttpClient httpClient;
        private readonly IMemoryCache memoryCache;

        public FirebaseAppCheckService(ILogger<FirebaseAppCheckService> logger,
            IOptions<FirebaseAppCheckOptions> options,
            CertificateClient certificateClient,
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache)
        {
            this.appCheckOptions = options.Value;
            this.logger = logger;
            this.httpClient = httpClientFactory.CreateClient(nameof(FirebaseAppCheckService));
            this.memoryCache = memoryCache;

            ArgumentNullException.ThrowIfNull(appCheckOptions);
            ArgumentException.ThrowIfNullOrWhiteSpace(appCheckOptions.ProjectNumber, nameof(appCheckOptions.ProjectNumber));
            ArgumentException.ThrowIfNullOrWhiteSpace(appCheckOptions.ServiceAccountEmail, nameof(appCheckOptions.ServiceAccountEmail));
            ArgumentException.ThrowIfNullOrWhiteSpace(appCheckOptions.ServiceAccountKeyVaultCertificate, nameof(appCheckOptions.ServiceAccountKeyVaultCertificate));


            // Downloads certificate WITH private key (requires both certificates/get and secrets/get permissions)
            X509Certificate2 certificate = certificateClient.DownloadCertificate(appCheckOptions.ServiceAccountKeyVaultCertificate);

            ServiceAccountCredential credential = new ServiceAccountCredential(
               new ServiceAccountCredential.Initializer(appCheckOptions.ServiceAccountEmail)
               {
                   Scopes = new[] { FirebaseappcheckService.Scope.Firebase }
               }.FromCertificate(certificate));

            // 2. Initialize the v1beta Firebase App Check Service
            appCheckService = new FirebaseappcheckService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
            });

            firebaseProject = $"projects/{appCheckOptions.ProjectNumber}";
        }

        /// <summary>
        /// Cryptographically verifies a Firebase App Check token locally using cached Google public keys. 
        /// This method is highly performant (no network latency) but does NOT prevent token replay attacks.
        /// </summary>
        /// <param name="appCheckToken">The raw Firebase App Check token (JWT) from the client.</param>
        /// <returns>True if the token's signature, issuer, audience, and lifetime are cryptographically valid; otherwise, false.</returns>
        public async Task<bool> VerifyTokenAsync(string appCheckToken)
        {
            if (string.IsNullOrEmpty(appCheckToken)) return false;

            var validationParameters = new TokenValidationParameters
            {
                // 2. Verify the token's cryptographic signature
                ValidateIssuerSigningKey = true,

                // 3. Verify the Issuer (must match your project number)
                ValidateIssuer = true,
                ValidIssuer = $"https://firebaseappcheck.googleapis.com/{appCheckOptions.ProjectNumber}",

                // 4. Verify the Audience (must match your project number)
                ValidateAudience = true,
                ValidAudience = firebaseProject,

                // 5. Verify the token hasn't expired
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                // 1. Fetch the cached public keys from Google
                var jsonWebKeySet = await GetOrRefreshJwksAsync();

                var signingKeys = jsonWebKeySet!.GetSigningKeys();

                validationParameters.IssuerSigningKeys = signingKeys;

                // This will throw an exception if the signature, issuer, audience, or lifetime is invalid
                var principal = tokenHandler.ValidateToken(appCheckToken, validationParameters, out var validatedToken);

                // Optional: If you want to restrict requests to a specific App ID (e.g., just your iOS app)
                // var appId = principal.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                // if (appId != "YOUR_APP_ID") return false;

                return true;
            }
            catch (SecurityTokenSignatureKeyNotFoundException )
            {
                try
                {
                    var jsonWebKeySet = await GetOrRefreshJwksAsync(true);

                    validationParameters.IssuerSigningKeys = jsonWebKeySet!.GetSigningKeys();

                    // Retry once with fresh keys
                    var principal = tokenHandler.ValidateToken(appCheckToken, validationParameters, out var validatedToken);

                    return true;
                }
                catch (Exception){}
                return false;
            }
            catch (SecurityTokenException ex)
            {
                // Token is invalid (expired, tampered with, wrong project, etc.)
                logger.LogError(ex, $"App Check token validation failed");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred during verification");
                return false;
            }
        }

        // <summary>
        /// Verifies a Firebase App Check token by making an authenticated network request to Google's API. 
        /// This strictly prevents Replay Attacks by consuming the token, meaning the same token cannot be used twice.
        /// </summary>
        /// <param name="appCheckToken">The raw Firebase App Check token from the client.</param>
        /// <returns>True if the token is mathematically valid AND has not been previously consumed; otherwise, false.</returns>
        public async Task<bool> VerifyTokenWithReplayProtectionAsync(string appCheckToken)
        {
            if(!(await VerifyTokenAsync(appCheckToken))) return false;

            try
            {
                // Prepare the request body
                var requestBody = new GoogleFirebaseAppcheckV1betaVerifyAppCheckTokenRequest
                {
                    AppCheckToken = appCheckToken,
                };

                // Create the network request targeting the tokens:verify endpoint
                var request = appCheckService.Projects.VerifyAppCheckToken(requestBody, firebaseProject);

                // Execute the request to Google
                var response = await request.ExecuteAsync();

                // 3. Evaluate Replay Protection
                // If it's the FIRST time Google has seen this token, AlreadyConsumed will be null or false.
                if (response.AlreadyConsumed == true)
                {
                    logger.LogInformation("REPLAY DETECTED: This token has already been consumed!");
                    return false;
                }

                // The token is valid, and Google has now marked it as consumed for future requests.
                return true;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // An HTTP 403 means the token itself is fundamentally invalid 
                // (e.g., bad signature, expired, or belongs to a different project).
                logger.LogError(ex, "Token verification failed: Invalid or expired token.");
                return false;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // An HTTP 400 means the attestation provider used for this token 
                // does not currently support Replay Protection via this endpoint.
                logger.LogError(ex, "Token verification failed: Unsupported attestation provider.");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A network or server error occurred");
                return false;
            }
        }

        // <summary>
        /// Gets the JSON Web Key Set (JWKS) from Memory cache if available, otherwise from Google's public endpoint, which contains the public keys used to verify Firebase App Check tokens.
        /// </summary>
        private async Task<JsonWebKeySet> GetOrRefreshJwksAsync(bool forceRefresh = false)
        {
            try
            {
                if (forceRefresh)
                    memoryCache.Remove(JWKSCacheKey);

                var jsonWebKeySet = await memoryCache.GetOrCreateAsync(JWKSCacheKey, async entry =>
                {
                    var jwksJson = await httpClient.GetStringAsync(JwksEndpoint);

                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(5.7);

                    return new JsonWebKeySet(jwksJson);
                });

                return jsonWebKeySet!;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch JWKS from Google and Memory Cache.");
                throw;
            }
        }
    }
}