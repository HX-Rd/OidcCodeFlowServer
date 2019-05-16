using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HXRd.CodeFlowServer
{
    public interface IAuthService
    {
        Task<AuthResponse> GetTokens(string code, string redirectUrl);
        Task<AuthResponse> RefreshTokens(string refreshToken, string scopes = null);
    }
}
