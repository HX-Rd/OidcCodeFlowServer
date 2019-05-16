using HXRd.CodeFlowServer;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OidcCodeCallbackServiceCollectionExtensions
    {
        public static IServiceCollection AddCodeCallback(this IServiceCollection services) => services.AddCodeCallback(_ => { });
        public static IServiceCollection AddCodeCallback(this IServiceCollection services, Action<IOidcCodeCallbackBuilder> action)
        {
            var builder = new OidcCodeCallbackBuilder
            {
                Services = services,
                Properties = new Dictionary<string, object>()
            };
            services.AddHttpClient<IAuthService, AuthService>();
            action(builder);
            return services;
        }
    }
}
