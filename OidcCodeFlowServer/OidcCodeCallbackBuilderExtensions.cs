using HXRd.CodeFlowServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OidcCodeCallbackBuilderExtensions
    {
        public static IApplicationBuilder UseOidcCodeCallback(this IApplicationBuilder builder, PathString path) => builder.UseOidcCodeCallback(path, _ => { });
        public static IApplicationBuilder UseOidcCodeCallback(this IApplicationBuilder builder, PathString path, Action<IApplicationBuilder> configure)
        {
            builder.Map(path, b =>
            {
                b.UseMiddleware<OidcCodeCallbackMiddleware>();
                configure(b);
            });
            return builder;
        }
        public static IApplicationBuilder UseRefresh(this IApplicationBuilder builder, PathString path) => builder.UseRefresh(path, _ => { });
        public static IApplicationBuilder UseRefresh(this IApplicationBuilder builder, PathString path, Action<IApplicationBuilder> configure)
        {
            builder.Map(path, b =>
            {
                b.UseMiddleware<RefreshMiddleware>();
                configure(b);
            });
            return builder;
        }
    }
}
