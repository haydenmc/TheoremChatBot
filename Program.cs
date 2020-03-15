using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Middleware;
using Theorem.Models;
using Theorem.Providers;

namespace Theorem
{
    public class Program
    {
        public IConfigurationRoot Configuration { get; set; }

        private ILogger<Program> Logger { get; set; }
        
        private IContainer _iocContainer { get; set; }
        
        public async static Task Main(string[] args)
        {
            await new Program().Start();
        }

        public Program()
        {
            // Set up logging
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddConsole();
            });
            Logger = loggerFactory.CreateLogger<Program>();
        }
        
        public async Task Start()
        {
            Logger.LogInformation("Starting Theorem...");

            // Load configuration
            Logger.LogInformation("Loading configuration data...");
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.default.json")
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // TODO: Allow configuring logger from configuration
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddConsole();
            });

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

            // Logging
            containerBuilder
                .RegisterInstance(loggerFactory)
                .As<ILoggerFactory>()
                .SingleInstance();
            containerBuilder
                .RegisterGeneric(typeof(Logger<>))
                .As(typeof(ILogger<>))
                .SingleInstance();
            
            // SQLite database
            var dbFileName = Configuration.GetValue<string>("Database", "Theorem.db");
            Logger.LogInformation("Registering database context with file {file}...", dbFileName);
            containerBuilder
                .Register(c => new TheoremDbContext(dbFileName))
                .InstancePerDependency();

            // Chat service providers
            RegisterChatServiceConnections(containerBuilder);

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

        private void RegisterChatServiceConnections(ContainerBuilder containerBuilder)
        {
            Logger.LogInformation("Registering chat service connections...");

            // Find all the chat service connection types in the current assembly
            var connectionTypes = Assembly.GetEntryAssembly().GetTypes()
                .Where(t => t.IsAssignableTo<IChatServiceConnection>() 
                    && !t.Equals(typeof(IChatServiceConnection)))
                .ToDictionary(
                    k => k.Name.EndsWith("ChatServiceConnection") ? 
                        k.Name.Substring(0, k.Name.Length - 21) :
                        k.Name);
            Logger.LogInformation(
                "Found {count} chat service connection types in assembly: {types}.",
                connectionTypes.Keys.Count,
                String.Join(", ", connectionTypes.Keys));

            // Pull configuration data and instantiate connections
            Logger.LogInformation("Reading chat service configuration...");
            var chatServicesConfig = Configuration.GetSection("ChatServiceConnections");
            foreach (var chatServiceConfig in chatServicesConfig.GetChildren())
            {
                var chatServiceName = chatServiceConfig.Key;
                Logger.LogDebug("Reading configuration for chat service {name}...",
                    chatServiceName);
                var chatServiceService = chatServiceConfig.GetValue<string>("Service", "");
                if (chatServiceService.Length <= 0)
                {
                    Logger.LogError(
                        "Service configuration value not found for chat service {name}!" + 
                        " Skipping...", chatServiceName);
                    break;
                }
                if (!connectionTypes.ContainsKey(chatServiceService))
                {
                    Logger.LogError("Could not find chat connection type for service {service}",
                        chatServiceService);
                    break;
                }
                Type connectionType = connectionTypes[chatServiceService];
                Logger.LogDebug("Registering chat service connection type {type}...",
                    connectionType.Name);
                containerBuilder
                    .RegisterType(connectionType)
                    .WithParameter(
                        new TypedParameter(typeof(ConfigurationSection), chatServiceConfig))
                    .As<IChatServiceConnection>()
                    .SingleInstance();
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
