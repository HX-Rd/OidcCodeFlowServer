using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace HXRd.CodeFlowServer
{
    public class ResourceError
    {
        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }
        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
