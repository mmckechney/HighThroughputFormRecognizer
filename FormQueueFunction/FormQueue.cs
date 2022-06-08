using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using AzureUtilities;
using Microsoft.AspNetCore.Mvc.WebApiCompatShim;
using System.Security.Cryptography.X509Certificates;
using Azure.Storage.Blobs.Models;
using System.Threading;
using System.Collections.Generic;
using Azure.Storage.Blobs;
using System.Web.Http;
using Azure.Messaging.ServiceBus;

namespace FormQueueFunction
{
    public static class FormQueue
    {
        private static ILogger logger;
        [FunctionName("FormQueue")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            logger = log;
            int fileCounter = 0;
            logger.LogInformation("Request received to queue form files");
            var cancelSource = new CancellationTokenSource();
            bool force = false;
            bool.TryParse(req.Query["force"], out force);

            bool useRawFilesAsSource = false;
            bool.TryParse(req.Query["useRawFiles"], out useRawFilesAsSource);

            DateTime queuedDate = DateTime.MinValue;
            DateTime.TryParse(req.Query["queuedDate"], out queuedDate);

             List<Task> metaDataTasks = new List<Task>();

            logger.LogInformation($"Processing sessings: Force re-queue: '{force.ToString()}',  Re-queue forms previously queued before: '{queuedDate}");

            try
            {
                BlobContainerClient containerClient;
                ServiceBusSender sbSender;
                if (useRawFilesAsSource)
                {
                    containerClient = Settings.RawFilesContainerClient;
                    sbSender = Settings.ServiceBusRawSenderClient;
                    
                }
                else
                {
                    containerClient = Settings.SourceContainerClient;
                    sbSender = Settings.ServiceBusSenderClient;
                }
                logger.LogInformation($"Using storage container '{containerClient.Name}' as files source.");
                logger.LogInformation($"Sending messages to Service Bus Queue '{sbSender.EntityPath}'");


                var blobList = containerClient.GetBlobsAsync(BlobTraits.Metadata);
                int counter = 0;
                await foreach (var blob in blobList)
                {
                    if (cancelSource.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!force && blob.Metadata.ContainsKey("Processed"))
                    {
                        logger.LogInformation($"Skipping {blob.Name}. Already marked as Processed and 'force' flag not set");
                        continue;
                    }

                    string queueDateStr;
                    if (blob.Metadata.TryGetValue("IsQueued", out queueDateStr) && queuedDate != DateTime.MinValue)
                    {
                        DateTime fileQueueDate;
                        if (DateTime.TryParse(queueDateStr, out fileQueueDate))
                        {
                            if (fileQueueDate > queuedDate)
                            {
                                logger.LogInformation($"Skipping {blob.Name}. Already marked as queued and metadata date of {fileQueueDate} is greater than target requeue date of {queuedDate}");
                                continue;
                            }
                        }
                    }

                    if (counter > 9) counter = 0;
                    logger.LogDebug($"Found file  {blob.Name}");
                    ServiceBusMessage sbMessage;
                    if (useRawFilesAsSource)
                    {
                        sbMessage = new ServiceBusMessage(blob.Name);
                    }
                    else
                    {
                        sbMessage = new FileQueueMessage() { FileName = blob.Name, ContainerName = containerClient.Name, RecognizerIndex = counter }.AsMessage();
                    }
                    
                    await sbSender.SendMessageAsync(sbMessage);
                    logger.LogInformation($"Queued file {blob.Name} for processing from storage container '{containerClient.Name}' ");
                    fileCounter++;
                    counter++;

                    metaDataTasks.Add(UpdateBlobMetaData(blob.Name, containerClient, "IsQueued", DateTime.UtcNow.ToString()));

                    if (metaDataTasks.Count > 200)
                    {
                        logger.LogInformation("Purging collection of completed tasks....");
                        var waiting = Task.WhenAll(metaDataTasks);
                        await waiting;
                        metaDataTasks.Clear();
                    }
                }

                if (metaDataTasks.Count > 0)
                {
                    logger.LogInformation("Waiting for metadata updates to complete....");
                    var waiting = Task.WhenAll(metaDataTasks);
                    await waiting;
                }

                return new OkObjectResult($"Queued {fileCounter} files");
            }
            catch(Exception exe)
            {
                logger.LogError($"Failed to queue files: {exe.ToString()}");
                return new ExceptionResult(exe, true);
            }
        }
        public static async Task UpdateBlobMetaData(string blobName, BlobContainerClient containerClient, string key, string value, int retry = 0)
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

    }
}
