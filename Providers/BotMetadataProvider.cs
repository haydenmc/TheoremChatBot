using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Theorem.Models;

namespace Theorem.Providers
{
    /// <summary>
    /// BotInfoProvider provides meta-information about the Theorem environment the middleware
    /// runs in to the middleware itself
    /// </summary>
    public class BotMetadataProvider
    {        
        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private IConfigurationRoot _configuration { get; set; }

        /// <summary>
        /// List of names for all running middleware
        /// </summary>
        public IEnumerable<MiddlewareMetadata> RunningMiddlewares { get; set; }
        
        /// <summary>
        /// Constructs a new instance of SlackProvider, requires configuration for things like API token
        /// </summary>
        /// <param name="configuration">Configuration object</param>
        public BotMetadataProvider(IEnumerable<MiddlewareMetadata> runningMiddlewares)
        {
            this.RunningMiddlewares = runningMiddlewares;
        }
    }
}