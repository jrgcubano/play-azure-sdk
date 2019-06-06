using Appy.Play.Infrastructure.Utils;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
using Appy.Play.Infrastructure.Management;
using static Appy.Play.Infrastructure.Management.AppyAzureManagementExtensions;

namespace Appy.Play.Infrastructure
{
    /// <summary>
    /// Infrastructure as code
    /// - Create
    /// - Update
    /// - Delete
    /// </summary>

    internal class Program
    {
        public const string TenantId = "Azure TenantId";
        public const string ClientId = "Azure Client Id";
        public const string ClientSecret = "Azure ClientSecret";
        public const string SubscriptionId = "Azure SubscriptionId";    
    
        static async Task Main(string[] args)
        {
            var logger = new ConsoleLogger();

            var appyCredentials = AppyAzureCredentials.Create(
                TenantId, ClientId, ClientSecret, SubscriptionId);
            
            // Create AppyPlay Azure configuration
            var createAppyPlayConfiguration = AppyPlayAzureConfigurationBuilder
                .Define("AppyPlayResources")
                .WithResourceGroup("AppyPlay", Region.EuropeNorth)
                .WithStorageAccount("appyplaystorage", Region.EuropeNorth)
                //.WithCosmosSqlAccount("appyplaysql", Region.EuropeNorth, Region.EuropeWest, Region.EuropeWest)
                //.WithCosmosDbCollection("appyplaydb", "appyplaycollection")
                .WithSignalRService("appyplaysignalr", Region.EuropeNorth)
                .WithFunctionApp("appy-play-durable", Region.EuropeNorth, "~1")
                .WithFunctionApp("appy-play-client", Region.EuropeNorth, "~1")
                .Build();

            // Update AppyPlay Configuration: We update the function apps to dotnet and version 2
            var updateAppyPlayConfiguration = AppyPlayAzureConfigurationBuilder
                .Define("AppyPlayResources")
                .WithResourceGroup("AppyPlay", Region.EuropeNorth)
                .WithStorageAccount("appyplaystorage", Region.EuropeNorth)
                //.WithCosmosSqlAccount("appyplaysql", Region.EuropeNorth, Region.EuropeWest, Region.EuropeWest)
                //.WithCosmosDbCollection("appyplaydb", "appyplaycollection")
                .WithSignalRService("appyplaysignalr", Region.EuropeNorth)
                .WithFunctionApp("appy-play-durable", Region.EuropeNorth, "~2")           
                .WithFunctionApp("appy-play-client", Region.EuropeNorth, "~2", "dotnet")  
                .Build();

            await CreateInfrastructure(logger, appyCredentials, createAppyPlayConfiguration);
            // await CreateInfrastructure(logger, appyCredentials, updateAppyPlayConfiguration);
            // await DeleteInfrastructure(logger, appyCredentials, appyPlayConfiguration);

            Console.ReadLine();
        }

        /// <summary>
        /// Appy Play Environment
        /// </summary>
        static async Task CreateInfrastructure(
            ILogger logger, AppyAzureCredentials azureCredentials, AppyPlayInfrastructureParameters parameters)
        {
            logger.Info($"Creating AppyPlay azure infrastructure");

            var azure = AuthenticateWithAzure(logger, azureCredentials);

            await azure.CreateResourceGroupIfNotExists(logger, parameters.ResourceGroupParameters);
            await azure.ListResourceGroups(logger);

            if (parameters.StorageAccountParameters != null)
                await azure.CreateStorageAccountIfNotExists(logger, parameters.StorageAccountParameters);

            if (parameters.CosmosDbParameters != null)
                await azure.CreateCosmosResourcesIfNotExists(logger, parameters.CosmosDbParameters);

            if (parameters.SignalRParameters != null)
            {
                var signalRManagementClient = CreateSignalRManagementClient(azureCredentials);
                await signalRManagementClient.CreateSignalRServiceIfNotExists(logger, parameters.SignalRParameters);
            }

            if (parameters.FunctionsApps.Any())
            {
                foreach(var functionAppParameters in parameters.FunctionsApps)
                    await azure.CreateFunctionAppResources(logger, functionAppParameters);

                await azure.ListFunctionApps(logger, parameters.ResourceGroupParameters.ResourceGroupName);
            }

            logger.Info($"Created or Updated AppyPlay azure infrastructure");
        }

        static async Task DeleteInfrastructure(
            ILogger logger, AppyAzureCredentials credentials, AppyPlayInfrastructureParameters parameters)
        {
            logger.Info("Deleting AppyPlay azure infrastructure");

            var azure = AuthenticateWithAzure(logger, credentials);

            if (parameters.CosmosDbParameters != null)
                await azure.DeleteCosmosDbResourcesIfExists(logger, parameters.CosmosDbParameters);

            if (parameters.SignalRParameters != null)
            {
                var signalRManagementClient = CreateSignalRManagementClient(credentials);
                await signalRManagementClient.DeleteSignalRServiceResources(logger, parameters.SignalRParameters);
            }

            if (parameters.FunctionsApps.Any())
            {
                parameters.FunctionsApps.ForEach(async functionParameters =>
                    await azure.DeleteFunctionAppResources(logger, functionParameters));

                await azure.ListFunctionApps(logger, parameters.ResourceGroupParameters.ResourceGroupName);
            }

            if (parameters.StorageAccountParameters != null)
                await azure.DeleteStorageAccountIfExists(logger, parameters.StorageAccountParameters);

            await azure.DeleteResourceGroupIfExists(logger, parameters.ResourceGroupParameters);

            logger.Info("Deleted AppyPlay azure infrastructure");
        }
    }
}