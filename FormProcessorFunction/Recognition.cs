using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureUtilities;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Polly;
using System.Net;
using System.Threading;
using System.Reflection.Metadata;
using System.Linq;
using Microsoft.Azure.Functions.Worker;

namespace FormProcessorFunction
{
   public class Recognition
   {
      private readonly ILogger<Recognition> logger;

      public Recognition(ILogger<Recognition> logger)
      {
         this.logger = logger;
      }
      private static ServiceBusSender serviceBusProcessedSender = Settings.ServiceBusProcessedSenderClient;
      private static List<DocumentAnalysisClient> formRecogClients = Settings.FormRecognizerClients;
      private static BlobContainerClient outputContainerClient = Settings.OutputContainerClient;
      private static BlobContainerClient sourceContainerClient = Settings.SourceContainerClient;
      [Function("Recognition")]
      public async Task Run([ServiceBusTrigger("formqueue", Connection = "SERVICE_BUS_CONNECTION")] ServiceBusReceivedMessage message)
      {
         try
         {
            bool success = await ProcessMessage(message);
            if (!success)
            {
               throw new Exception("Failed to process message");
            }
         }
         catch (Exception exe)
         {
            logger.LogError(exe.ToString());
            throw;

         }
         return;
      }

      public async Task<bool> ProcessMessage(ServiceBusReceivedMessage queueMessage)
      {
         var fileMessage = queueMessage.As<FileQueueMessage>();
         return await ProcessMessage(fileMessage);
      }
      public async Task<bool> ProcessMessage(FileQueueMessage fileMessage)
      {

         var uri = GetSourceFileUrl(fileMessage.FileName);
         var recogOutput = await ProcessFormRecognition(uri, fileMessage.RecognizerIndex);
         if (string.IsNullOrWhiteSpace(recogOutput))
         {
            logger.LogError($"Failed to get Form Recognizer output for file '{fileMessage.FileName}'. Stopping processing and abandoning message.");
            return false;
         }
         var saveResult = await SaveRecognitionResults(recogOutput, fileMessage.FileName);
         if (!saveResult)
         {
            logger.LogError($"Unable to save results to output file for processed file '{fileMessage.FileName}'. Stopping processing and abandoning message.");
            return false;
         }
         var tagResult = await SetTagAndMetaDataOriginalFileAsProcessed(fileMessage.FileName);
         if (!tagResult)
         {
            logger.LogWarning($"Unable to tag the original processed file '{fileMessage.FileName}'. Will still complete the message");
         }
         var messageResult = await SendProcessedQueueMessage(fileMessage.FileName);
         if (!tagResult)
         {
            logger.LogWarning($"Unable to send 'processed' queue message for '{fileMessage.FileName}'. Will still complete the message");
         }
         return true;

      }
      public Uri GetSourceFileUrl(string sourceFile)
      {
         var sourceBlob = Settings.SourceContainerClient.GetBlobClient(sourceFile);
         return sourceBlob.Uri;
      }
      private DocumentAnalysisClient GetFormRecognizerClient(int index)
      {
         try
         {
            int clientCount = formRecogClients.Count;
            if (index < clientCount)
            {
               return formRecogClients[index];
            }
            else
            {
               int mod = index % clientCount;
               if (mod < clientCount)
               {
                  return formRecogClients[mod];
               }
               else
               {
                  return GetFormRecognizerClient(index - 1);
               }
            }
         }
         catch
         {
            return formRecogClients.First();
         }
      }
      public async Task<string> ProcessFormRecognition(Uri fileUri, int index)
      {
         Random jitterer = new Random();
         CancellationTokenSource source = new CancellationTokenSource();
         try
         {
            var formRecogClient = GetFormRecognizerClient(index);


            //Retry policy to back off if too many calls are made to the Form Recognizer
            var retryPolicy = Policy.Handle<RequestFailedException>(e => e.Status == (int)HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(retryAttempt++) + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)));

            AnalyzeDocumentOperation operation = null;

            var pollyResult = await retryPolicy.ExecuteAndCaptureAsync(async token =>
            {
               operation = await formRecogClient.AnalyzeDocumentFromUriAsync(WaitUntil.Started, Settings.DocumentProcessingModel, fileUri);
            }, source.Token);


            if (pollyResult.Outcome == OutcomeType.Failure)
            {
               logger.LogError($"Policy retries failed for {fileUri}. Resulting exception: {pollyResult.FinalException}");
               return String.Empty;
            }


            //Using this sleep vs. operation.WaitForCompletion() to avoid over loading the endpoint
            do
            {
               System.Threading.Thread.Sleep(2000);
               await retryPolicy.ExecuteAndCaptureAsync(async token =>
               {
                  await operation.UpdateStatusAsync();
               }, source.Token);

               if (pollyResult.Outcome == OutcomeType.Failure)
               {
                  logger.LogError($"Policy retries failed for calling UpdateStatusAsync on {fileUri}. Resulting exception: {pollyResult.FinalException}");
               }

            } while (!operation.HasCompleted);


            string output = JsonSerializer.Serialize(operation.Value, new JsonSerializerOptions() { WriteIndented = true });
            return output;
         }
         catch (Azure.RequestFailedException are)
         {
            if (are.ErrorCode == "InvalidRequest")
            {
               logger.LogError($"Failed to process file at URL:{fileUri.AbsoluteUri}. You may need to set permissions from the Form Recognizer to access your storage account. {are.ToString()}");
            }
            else
            {
               logger.LogError($"Failed to process file at URL:{fileUri.AbsoluteUri}. {are.ToString()}");
            }
            return String.Empty;
         }
         catch (Exception exe)
         {

            logger.LogError($"Failed to process file at URL:{fileUri.AbsoluteUri}. {exe.ToString()}");
            return String.Empty;
         }

      }
      public async Task<bool> SaveRecognitionResults(string results, string sourceFileName)
      {
         return await SaveRecognitionResults(results, sourceFileName, false);

      }
      private async Task<bool> SaveRecognitionResults(string results, string sourceFileName, bool isRetry)
      {
         try
         {
            string destinationFileName = $"{Path.GetFileNameWithoutExtension(sourceFileName)}.json";
            Response<BlobContentInfo> resp;
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(results)))
            {
               resp = await outputContainerClient.UploadBlobAsync(destinationFileName, ms);
            }
            if (resp.GetRawResponse().Status >= 300)
            {
               logger.LogError($"Error saving recognition results: {resp.GetRawResponse().ReasonPhrase}");
               return false;
            }
         }
         catch (RequestFailedException re)
         {
            if (re.ErrorCode == "BlobAlreadyExists" && !isRetry)
            {
               var newName = Path.GetFileNameWithoutExtension(sourceFileName) + "-" + DateTime.UtcNow.ToString("yyyy-MM-dd");
               return await SaveRecognitionResults(results, newName, true);
            }
            else
            {
               logger.LogError($"Error saving recognition results. Tried alternate filename: {re.ToString()}");
               return false;
            }
         }
         catch (Exception exe)
         {
            logger.LogError($"Error saving recognition results: {exe.ToString()}");
            return false;
         }

         return true;
      }


      public async Task<bool> SetTagAndMetaDataOriginalFileAsProcessed(string sourceFileName)
      {
         try
         {
            var sourceBlob = sourceContainerClient.GetBlobClient(sourceFileName);

            var tags = new Dictionary<string, string>();
            tags.Add("Processed", "true");
            var resp = await sourceBlob.SetTagsAsync(tags);
            var resp2 = await sourceBlob.SetMetadataAsync(tags);
            return !resp.IsError && !(resp2.GetRawResponse().Status >= 300);

         }
         catch (Exception exe)
         {
            logger.LogError(exe.ToString());
            return false;
         }
      }

      public async Task<bool> SendProcessedQueueMessage(string sourceFileName)
      {
         try
         {
            var sbMessage = new FileQueueMessage() { FileName = sourceFileName, ContainerName = Settings.ProcessedContainerName }.AsMessage();
            await serviceBusProcessedSender.SendMessageAsync(sbMessage);

            return true;

         }
         catch (Exception exe)
         {
            logger.LogError(exe.ToString());
            return false;
         }
      }
   }
}
