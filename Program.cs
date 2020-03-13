using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Theorem.ChatServices;
using Theorem.Middleware;
using Theorem.Models;
using Theorem.Providers;

namespace Theorem
{
    public class Program
    {
        public IConfigurationRoot Configuration { get; set; }
        
        private IContainer _iocContainer { get; set; }
        
        public static void Main(string[] args)
        {
            new Program().Start().Wait();
        }
        
        public async Task Start()
        {
            // Load configuration
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.default.json")
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // determine execution order
            // get all middleware listed in configuration
            var rootChildren = Configuration.GetChildren();
            var middlewareSection = rootChildren.Where(c => c.Key.Equals("Middleware")).SingleOrDefault();
            var middlewareList = middlewareSection?.GetChildren()
                .Select(c => new Tuple<string, int, bool>(c.Key, GetExecutionOrderNumber(c), GetEnabledValue(c)));

            // select middleware with specified order
            var orderSpecifiedMiddleware = middlewareList.Where(m => m.Item2 != 0);
            // select middleware with unspecified order
            var orderUnspecifiedMiddleware = middlewareList.Where(m => m.Item2 == 0);

            // order according to execution order number and append unspecified
            orderSpecifiedMiddleware = orderSpecifiedMiddleware.OrderBy(m => m.Item2);
            var orderedMiddleware = new List<Tuple<string, bool>>();
            orderedMiddleware.AddRange(orderSpecifiedMiddleware.Select(m => new Tuple<string, bool>(m.Item1, m.Item3)));
            orderedMiddleware.AddRange(orderUnspecifiedMiddleware.Select(m => new Tuple<string, bool>(m.Item1, m.Item3)));

            // Register dependencies
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterInstance(Configuration).As<IConfigurationRoot>();
            containerBuilder.RegisterType<TheoremDbContext>().InstancePerDependency();
            //containerBuilder.RegisterType<SlackProvider>().SingleInstance();
            containerBuilder
                .RegisterType<MattermostChatServiceConnection>()
                .SingleInstance()
                .As<IChatServiceConnection>();

            // Middleware
            // Find all the middleware in the current assembly
            var middlewareTypes = Assembly.GetEntryAssembly().GetTypes()
                .Where(t => t.IsAssignableTo<IMiddleware>() 
                    && !t.Equals(typeof(IMiddleware)))
                .Select(t => new Tuple<string, Type>(
                    t.Name.EndsWith("Middleware") ? t.Name.Substring(0, t.Name.Length - 10) : t.Name, t)); 

            // Find all middleware that has not been mentioned in the config file & order them last
            var undeclaredMiddleware = middlewareTypes.Where(t => !orderedMiddleware.Select(m => m.Item1).Contains(t.Item1))
                .Select(t => t.Item1);
            orderedMiddleware.AddRange(undeclaredMiddleware.Select(m => new Tuple<string, bool>(m, true)));

            // save list of middleware names in order for later
            var middlewareNamesList = orderedMiddleware.Select(mw => mw.Item1);

            // reverse middleware order to ensure proper loading of middleware by the middleware pipeline
            orderedMiddleware.Reverse();

            // Register all middleware according to order policy
            foreach(var middlewareName in orderedMiddleware)
            {
                if(!middlewareName.Item2) continue;
                if(!middlewareTypes.Any(t => t.Item1.Equals(middlewareName.Item1))) continue;

                // containerBuilder.RegisterType(middlewareTypes.First(t => t.Item1.Equals(middlewareName.Item1)).Item2)
                //     .As<Middleware.Middleware>();
            }

            // Register BotInfoProvider instance
            var botInfoProvider = new BotInfoProvider(middlewareNamesList);
            containerBuilder.RegisterInstance(botInfoProvider);

            // Register MiddlewarePipeline
            containerBuilder.RegisterType<MiddlewarePipeline>().SingleInstance().AutoActivate();
            // Construct IoC container
            _iocContainer = containerBuilder.Build();

            using (var scope = _iocContainer.BeginLifetimeScope())
            {
                // Trigger database migrations
                using (var db = scope.Resolve<TheoremDbContext>())
                {
                    db.Database.Migrate();
                }
                
                // Connect to chat providers!
                var chatProviders = scope.Resolve<IEnumerable<IChatServiceConnection>>();
                foreach (var chatProvider in chatProviders)
                {
                    await chatProvider.Connect();
                }
            }
        }

        private static int GetExecutionOrderNumber(IConfigurationSection c)
        {
            int i;
            return int.TryParse(c.GetChildren().Where(c2 => c2.Key.Equals("ExecutionOrder"))
                .SingleOrDefault()?.Value, out i) ? i : 0;
        }

        private static bool GetEnabledValue(IConfigurationSection c)
        {
            // return true by default -> middleware has to be explicitly disabled to not be loaded
            bool b;
            return bool.TryParse(c.GetChildren().Where(c2 => c2.Key.Equals("Enabled"))
                .SingleOrDefault()?.Value, out b) ? b : true;
        }
    }
}
