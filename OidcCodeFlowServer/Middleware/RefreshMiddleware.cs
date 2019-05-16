using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace HXRd.CodeFlowServer
{
    public class RefreshMiddleware
    {
        private RequestDelegate _next;
        private IAuthService _authService;
        private AuthSettings _settings;
        private ILogger<RefreshMiddleware> _logger;
        private string _username;
        private string _password;
        private bool _useAuthorization;

        public RefreshMiddleware(RequestDelegate next, IAuthService authService, IOptions<AuthSettings> settings, ILogger<RefreshMiddleware> logger)
        {
            _next = next;
            _authService = authService;
            _settings = settings.Value;
            _logger = logger;
            _username = _settings.Username;
            _password = _settings.Password;
            _useAuthorization = _settings.UseAuthorizationOnRefreshEndpoint;
        }
        public async Task Invoke(HttpContext context)
        {
            RefreshRequest refreshRequest;
            using (_logger.BeginScope("Refresh Middleware Request"))
            {
                if (ShouldValidateAuthorizationHeader())
                {
                    var authHeader = context.Request.Headers["Authorization"];
                    try
                    {
                        ValidateAuthorizationHeader(authHeader);
                    }
                    catch (AuthenticationException)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }
                }
                using (var stream = new MemoryStream())
                {
                    await context.Request.Body.CopyToAsync(stream);
                    stream.Position = 0;
                    var reader = new StreamReader(stream);
                    string json = await reader.ReadToEndAsync();
                    refreshRequest = JsonConvert.DeserializeObject<RefreshRequest>(json);
                    stream.Position = 0;
                    context.Request.Body = stream;
                }
            }
            await _next(context);
            AuthResponse authResponse;
            using (_logger.BeginScope("Refresh Middleware Response"))
            {
                context.Response.Headers.Add("Content-Type", "application/json");
                try
                {
                    authResponse = await _authService.RefreshTokens(refreshRequest.RefreshToken, refreshRequest.Scope);
                }
                catch (AuthenticationException authException)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    _logger.LogWarning($"Authentication Error: {authException.Data["error"] as string} Description: {authException.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    _logger.LogError(ex, "Something went wrong");
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(authResponse));
            }
        }

        private void ValidateAuthorizationHeader(StringValues value)
        {
            using (_logger.BeginScope("Validating Authorization Header"))
            {
                _logger.LogTrace($"Validating header Authorization {value}");
                if (value.Count != 1)
                {
                    _logger.LogWarning("Request should include one Authorization header");
                    throw new AuthenticationException();
                }
                var authHeaderValue = value.ToString();
                if (!authHeaderValue.StartsWith("Basic "))
                {
                    _logger.LogWarning("Authorization header must be Basic");
                    throw new AuthenticationException();
                }
                var split = authHeaderValue.Split(' ');
                if (split.Length != 2)
                {
                    _logger.LogWarning("Authorization Basic is not formated correctly");
                    throw new AuthenticationException();
                }
                _logger.LogDebug("Header passed format validations");
                string user, pass;
                try
                {
                    var b64 = split[1];
                    _logger.LogTrace($"Base64 header value {b64}");
                    var bytes = Convert.FromBase64String(b64);
                    var userPass = Encoding.UTF8.GetString(bytes);
                    _logger.LogTrace($"userPass {userPass}");
                    var userPassSplit = userPass.Split(':');
                    user = userPassSplit[0];
                    _logger.LogTrace($"user {user}");
                    pass = userPassSplit[1];
                    _logger.LogTrace($"pass {pass}");
                }
                catch (Exception)
                {
                    _logger.LogWarning("Username and password can not be extracted from Authorization Basic value");
                    throw new AuthenticationException();
                }
                if (user != _settings.Username || pass != _settings.Password)
                {
                    _logger.LogWarning("User or password incorrect in Authorization header");
                    throw new AuthenticationException();
                }
                _logger.LogInformation("Authentication header validated");
            }
        }

        private bool ShouldValidateAuthorizationHeader()
        {
            if (!_useAuthorization)
                return false;
            if (string.IsNullOrEmpty(_username) && string.IsNullOrEmpty(_password))
                return false;
            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
                return true;
            _logger.LogError("You have to provide both username and password to enable Authorization header validation on Refresh token route");
            throw new AuthenticationException();
        }
    }
}
