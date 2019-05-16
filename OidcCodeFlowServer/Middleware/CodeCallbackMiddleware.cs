using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace HXRd.CodeFlowServer
{
    public class OidcCodeCallbackMiddleware
    {
        private RequestDelegate _next;
        private IAuthService _authService;
        private ILogger<OidcCodeCallbackMiddleware> _logger;

        public OidcCodeCallbackMiddleware(RequestDelegate next, IAuthService authService, ILogger<OidcCodeCallbackMiddleware> logger)
        {
            _next = next;
            _authService = authService;
            _logger = logger;
        }
        public async Task Invoke(HttpContext context)
        {
            await _next(context);
            using (_logger.BeginScope("Oidc CodeCallback Middleware Response"))
            {
                _logger.LogInformation("Start run of Oidc CodeCallback middleware");
                var code = context.Request.Query["code"];
                _logger.LogTrace($"code {code}");
                var redirectUri = context.Request.Query["redirect_uri"];
                _logger.LogTrace($"redirectUri {redirectUri}");
                AuthResponse authResponse;
                context.Response.Headers.Add("Content-Type", "application/json");
                try
                {
                    _logger.LogTrace("Calling _authService to get tokens");
                    authResponse = await _authService.GetTokens(code, redirectUri);
                    _logger.LogTrace("Success calling authService");
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
                _logger.LogInformation("Oidc CodeCallback middleware ran successfully");
            }
        }
    }
}
