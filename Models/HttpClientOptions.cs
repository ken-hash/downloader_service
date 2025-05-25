using System;
using System.Collections.Generic;

namespace downloader_service.Models
{
    public class HttpClientOptions
    {
        public Uri BaseAddress { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
        public IDictionary<string, string> DefaultRequestHeaders { get; } = new Dictionary<string, string>();
    }
}
