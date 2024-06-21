using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SketchNotes
{
    public class TokenProvider : IAccessTokenProvider
    {
        private Func<string[], Task<string>> GetTokenDelegate;
        private string[] Scopes;

        public TokenProvider(Func<string[], Task<string>> getTokenDelegate, string[] scopes)
        {
            GetTokenDelegate = getTokenDelegate;
            Scopes = scopes;
        }

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = default,
            CancellationToken cancellationToken = default)
        {
            return GetTokenDelegate(Scopes);
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }
    }
}
