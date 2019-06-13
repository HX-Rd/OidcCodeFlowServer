using HXRd.CodeFlowServer.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace HXRd.CodeFlowServer
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private AuthSettings _auth;
        private Uri _accessTokenJWKS;
        private Uri _idTokenJWKS;
        private ILogger<AuthService> _logger;

        public AuthService(HttpClient httpClient, IOptions<AuthSettings> options, ILogger<AuthService> logger)
        {
            _httpClient = httpClient;
            _auth = options.Value;
            _logger = logger;
            if (_auth.ValidateIdToken)
            {
                if (string.IsNullOrEmpty(_auth.IdTokenJwksEndpoint))
                {
                    if (!string.IsNullOrEmpty(_auth.JwksEndpoint))
                        _idTokenJWKS = new Uri(_auth.JwksEndpoint);
                }
                else _idTokenJWKS = new Uri(_auth.IdTokenJwksEndpoint);
            }
            if (_auth.ValidateAccessToken)
            {
                if (string.IsNullOrEmpty(_auth.AccessTokenJwksEndpoint))
                {
                    if (!string.IsNullOrEmpty(_auth.JwksEndpoint))
                        _accessTokenJWKS = new Uri(_auth.JwksEndpoint);
                }
                else _accessTokenJWKS = new Uri(_auth.AccessTokenJwksEndpoint);
            }
        }

        public async Task<AuthResponse> GetTokens(string code, string redirectUrl)
        {
            using (_logger.BeginScope("Auth Service GetTokens"))
            {
                _logger.LogTrace($"TokenEndpoint Url: {_auth.TokenEndpoint}");
                var values = new Dictionary<string, string>
                {
                    { "client_id", _auth.ClientId },
                    { "client_secret", _auth.ClientSecret },
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", redirectUrl }
                };
                _logger.LogDebug($"Client ID: {_auth.ClientId}");
                _logger.LogDebug($"Client Secret: {_auth.ClientSecret}");
                _logger.LogDebug("GrantType: authorization_code");
                _logger.LogDebug($"Code: {code}");
                _logger.LogDebug($"Redirect Uri: {redirectUrl}");
                var content = new FormUrlEncodedContent(values);
                var response = await _httpClient.PostAsync(_auth.TokenEndpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();
                _logger.LogTrace($"Response: {response}");
                _logger.LogTrace($"ResponseString {responseString}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug($"Response status code: {responseString}");
                    var resourceError = JsonConvert.DeserializeObject<ResourceError>(responseString);
                    throw new AuthenticationException(resourceError.ErrorDescription)
                    {
                        Data = { { "error", resourceError.Error } }
                    };
                }
                _logger.LogDebug("Auth Service GetTokens ran successfully");
                var authResponse = JsonConvert.DeserializeObject<AuthResponse>(responseString);

                if (_accessTokenJWKS != null || _idTokenJWKS != null)
                    await ValidateTokens(authResponse);

                return authResponse;
            }
        }

        public async Task<AuthResponse> RefreshTokens(string refreshToken, string scopes = null)
        {
            using (_logger.BeginScope("Auth Service RefreshTokens"))
            {
                var url = $"{_auth.TokenEndpoint}";
                _logger.LogTrace($"Token Endpoint: {_auth.TokenEndpoint}");
                var values = new Dictionary<string, string>
                {
                    { "client_id", _auth.ClientId },
                    { "client_secret", _auth.ClientSecret },
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshToken }
                };
                _logger.LogDebug($"Client ID: {_auth.ClientId}");
                _logger.LogDebug($"Client Secret: {_auth.ClientSecret}");
                _logger.LogDebug("GrantType: refresh_token");
                _logger.LogDebug($"Refresh Token: {refreshToken}");
                if (scopes != null)
                {
                    values.Add("scope", scopes);
                    _logger.LogDebug($"Scopes: {scopes}");
                }
                var content = new FormUrlEncodedContent(values);
                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();
                _logger.LogTrace($"Response: {responseString}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug($"Response status code: {response.StatusCode}");
                    var resourceError = JsonConvert.DeserializeObject<ResourceError>(responseString);
                    throw new AuthenticationException(resourceError.ErrorDescription)
                    {
                        Data = { { "error", resourceError.Error } }
                    };
                }
                _logger.LogDebug("Auth Service RefreshTokens ran successfully");
                var authResponse = JsonConvert.DeserializeObject<AuthResponse>(responseString);

                if (_accessTokenJWKS != null)
                {
                    var accessTokenKeySet = await GetWebKeySet(_accessTokenJWKS);
                    try
                    {
                        ValidateAccessToken(authResponse.AccessToken, accessTokenKeySet);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error when validating Access Token");
                        throw;
                    }
                }
                return authResponse;
            }
        }

        private async Task ValidateTokens(AuthResponse authResponse)
        {
            JsonWebKeySet accessTokenKeySet = null;
            JsonWebKeySet idTokenKeySet = null;
            if ((_accessTokenJWKS != null && _idTokenJWKS != null) && (_accessTokenJWKS.AbsoluteUri == _idTokenJWKS.AbsoluteUri))
            {
                accessTokenKeySet = await GetWebKeySet(_accessTokenJWKS);
                idTokenKeySet = accessTokenKeySet;
            }
            else
            {
                if (_accessTokenJWKS != null)
                    accessTokenKeySet = await GetWebKeySet(_accessTokenJWKS);
                if (_idTokenJWKS != null)
                    idTokenKeySet = await GetWebKeySet(_idTokenJWKS);
            }
            if (_accessTokenJWKS != null)
            {
                try
                {
                    ValidateAccessToken(authResponse.AccessToken, accessTokenKeySet);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error when validating Access Token");
                    throw;
                }
            }
            if (_idTokenJWKS != null)
            {
                try
                {
                    ValidateIdToken(authResponse.IdToken, idTokenKeySet);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error when validating Id Token");
                    throw;
                }
            }
        }

        private void ValidateAccessToken(string accessTokenJwt, JsonWebKeySet jsonWebKeySet)
        {
            using (_logger.BeginScope("Validating AccessToken"))
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(accessTokenJwt);
                var kid = jwtToken.Header.Kid;
                _logger.LogDebug($"Kid: {kid}");

                var webKey = jsonWebKeySet.Keys.Single(k => k.Kid == kid);

                var certString = webKey.X5c[0];
                _logger.LogDebug($"Cert64: {certString}");
                var certBytes = Base64Url.Decode(certString);
                var cert = new X509Certificate2(certBytes);
                var key = new X509SecurityKey(cert);

                var parameters = new TokenValidationParameters
                {
                    ValidIssuer = _auth.Issuer,
                    ValidAudience = _auth.Audience,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    AuthenticationType = "bearer",
                    NameClaimType = "sub",
                    IssuerSigningKey = key,
                    RequireSignedTokens = true
                };

                var user = handler.ValidateToken(accessTokenJwt, parameters, out var _);
            }
        }

        private void ValidateIdToken(string idTokenJwt, JsonWebKeySet jsonWebKeySet)
        {
            using (_logger.BeginScope("Validating IdToken"))
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(idTokenJwt);
                var kid = jwtToken.Header.Kid;
                _logger.LogDebug($"Kid: {kid}");

                var webKey = jsonWebKeySet.Keys.Single(k => k.Kid == kid);

                var e = Base64Url.Decode(webKey.E);
                var n = Base64Url.Decode(webKey.N);
                var key = new RsaSecurityKey(new RSAParameters { Exponent = e, Modulus = n })
                {
                    KeyId = kid
                };

                var parameters = new TokenValidationParameters
                {
                    ValidIssuer = _auth.Issuer,
                    ValidAudience = _auth.ClientId,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    NameClaimType = "sub",
                    IssuerSigningKey = key,
                    RequireSignedTokens = true
                };

                var user = handler.ValidateToken(idTokenJwt, parameters, out var _);
            }
        }
        private async Task<JsonWebKeySet> GetWebKeySet(Uri jwksUri)
        {
            var jwksResponse = await _httpClient.GetAsync(jwksUri);
            var jwksRaw = await jwksResponse.Content.ReadAsStringAsync();
            _logger.LogTrace($"Jwks Response: {jwksRaw}");
            return JsonConvert.DeserializeObject<JsonWebKeySet>(jwksRaw);
        }
    }
}
