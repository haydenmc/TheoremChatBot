using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Theorem.Providers
{
    /// <summary>
    /// SlackProvider provides all Slack functionality (send/receive/etc)
    /// </summary>
    public class BotInfoProvider
    {        
        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private IConfigurationRoot _configuration { get; set; }

        /// <summary>
        /// List of names for all running middleware
        /// </summary>
        public IEnumerable<string> RunningMiddlewares { get; set; }
        
        /// <summary>
        /// Constructs a new instance of SlackProvider, requires configuration for things like API token
        /// </summary>
        /// <param name="configuration">Configuration object</param>
        public BotInfoProvider(IEnumerable<string> runningMiddlewares)
        {
            this.RunningMiddlewares = runningMiddlewares;
        }
    }
}