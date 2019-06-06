using Appy.Play.Infrastructure.Utils;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Management.AppService.Fluent.FunctionApp.Definition;
using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.SignalR;
using Microsoft.Azure.Management.SignalR.Models;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Appy.Play.Infrastructure.Management
{
    public static class AppyAzureManagementExtensions
    {
        public const string SignalRNamespace = "Microsoft.SignalRService";
        public const string SignalRResourceTypeName = "SignalR";
        public const string SignalRResourceType = SignalRNamespace + "/" + SignalRResourceTypeName;

        public static IAzure AuthenticateWithAzure(ILogger logger, AppyAzureCredentials appyCredentials)
        {
            logger.Info($"Authenticating with azure credentials");
            var sp = new ServicePrincipalLoginInformation
            {
                ClientId = appyCredentials.ClientId,
                ClientSecret = appyCredentials.ClientSecret
            };
            var credentials = new AzureCredentials(sp, appyCredentials.TenantId, AzureEnvironment.AzureGlobalCloud);
            var authenticatedAzure = Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials);

            var azure = authenticatedAzure.WithSubscription(appyCredentials.SubscriptionId);
            logger.Info($"Authenticated with azure credentials");

            return azure;
        }

        public static async Task CreateResourceGroupIfNotExists(this IAzure azure, ILogger logger, AppyResourceGroupParameters parameters)
        {
            logger.Info($"Creating resource group with name {parameters.ResourceGroupName}");
            if ((await azure.ResourceGroups.ContainAsync(parameters.ResourceGroupName)))
            {
                logger.Info($"Resource group already exists with name {parameters.ResourceGroupName}");
                return;
            }

            await azure.ResourceGroups
                .Define(parameters.ResourceGroupName)
                .WithRegion(parameters.ResourceRegion)
                .CreateAsync();

            logger.Info($"Created resource group with name {parameters.ResourceGroupName}");
        }

        public static async Task ListResourceGroups(this IAzure azure, ILogger logger)
        {
            logger.Info("Listing all resource groups:");
            var resourceGroups = await azure.ResourceGroups.ListAsync();
            resourceGroups.ForEach(rGroup =>
                logger.Info($"\t Resource group: {rGroup.Name}"));
        }

        public static async Task DeleteResourceGroupIfExists(this IAzure azure, ILogger logger, AppyResourceGroupParameters parameters)
        {
            logger.Info($"Deleting resource group with name {parameters.ResourceGroupName}");
            if (!(await azure.ResourceGroups.ContainAsync(parameters.ResourceGroupName)))
            {
                logger.Info($"Resource group not exists with name {parameters.ResourceGroupName}");
                return;
            }

            await azure.ResourceGroups.DeleteByNameAsync(parameters.ResourceGroupName);
            logger.Info($"Deleted resource group with name {parameters.ResourceGroupName}");
        }

        public static async Task CreateStorageAccountIfNotExists(this IAzure azure, ILogger logger, AppyStorageParameters parameters)
        {
            logger.Info($"Creating storage account with name {parameters.AccountName} in group {parameters.ResourceGroupName}");
            var storageResource = await azure.StorageAccounts.GetByResourceGroupAsync(parameters.ResourceGroupName, parameters.AccountName);
            if (!ReferenceEquals(storageResource, null))
            {
                logger.Info($"Storage Account with name {parameters.AccountName} already exists in group {parameters.ResourceGroupName}");
                return;
            }

            await azure.StorageAccounts
                .Define(parameters.AccountName)
                .WithRegion(parameters.AccountRegion)
                .WithExistingResourceGroup(parameters.ResourceGroupName)
                .WithGeneralPurposeAccountKind()
                .CreateAsync();

            logger.Info($"Created storage account with name {parameters.AccountName} in group {parameters.ResourceGroupName}");
        }

        public static async Task DeleteStorageAccountIfExists(this IAzure azure, ILogger logger, AppyStorageParameters parameters)
        {
            logger.Info($"Deleting storage account with name {parameters.AccountName} in group {parameters.ResourceGroupName}");
            var storageResource = await azure.StorageAccounts.GetByResourceGroupAsync(parameters.ResourceGroupName, parameters.AccountName);
            if (ReferenceEquals(storageResource, null))
            {
                logger.Info($"Storage Account with name {parameters.AccountName} not exists in group {parameters.ResourceGroupName}");
                return;
            }

            await azure.ResourceGroups.DeleteByNameAsync(parameters.ResourceGroupName);
            logger.Info($"Deleted resource group with name {parameters.ResourceGroupName}");
        }

        public static async Task CreateCosmosResourcesIfNotExists(this IAzure azure, ILogger logger, AppyCosmosDbParameters parameters)
        {
            var cosmosAccount = await azure.CreateCosmosAccountIfNotExists(logger, parameters);
            var cosmosKeys = cosmosAccount.ListKeys();
            var masterKey = cosmosKeys.PrimaryMasterKey;
            var endPoint = cosmosAccount.DocumentEndpoint;
            var documentClient = CreateCosmosDocumentClient(masterKey, endPoint);

            await documentClient.CreateCosmosDatabaseIfNotExists(logger, parameters.DatabaseId);

            foreach (var collectionId in parameters.CollectionIds)
                await documentClient.CreateCosmosDbAndCollectionIfNotExists(logger, parameters.DatabaseId, collectionId);
        }

        public static async Task<ICosmosDBAccount> CreateCosmosAccountIfNotExists(this IAzure azure, ILogger logger, AppyCosmosDbParameters parameters)
        {
            logger.Info($"Creating Cosmos Account with name {parameters.AccountName} in group {parameters.ResourceGroupName}");
            var cosmosResource = await azure.CosmosDBAccounts.GetByResourceGroupAsync(parameters.ResourceGroupName, parameters.AccountName);
            if (!ReferenceEquals(cosmosResource, null))
            {
                logger.Info($"Cosmos Account with name {parameters.AccountName} already exists in group {parameters.ResourceGroupName}");
                return cosmosResource;
            }

            var cosmosAccount = await azure.CosmosDBAccounts
                .Define(parameters.AccountName)
                .WithRegion(parameters.AccountRegion)
                .WithExistingResourceGroup(parameters.ResourceGroupName)
                .WithDataModelSql()
                .WithEventualConsistency()
                .WithWriteReplication(parameters.WriteReplicationRegion)
                .WithReadReplication(parameters.ReadReplicationRegion)
                .CreateAsync();

            logger.Info($"Created Cosmos Account with name {parameters.AccountName} in group {parameters.ResourceGroupName}");

            return cosmosAccount;
        }

        public static IDocumentClient CreateCosmosDocumentClient(string masterKey, string endPoint) =>
            new DocumentClient(new Uri(endPoint), masterKey, ConnectionPolicy.Default, ConsistencyLevel.Session);

        public static async Task CreateCosmosDatabaseIfNotExists(this IDocumentClient documentClient, ILogger logger, string databaseId)
        {
            logger.Info($"Creating Cosmos Db if not exists with id {databaseId}");
            var database = new Database { Id = databaseId };
            await documentClient.CreateDatabaseIfNotExistsAsync(database);
            logger.Info($"Created Cosmos Db with id {databaseId}");
        }

        public static async Task CreateCosmosDbAndCollectionIfNotExists(this IDocumentClient documentClient, ILogger logger, string databaseId, string collectionId)
        {
            logger.Info($"Creating Cosmos Db Collection if not exists for database {databaseId} with id {collectionId}");
            var collection = new DocumentCollection { Id = collectionId };
            var requestOptions = new RequestOptions { OfferThroughput = 400 };
            await documentClient.CreateDocumentCollectionIfNotExistsAsync($"dbs/{databaseId}", collection, requestOptions);
            logger.Info($"Created Cosmos Db Collection for database {databaseId} with id {collectionId}");
        }

        public static async Task DeleteCosmosDbResourcesIfExists(this IAzure azure, ILogger logger, AppyCosmosDbParameters parameters)
        {
            logger.Info($"Deleting Cosmos Account with name {parameters.AccountName} in group {parameters.ResourceGroupName}");
            var cosmosResource = await azure.CosmosDBAccounts.GetByResourceGroupAsync(parameters.ResourceGroupName, parameters.AccountName);
            if (ReferenceEquals(cosmosResource, null))
            {
                logger.Info($"Cosmos Account with name {parameters.AccountName} not exists in group {parameters.ResourceGroupName}");
                return;
            }

            await azure.CosmosDBAccounts.DeleteByResourceGroupAsync(parameters.ResourceGroupName, parameters.AccountName);
            logger.Info($"Deleted Cosmos Account with name {parameters.AccountName} in group {parameters.ResourceGroupName}");
        }

        public static ISignalRManagementClient CreateSignalRManagementClient(AppyAzureCredentials appyCredentials)
        {
            var sp = new ServicePrincipalLoginInformation
            {
                ClientId = appyCredentials.ClientId,
                ClientSecret = appyCredentials.ClientSecret,
            };
            var azureCredentials = new AzureCredentials(sp, appyCredentials.TenantId, AzureEnvironment.AzureGlobalCloud);

            return new SignalRManagementClient(azureCredentials)
            {
                SubscriptionId = appyCredentials.SubscriptionId
            };
        }

        public static async Task CreateSignalRServiceIfNotExists(this ISignalRManagementClient managementClient, ILogger logger, AppySignalRParameters parameters)
        {
            logger.Info($"Creating SignalR Service with name {parameters.ServiceName} in group {parameters.ResourceGroupName}");
            var locationName = parameters.ServiceRegion.Name;
            var nameAvailabilityParams = new NameAvailabilityParameters(SignalRResourceType, parameters.ServiceName);
            var availability = await managementClient.SignalR.CheckNameAvailabilityAsync(locationName, nameAvailabilityParams);
            if (availability.NameAvailable == false)
            {
                logger.Info($"SignalR Service with name {parameters.ServiceName} already exists in group {parameters.ResourceGroupName}");
                // TODO (compare parameters and update with CreateOrUpdateAsync)
                return;
            }

            var tags = new Dictionary<string, string>
            {
                { "description", parameters.Description },
            };
            var createParameters = new SignalRCreateParameters
            {
                Location = locationName,
                Sku = parameters.ResourceSku,
                Tags = tags
            };
            await managementClient.SignalR.CreateOrUpdateAsync(parameters.ResourceGroupName, parameters.ServiceName, createParameters);

            logger.Info($"Created SignalR Service with name {parameters.ServiceName} in group {parameters.ResourceGroupName}");
        }

        public static async Task DeleteSignalRServiceResources(this ISignalRManagementClient managementClient, ILogger logger, AppySignalRParameters parameters)
        {
            logger.Info($"Deleting SignalR Service with name {parameters.ServiceName} in group {parameters.ResourceGroupName}");
            var locationName = Region.EuropeNorth.Name;
            var nameAvailabilityParams = new NameAvailabilityParameters(SignalRResourceType, parameters.ServiceName);
            var availability = await managementClient.SignalR.CheckNameAvailabilityAsync(locationName, nameAvailabilityParams);
            if (availability.NameAvailable == true)
            {
                logger.Info($"SignalR Service with name {parameters.ServiceName} not exists in group {parameters.ResourceGroupName}");
                return;
            }

            await managementClient.SignalR.DeleteAsync(parameters.ResourceGroupName, parameters.ServiceName);

            logger.Info($"Deleted SignalR Service with name {parameters.ServiceName} in group {parameters.ResourceGroupName}");
        }

        public static async Task CreateFunctionAppResources(this IAzure azure, ILogger logger, AppyFunctionAppParameters parameters)
        {
            logger.Info($"Creating Azure Function App with name {parameters.AppName} in group {parameters.ResourceGroupName}");
            var functionApps = await azure.AppServices.FunctionApps.ListByResourceGroupAsync(parameters.ResourceGroupName);
            var storageAccount = await azure.StorageAccounts.GetByResourceGroupAsync(parameters.ResourceGroupName, parameters.StorageAccountName);
            var functionResource = functionApps.SingleOrDefault(functionApp => functionApp.Name == parameters.AppName);
            if (!ReferenceEquals(functionResource, null))
            {
                logger.Info($"Azure Function App with name {parameters.AppName} already exists in group {parameters.ResourceGroupName}");

                logger.Info($"Looking changes in Function App {parameters.AppName} parameters in group {parameters.ResourceGroupName}");
                var updateProcess = functionResource
                    .Update()
                    .WithExistingStorageAccount(storageAccount);

                var appSettings = await functionResource.GetAppSettingsAsync();
                var hasChanges = false;

                // update worker runtime (dotnet, node, etc)
                if (hasChangedFunctionWorkerRunTime(parameters, appSettings))
                {
                    updateProcess.WithAppSetting("FUNCTIONS_WORKER_RUNTIME", parameters.WorkerRunTime);
                    hasChanges = true;
                }

                if (!hasChanges)
                {
                    logger.Info($"There are not changes in Function App {parameters.AppName} parameters in group {parameters.ResourceGroupName}");
                    return;
                }

                logger.Info($"Applying changes in Function App {parameters.AppName} parameters in group {parameters.ResourceGroupName}");
                await updateProcess
                    .ApplyAsync();
                logger.Info($"Applied changes in Function App {parameters.AppName} parameters in group {parameters.ResourceGroupName}");

                return;
            }

            var createProcess = azure.AppServices.FunctionApps
                .Define(parameters.AppName)
                .WithRegion(parameters.AppRegion)
                .WithExistingResourceGroup(parameters.ResourceGroupName)
                .WithNewConsumptionPlan()
                .WithExistingStorageAccount(storageAccount)
                .WithRuntimeVersion(parameters.ExtensionsRunTime);

            if (!string.IsNullOrEmpty(parameters.WorkerRunTime))
                createProcess = (IWithCreate)createProcess.WithAppSetting("FUNCTIONS_WORKER_RUNTIME", parameters.WorkerRunTime);
            await createProcess
                .CreateAsync();

            logger.Info($"Created Azure Function App with name {parameters.AppName} in group {parameters.ResourceGroupName}");
        }

        static bool hasChangedFunctionWorkerRunTime(AppyFunctionAppParameters parameters, IReadOnlyDictionary<string, Microsoft.Azure.Management.AppService.Fluent.IAppSetting> appSettings) =>
         (!appSettings.TryGetValue("FUNCTIONS_WORKER_RUNTIME", out var workerRunTimeSetting) && !string.IsNullOrEmpty(parameters.WorkerRunTime))
          || workerRunTimeSetting != null && (workerRunTimeSetting.Value != parameters.WorkerRunTime);        

        public static async Task ListFunctionApps(this IAzure azure, ILogger logger, string rgName)
        {
            logger.Info("Listing all function apps:");
            var functionApps = await azure.AppServices.FunctionApps.ListByResourceGroupAsync(rgName);
            functionApps.ForEach(functionApp =>
                logger.Info($"\t FunctionApp: {functionApp.Name}"));
        }

        public static async Task DeleteFunctionAppResources(this IAzure azure, ILogger logger, AppyFunctionAppParameters parameters)
        {
            logger.Info($"Deleting Azure Function App with name {parameters.AppName} in group {parameters.ResourceGroupName}");
            var functionResource = await azure.AppServices.FunctionApps.GetByResourceGroupAsync(parameters.ResourceGroupName, parameters.AppName);
            if (ReferenceEquals(functionResource, null))
            {
                logger.Info($"Azure Function App with name {parameters.AppName} not exists in group {parameters.ResourceGroupName}");
                return;
            }

            await azure.AppServices.FunctionApps
                .DeleteByResourceGroupAsync(parameters.ResourceGroupName, parameters.AppName);
        }
    }
}