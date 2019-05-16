using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HXRd.CodeFlowServer
{
    public class AuthSettings
    {
        public string RelativeCallbackEndpoint { get; set; }
        public string RelativeRefreshEndpoint { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string TokenEndpoint { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string JwksEndpoint { get; set; }
        public string AccessTokenJwksEndpoint { get; set; }
        public string IdTokenJwksEndpoint { get; set; }
        public string AllowedCORSHeaders { get; set; }
        public string AllowedCORSOrigins { get; set; }
        public bool ValidateAccessToken { get; set; }
        public bool ValidateIdToken { get; set; }
        public bool UseAuthorizationOnRefreshEndpoint { get; set; }
    }
}
