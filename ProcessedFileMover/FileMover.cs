using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureUtilities;
using Microsoft.Azure.Amqp.Framing;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

namespace ProcessedFileMover
{
    public static class FileMover
    {
        static ILogger logger;

        [ServiceBusAccount("SERVICE_BUS_CONNECTION")]
        [FunctionName("FileMover")]
        public static async Task Run([ServiceBusTrigger("processedqueue", AutoCompleteMessages = true)] ServiceBusReceivedMessage message, ILogger log)
        {
            logger = log;

            var filemessage = message.As<FileQueueMessage>();
            logger.LogInformation($"Moving processed file {filemessage.FileName} to {Settings.ProcessedContainerName} container");
            bool success = await MoveOriginalFileToProcessed(filemessage.FileName);
            if(success)
            {
                logger.LogInformation($"Successfully move file {filemessage.FileName} to {Settings.ProcessedContainerName} container");
            }
            else
            {
                logger.LogInformation($"Failed move file {filemessage.FileName} to {Settings.ProcessedContainerName} container");
            }
          
        }

        public static async Task<bool> MoveOriginalFileToProcessed(string sourceFileName)
        {
            try
            {
                var sourceBlob = Settings.SourceContainerClient.GetBlobClient(sourceFileName);
                var destBlob = Settings.ProcessedContainerClient.GetBlobClient(sourceFileName);

                var operation = await destBlob.StartCopyFromUriAsync(sourceBlob.Uri);
                operation.WaitForCompletion();
                if (operation.GetRawResponse().Status >= 300)
                {
                    return false;
                }
                bool deleteResp = await sourceBlob.DeleteIfExistsAsync();
                return deleteResp;
            }
            catch (Exception exe)
            {
                logger.LogError(exe.ToString());
                return false;
            }
        }

        public static async Task<bool> CleanupFolder()
        {
            List<string> lstBlobsToMove = new List<string>();
            var blobList = Settings.SourceContainerClient.GetBlobsAsync(BlobTraits.Metadata);
            await foreach (var blob in blobList)
            {
                if (blob.Metadata.ContainsKey("Processed"))
                {
                    lstBlobsToMove.Add(blob.Name);
                }
            }
            if (lstBlobsToMove.Count == 0)
            {
                return true;
            }

            await Parallel.ForEachAsync(lstBlobsToMove,
                new ParallelOptions() { MaxDegreeOfParallelism = 20 },
                async (blobName, cancelationToken) =>
                {
                    bool success = await MoveOriginalFileToProcessed(blobName);
                    if (success)
                    {
                        logger.LogInformation($"Successfully moved file '{blobName}'");
                    }
                    else
                    {
                        logger.LogInformation($"File '{blobName}' was not moved!");
                    }
                });

            return true;
        }
    }
}
