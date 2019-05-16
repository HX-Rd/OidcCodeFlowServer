using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace HXRd.CodeFlowServer
{
    public interface IOidcCodeCallbackBuilder
    {
        IServiceCollection Services { get; }
        IDictionary<string, object> Properties {get; }
    }
}
