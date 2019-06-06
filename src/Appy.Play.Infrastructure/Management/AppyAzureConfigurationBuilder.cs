using System.Collections.Generic;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.SignalR.Models;

namespace Appy.Play.Infrastructure.Management
{
    public class AppyAzureCredentials
    {
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string SubscriptionId { get; set; }

        public static AppyAzureCredentials Create(string tenantId, string clientId, string clientSecret, string subscriptionId) =>
            new AppyAzureCredentials
            {
                TenantId = tenantId,
                ClientId = clientId,
                ClientSecret = clientSecret,
                SubscriptionId = subscriptionId
            };
    }

    public class AppyResourceGroupParameters
    {
        public string ResourceGroupName { get; set; }
        public Region ResourceRegion { get; set; }
    }

    public class AppyStorageParameters
    {
        public string ResourceGroupName { get; set; }
        public string AccountName { get; set; }        
        public Region AccountRegion { get; set; }
    }

    public class AppyCosmosDbParameters
    {
        public AppyCosmosDbParameters()
        {
            CollectionIds = new List<string>();
        }

        public string AccountName { get; set; }
        public string ResourceGroupName { get; set; }
        public string DatabaseId { get; set; }
        public List<string> CollectionIds { get; set; }
        public Region AccountRegion { get; set; }
        public Region ReadReplicationRegion { get; set; }
        public Region WriteReplicationRegion { get; set; }
        public void AddCollectionId(string collectionId) => CollectionIds.Add(collectionId);
    }

    public class AppyFunctionAppParameters
    {
        public string ResourceGroupName { get; set; }
        public string AppName { get; set; }
        public Region AppRegion { get; set; }

        public string StorageAccountName { get; set; }
        public string ExtensionsRunTime { get; set; }
        public string WorkerRunTime { get; set; }
    }

    public class AppySignalRParameters
    {
        public string ResourceGroupName { get; set; }
        public Region ServiceRegion { get; set; }
        public ResourceSku ResourceSku { get; set; }
        public string ServiceName { get; set; }
        public string Description { get; set; }
    }

    public class AppyPlayInfrastructureParameters
    {
        public AppyPlayInfrastructureParameters(string name)
        {
            Name = name;
            FunctionsApps = new List<AppyFunctionAppParameters>();
        }

        public string Name { get; }
        public AppyResourceGroupParameters ResourceGroupParameters { get; set; }
        public AppyStorageParameters StorageAccountParameters { get; set; }
        public AppyCosmosDbParameters CosmosDbParameters { get; set; }
        public List<AppyFunctionAppParameters> FunctionsApps { get; set; }
        public AppySignalRParameters SignalRParameters { get; set; }
    }

    public sealed class AppyPlayAzureConfigurationBuilder
    {
        readonly AppyPlayInfrastructureParameters _infraParameters;
        AppyPlayAzureConfigurationBuilder(string name) =>
            _infraParameters = new AppyPlayInfrastructureParameters(name);

        public static AppyPlayAzureConfigurationBuilder Define(string name) => 
            new AppyPlayAzureConfigurationBuilder(name);

        public AppyPlayAzureConfigurationBuilder WithResourceGroup(string resourceGroupName, Region resourceRegion)
        {
            var parameters = new AppyResourceGroupParameters
            {
                ResourceGroupName = resourceGroupName,
                ResourceRegion = resourceRegion
            };
            _infraParameters.ResourceGroupParameters = parameters;

            return this;
        }

        public AppyPlayAzureConfigurationBuilder WithStorageAccount(string accountName, Region accountRegion)
        {
            var rgName = _infraParameters.ResourceGroupParameters.ResourceGroupName;
            var parameters = new AppyStorageParameters
            {
                ResourceGroupName = rgName,
                AccountName = accountName,
                AccountRegion = accountRegion
            };
            _infraParameters.StorageAccountParameters = parameters;

            return this;
        }

        public AppyPlayAzureConfigurationBuilder WithCosmosSqlAccount(string accountName, 
            Region accountRegion, Region writeReplicationRegion, Region readReplicationRegion)
        {
            var rgName = _infraParameters.ResourceGroupParameters.ResourceGroupName;
            _infraParameters.CosmosDbParameters = new AppyCosmosDbParameters
            {
                ResourceGroupName = rgName,
                AccountName = accountName,
                AccountRegion = accountRegion,
                WriteReplicationRegion = writeReplicationRegion,
                ReadReplicationRegion = readReplicationRegion
            };            

            return this;
        }

        public AppyPlayAzureConfigurationBuilder WithCosmosDbCollection(string databaseId, string collectionId)
        {
            var parameters = _infraParameters.CosmosDbParameters;
            parameters.DatabaseId = databaseId;
            parameters.AddCollectionId(collectionId);

            return this;
        }

        public AppyPlayAzureConfigurationBuilder WithFunctionApp(string functionAppName, Region functionRegion, string extensionRunTime, string workerRunTime = null)
        {
            var rgName = _infraParameters.ResourceGroupParameters.ResourceGroupName;
            var storageAccountName = _infraParameters.StorageAccountParameters.AccountName;
            var parameters = new AppyFunctionAppParameters
            {
                AppName = functionAppName,
                ResourceGroupName = rgName,
                StorageAccountName = storageAccountName,                
                AppRegion = functionRegion,
                ExtensionsRunTime = extensionRunTime,
                WorkerRunTime = workerRunTime
            };
            _infraParameters.FunctionsApps.Add(parameters);          

            return this;
        }
        
        public AppyPlayAzureConfigurationBuilder WithSignalRService(string serviceName, Region serviceRegion, ResourceSku sku = null)
        {
            var rgName = _infraParameters.ResourceGroupParameters.ResourceGroupName;
            var skuResource = sku ?? new ResourceSku
            {
                Name = "Free_F1",
                Tier = "Free",
                Size = "F1",
            };

            var parameters = new AppySignalRParameters
            {
                ServiceName = serviceName,
                ResourceGroupName = rgName,
                ResourceSku = skuResource,
                Description = serviceName,
                ServiceRegion = serviceRegion
            };
            _infraParameters.SignalRParameters = parameters;

            return this;
        }

        public AppyPlayInfrastructureParameters Build() => _infraParameters;
    }
}