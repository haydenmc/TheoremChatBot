using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Theorem.Models;
using Theorem.Models.Events;
using Theorem.Providers;
using System.Linq;

namespace Theorem.Middleware
{
    public abstract class Middleware
    {
        /// <summary>
        /// Reference to the Slack provider.
        /// </summary>
        protected SlackProvider _slackProvider { get; set;}

        /// <summary>
        /// Returns a new db context to use for interacting with the database.
        /// </summary>
        protected Func<ApplicationDbContext> _dbContext { get; set; }

        /// <summary>
        /// Reference to configuration values
        /// </summary>
        protected IConfigurationRoot _configuration { get; set; }

        public Middleware(SlackProvider slackProvider, Func<ApplicationDbContext> dbContext, IConfigurationRoot configuration)
        {
            _slackProvider = slackProvider;
            _dbContext = dbContext;
            _configuration = configuration;
        }

        public virtual MiddlewareResult ProcessMessage(MessageEventModel message)
        { 
            return MiddlewareResult.Continue;
        }

        protected string GetSavedValue(string key)
        {
            var area = GetType().Name;
            using (var db = _dbContext())
            {
                var datapair = db.SavedDataPairs.SingleOrDefault(d => d.Area == area && d.Key == key);
                return datapair?.Value;
            }
        }

        protected void SetSavedValue(string key, string value)
        {
            var area = GetType().Name;
            using (var db = _dbContext())
            {
                var datapair = db.SavedDataPairs.SingleOrDefault(d => d.Area == area && d.Key == key);
                if (datapair == null)
                {
                    datapair = new SavedDataPair();
                    db.SavedDataPairs.Add(datapair);
                }
                datapair.Area = area;
                datapair.Key = key;
                datapair.Value = value;
                db.SaveChanges();
            }
        }
    }
}