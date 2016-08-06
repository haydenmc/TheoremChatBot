using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
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
            
            // Register dependencies
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterInstance(Configuration).As<IConfigurationRoot>();
            containerBuilder.RegisterType<ApplicationDbContext>().InstancePerDependency();
            containerBuilder.RegisterType<SlackProvider>().SingleInstance();
            // Middleware
            containerBuilder.RegisterType<SeenMiddleware>().As<IMiddleware>();
            containerBuilder.RegisterType<RhymingMiddleware>().As<IMiddleware>();
            containerBuilder.RegisterType<WhatSheSaidMiddleware>().As<IMiddleware>();
            containerBuilder.RegisterType<MiddlewarePipeline>().SingleInstance().AutoActivate();
            // Construct IoC container
            _iocContainer = containerBuilder.Build();
            
            using(var scope = _iocContainer.BeginLifetimeScope())
            {
                // Trigger database migrations
                using (var db = scope.Resolve<ApplicationDbContext>())
                {
                    db.Database.Migrate();
                }
                // Connect to Slack!
                await scope.Resolve<SlackProvider>().Connect();
            }
        }
    }
}
