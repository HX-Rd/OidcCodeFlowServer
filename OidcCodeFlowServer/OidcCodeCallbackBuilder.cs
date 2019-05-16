using HXRd.CodeFlowServer;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    internal class OidcCodeCallbackBuilder : IOidcCodeCallbackBuilder
    {
        public IServiceCollection Services { get; set; }
        public IDictionary<string, object> Properties { get; set; }
    }
}
