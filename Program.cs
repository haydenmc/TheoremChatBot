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
using Theorem.Utility;

namespace Theorem
{
    public class Program
    {
        public IConfigurationRoot Configuration { get; set; }

        private ILogger<Program> _logger { get; set; }
        
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
            _logger = loggerFactory.CreateLogger<Program>();
        }
        
        public async Task Start()
        {
            _logger.LogInformation("Starting Theorem...");

            // Load configuration
            _logger.LogInformation("Loading configuration data...");
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
            _logger.LogInformation("Registering database context with file {file}...", dbFileName);
            containerBuilder
                .Register(c => new TheoremDbContext(dbFileName))
                .InstancePerDependency();

            // Middleware
            registerMiddleware(containerBuilder);

            // Chat service providers
            registerChatServiceConnections(containerBuilder);

            // Register MiddlewarePipeline
            registerMiddlewarePipeline(containerBuilder);

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
                var chatServices = scope.Resolve<IEnumerable<IChatServiceConnection>>();
                await Task.WhenAll(
                    chatServices.Select(c => 
                        TaskUtilities.ExpontentialRetryAsync(
                            c.StartAsync,
                            (e, r) => onChatServiceConnectionInterruption(c, e, r)))
                );
            }
        }

        private void onChatServiceConnectionInterruption(
            IChatServiceConnection connection,
            Exception exception,
            (uint retryNumber, uint nextRetrySeconds) retries)
        {
            _logger.LogError("{c} connection threw exception. " + 
                "retry {n} in {s} seconds. Exception: {e}",
                connection.Name,
                retries.retryNumber,
                retries.nextRetrySeconds,
                exception.Message);
        }

        private void registerMiddleware(ContainerBuilder containerBuilder)
        {
            _logger.LogInformation("Registering middleware...");

            // Find all the middleware in the current assembly
            var middlewareTypes = Assembly.GetEntryAssembly().GetTypes()
                .Where(t => t.IsAssignableTo<IMiddleware>() 
                    && !t.Equals(typeof(IMiddleware)))
                .ToDictionary(t => 
                    t.Name.EndsWith("Middleware") ? 
                        t.Name.Substring(0, t.Name.Length - 10) : 
                        t.Name);
            
            _logger.LogInformation(
                "Found {count} middleware types in assembly: {types}.",
                middlewareTypes.Keys.Count,
                String.Join(", ", middlewareTypes.Keys));

            // Pull configuration data and instantiate middleware
            _logger.LogInformation("Reading middleware configuration...");
            var middlewareConfigRoot = Configuration.GetSection("Middleware");
            var middlewareConfigs = middlewareConfigRoot.GetChildren();
            foreach (var middlewareType in middlewareTypes)
            {
                var typeConfig = middlewareConfigs
                    .SingleOrDefault(c => c.Key == middlewareType.Key);
                if (typeConfig == null)
                {
                    _logger.LogInformation(
                        "Configuration not found for middleware {name}. Skipping...",
                        middlewareType.Key);
                    continue;
                }
                if (!typeConfig.GetValue<bool>("Enabled", false))
                {
                    _logger.LogInformation(
                        "{name} configuration 'Enabled' property not true. Skipping...",
                        middlewareType.Key);
                    continue;
                }
                _logger.LogDebug("Registering middleware type {type}...",
                    middlewareType.Value.ToString());
                containerBuilder
                    .RegisterType(middlewareType.Value)
                    .WithParameter(
                        new TypedParameter(typeof(ConfigurationSection), typeConfig))
                    .As<IMiddleware>()
                    .SingleInstance();
            }
        }

        private void registerChatServiceConnections(ContainerBuilder containerBuilder)
        {
            _logger.LogInformation("Registering chat service connections...");

            // Find all the chat service connection types in the current assembly
            var connectionTypes = Assembly.GetEntryAssembly().GetTypes()
                .Where(t => t.IsAssignableTo<IChatServiceConnection>() 
                    && !t.Equals(typeof(IChatServiceConnection)))
                .ToDictionary(
                    k => k.Name.EndsWith("ChatServiceConnection") ? 
                        k.Name.Substring(0, k.Name.Length - 21) :
                        k.Name);
            _logger.LogInformation(
                "Found {count} chat service connection types in assembly: '{types}'.",
                connectionTypes.Keys.Count,
                String.Join(", ", connectionTypes.Keys));

            // Pull configuration data and instantiate connections
            _logger.LogInformation("Reading chat service configuration...");
            var chatServicesConfig = Configuration.GetSection("ChatServiceConnections");
            foreach (var chatServiceConfig in chatServicesConfig.GetChildren())
            {
                var chatServiceName = chatServiceConfig.Key;
                _logger.LogDebug("Reading configuration for chat service {name}...",
                    chatServiceName);
                var chatServiceService = chatServiceConfig.GetValue<string>("Service", "");
                if (chatServiceService.Length <= 0)
                {
                    _logger.LogError(
                        "Service configuration value not found for chat service {name}!" + 
                        " Skipping...", chatServiceName);
                    break;
                }
                if (!connectionTypes.ContainsKey(chatServiceService))
                {
                    _logger.LogError("Could not find chat connection type for service '{service}'.",
                        chatServiceService);
                    break;
                }
                Type connectionType = connectionTypes[chatServiceService];
                _logger.LogDebug("Registering chat service connection type {type}...",
                    connectionType.Name);
                containerBuilder
                    .RegisterType(connectionType)
                    .WithParameter(
                        new TypedParameter(typeof(ConfigurationSection), chatServiceConfig))
                    .As<IChatServiceConnection>()
                    .SingleInstance();
            }
        }

        private void registerMiddlewarePipeline(ContainerBuilder containerBuilder)
        {
            _logger.LogInformation("Registering middleware pipeline...");

            // Find all the middleware in the current assembly
            var middlewareTypes = Assembly.GetEntryAssembly().GetTypes()
                .Where(t => t.IsAssignableTo<IMiddleware>() 
                    && !t.Equals(typeof(IMiddleware)))
                .ToDictionary(t => 
                    t.Name.EndsWith("Middleware") ? 
                        t.Name.Substring(0, t.Name.Length - 10) : 
                        t.Name);

            // First, we need to parse the configuration to build a map of
            // Middlewares to ChatServiceConnections
            var chatServiceConnectionMiddlewares = new Dictionary<string, Type[]>();
            var chatServicesConfig = Configuration.GetSection("ChatServiceConnections");
            foreach (var chatServiceConfig in chatServicesConfig.GetChildren())
            {
                var chatServiceMiddlewareTypeList = new List<Type>();
                var chatServiceName = chatServiceConfig.Key;
                var chatServiceMiddlewareConfig = 
                    chatServiceConfig
                        .GetSection("Middleware")
                        .GetChildren()
                        .Select(c => c.Value);
                foreach (var middlewareName in chatServiceMiddlewareConfig)
                {
                    if (middlewareTypes.ContainsKey(middlewareName))
                    {
                        chatServiceMiddlewareTypeList.Add(middlewareTypes[middlewareName]);
                    }
                }
                chatServiceConnectionMiddlewares[chatServiceName] = 
                    chatServiceMiddlewareTypeList.ToArray();

                _logger.LogInformation(
                    "Chat service '{name}' registered with middlewares '{middlewares}'.",
                    chatServiceName,
                    String.Join(", ", chatServiceMiddlewareTypeList.Select(t => t.ToString())));
            }

            containerBuilder
                .RegisterType<MiddlewarePipeline>()
                .WithParameter(
                    new TypedParameter(typeof(IDictionary<string, Type[]>),
                        chatServiceConnectionMiddlewares))
                .SingleInstance()
                .AutoActivate();
        }

        private static int getExecutionOrderNumber(IConfigurationSection c)
        {
            int i;
            return int.TryParse(c.GetChildren().Where(c2 => c2.Key.Equals("ExecutionOrder"))
                .SingleOrDefault()?.Value, out i) ? i : 0;
        }

        private static bool getEnabledValue(IConfigurationSection c)
        {
            // return true by default -> middleware has to be explicitly disabled to not be loaded
            bool b;
            return bool.TryParse(c.GetChildren().Where(c2 => c2.Key.Equals("Enabled"))
                .SingleOrDefault()?.Value, out b) ? b : true;
        }
    }
}
