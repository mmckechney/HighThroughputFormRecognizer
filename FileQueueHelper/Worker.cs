using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.CommandLine;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using AzureUtilities;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Cryptography;
using Azure.Storage.Sas;
using Azure.Storage.Shared;
using Azure.Storage;
namespace FileQueueUtility
{
    public partial class Worker : IHostedService
    {
        private readonly IHostApplicationLifetime applicationLifetime;
        private readonly ILogger<Worker> logger;
        private readonly IConfiguration config;
        private string[] startArgs;
        private CommandBuilder cmdBuilder;
        CancellationToken cancellationToken;
        private StorageHelper storageHelper;
        private ServiceBusHelper sbHelper;
        private RuntimeValues runtimeVals;
        private string storageConnString;

        public Worker(ILogger<Worker> logger, CommandBuilder cmdBuilder, StartArgs args,  StorageHelper storageHelper, ServiceBusHelper sbHelper, IConfiguration config, IHostApplicationLifetime applicationLifetime)
        {
            this.logger = logger;
            this.config = config;
            this.applicationLifetime = applicationLifetime;
            this.startArgs = args.Args;
            this.cmdBuilder = cmdBuilder;
            this.storageHelper = storageHelper;
            this.sbHelper = sbHelper;
  
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            string[] args = startArgs;
            var rootCommand = cmdBuilder.ConfigureRootCommand(this);
            string[] exitKeywords = new string[] { "exit", "quit", "q" };
            int val = await rootCommand.InvokeAsync(args);
            logger.LogInformation("Complete!");
            applicationLifetime.StopApplication();
        }

        private List<Task> metaDataTasks = new List<Task>();
        internal async Task QueueFormsForProcessing(string storageName, string containerName, string servicebusNamespace, string queueName, string keyVaultName, bool force)
        {
            try
            {
                int filecounter =  0;
                var runtimeVars = new RuntimeValues(storageName, containerName, servicebusNamespace, queueName);
                logger.LogInformation($"Searching through storage container '{runtimeVars.ContainerName}' to queue messages in Service Bus queue '{runtimeVars.QueueName}'");
                var containerClient = storageHelper.CreateBlobContainerClient(runtimeVars.ContainerName, runtimeVars.StorageName);
                var sbSender = sbHelper.CreateServiceBusSender(runtimeVars.ServicebusNamespace, runtimeVars.QueueName);
                storageConnString = await storageHelper.GetStorageConnectionString(keyVaultName,storageName);

                var blobList = containerClient.GetBlobsAsync(BlobTraits.Metadata);
                await foreach (var blob in blobList)
                {
                    if(this.cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    if (!force && (blob.Metadata.ContainsKey("IsQueued") || blob.Metadata.ContainsKey("Processed")))
                    {
                        logger.LogInformation($"Skipping {blob.Name}. Already Queued or Processed");
                        continue;
                    }

                    logger.LogDebug($"Found file  {blob.Name}");
                    var sbMessage = new FileQueueMessage() { FileName = blob.Name, ContainerName = runtimeVars.ContainerName }.AsMessage();
                    await sbSender.SendMessageAsync(sbMessage);
                    logger.LogInformation($"Queued file {blob.Name} for processing");
                    filecounter++;
                    metaDataTasks.Add(UpdateBlobMetaData(blob.Name, containerClient, "IsQueued", DateTime.UtcNow.ToString()));

                    if (metaDataTasks.Count > 200)
                    {
                        logger.LogInformation("Purging collection of completed tasks....");
                        var waiting = Task.WhenAll(metaDataTasks);
                        await waiting;
                        metaDataTasks.Clear();
                    }
                }

                if(metaDataTasks.Count > 0)
                {
                    logger.LogInformation("Waiting for metadata updates to complete....");
                    var waiting =  Task.WhenAll(metaDataTasks);
                    await waiting;
                }
                logger.LogInformation($"Queued {filecounter} files.");
            }
            catch (Exception exe)
            {
                logger.LogError(exe.Message);
            }
        }

        public async Task UpdateBlobMetaData(string blobName, BlobContainerClient containerClient, string key, string value, int retry = 0)
        {
            try
            {

                BlobClientOptions opts = new BlobClientOptions();

                logger.LogDebug($"Updating metadata ({key}={value}) on blob {blobName} ");
                var meta = new Dictionary<string, string>();
                meta.Add(key, value);
                var bc = containerClient.GetBlobClient(blobName);
                await bc.SetMetadataAsync(meta);
                logger.LogInformation($"Updated metadata ({key}={value}) on blob {blobName} ");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error updating Blob Metadata for file '{blobName}'. {ex.Message}");
                if (retry < 3)
                {
                    retry = retry + 1;
                    logger.LogError($"Retrying to set Blob Metadata for file '{blobName}'. Attempt #{retry}");
                    await UpdateBlobMetaData(blobName, containerClient, key, value, retry);
                }
                else
                {
                    logger.LogError($"Error updating Blob Metadata for file '{blobName}'. Retries exceeded. {ex.Message}");
                }
            }
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public class StartArgs
        {
            public string[] Args { get; set; }
            public StartArgs(string[] args)
            {
                this.Args = args;
            }
        }
    }
}
