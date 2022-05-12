using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using System.Collections.Generic;
using Azure;

namespace AzureUtilities
{
    public class Settings
    {
        static Settings()
        {
            var loggerFactory = new LoggerFactory();
            storageLogger = loggerFactory.CreateLogger<StorageHelper>();
            sblogger = loggerFactory.CreateLogger<ServiceBusHelper>();
        }
        private static ILogger<StorageHelper> storageLogger;
        private static ILogger<ServiceBusHelper> sblogger;
        private static string _endpoint = string.Empty;
        private static List<string> _keys = new List<string>();
        private static string _queueName = string.Empty;
        private static string _processedQueueName = string.Empty;
        private static string _sourceContainerName = string.Empty;
        private static string _processedContainerName = string.Empty;
        private static string _outputContainerName = string.Empty;
        private static string _storageAccountName = string.Empty;
        private static string _documentProcessingModel = string.Empty;
        private static string _serviceBusNamespaceName = string.Empty;
        private static BlobContainerClient _sourceContainerClient;
        private static BlobContainerClient _processedContainerClient;
        private static BlobContainerClient _outputContainerClient;
        private static ServiceBusSender _serviceBusSenderClient;
        private static ServiceBusSender _serviceBusProcessedSenderClient;
        private static List<DocumentAnalysisClient> _formRecognizerClients = new List<DocumentAnalysisClient>();

        public static string Endpoint
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_endpoint))
                {
                    _endpoint = Environment.GetEnvironmentVariable("FORM_RECOGNIZER_ENDPOINT");
                }
                return _endpoint;
            }
        }
        public static List<string> Keys
        {
            get
            {
                if (_keys.Count == 0)
                {
                    var tmp = Environment.GetEnvironmentVariable("FORM_RECOGNIZER_KEY");
                    if (!string.IsNullOrWhiteSpace(tmp))
                    {
                        _keys.AddRange(tmp.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    }
                    else
                    {
                        storageLogger.LogError("FORM_RECOGNIZER_KEY is empty");
                    }
                }
                return _keys;
            }
        }
        public static string QueueName
        {
            get
            {
                if (string.IsNullOrEmpty(_queueName))
                {
                    _queueName = Environment.GetEnvironmentVariable("SERVICE_BUS_QUEUE_NAME");
                    if (string.IsNullOrEmpty(_queueName)) sblogger.LogError("SERVICE_BUS_QUEUE_NAME setting is empty!");
                }
                return _queueName;
            }
        }
        public static string ProcessedQueueName
        {
            get
            {
                if (string.IsNullOrEmpty(_processedQueueName))
                {
                    _processedQueueName = Environment.GetEnvironmentVariable("SERVICE_BUS_PROCESSED_QUEUE_NAME");
                    if (string.IsNullOrEmpty(_processedQueueName)) sblogger.LogError("SERVICE_BUS_PROCESSED_QUEUE_NAME setting is empty!");
                }
                return _processedQueueName;
            }
        }
        public static string SourceContainerName
        {
            get
            {
                if (string.IsNullOrEmpty(_sourceContainerName))
                {
                    _sourceContainerName = Environment.GetEnvironmentVariable("FORM_RECOGNIZER_SOURCE_CONTAINER_NAME");
                    if (string.IsNullOrEmpty(_sourceContainerName)) storageLogger.LogError("FORM_RECOGNIZER_SOURCE_CONTAINER_NAME setting is empty!");
                }
                return _sourceContainerName;

            }
        }
        public static string ProcessedContainerName
        {
            get
            {
                if (string.IsNullOrEmpty(_processedContainerName))
                {
                    _processedContainerName = Environment.GetEnvironmentVariable("FORM_RECOGNIZER_PROCESSED_CONTAINER_NAME");
                    if(string.IsNullOrEmpty(_processedContainerName)) storageLogger.LogError("FORM_RECOGNIZER_PROCESSED_CONTAINER_NAME setting is empty!");
                }
                return _processedContainerName;

            }
        }
        public static string OutputContainerName
        {
            get
            {
                if (string.IsNullOrEmpty(_outputContainerName))
                {
                    _outputContainerName = Environment.GetEnvironmentVariable("FORM_RECOGNIZER_OUTPUT_CONTAINER_NAME");
                    if (string.IsNullOrEmpty(_outputContainerName)) storageLogger.LogError("FORM_RECOGNIZER_OUTPUT_CONTAINER_NAME setting is empty!");
                }
                return _outputContainerName;

            }
        }
        public static string StorageAccountName
        {
            get
            {
                if (string.IsNullOrEmpty(_storageAccountName))
                {
                    _storageAccountName = Environment.GetEnvironmentVariable("FORM_RECOGNIZER_STORAGE_ACCOUNT_NAME");
                    if (string.IsNullOrEmpty(_storageAccountName)) storageLogger.LogError("FORM_RECOGNIZER_STORAGE_ACCOUNT_NAME setting is empty!");
                }
                return _storageAccountName;

            }
        }
        public static string DocumentProcessingModel
        {
            get
            {
                if (string.IsNullOrEmpty(_documentProcessingModel))
                {
                    _documentProcessingModel = Environment.GetEnvironmentVariable("FORM_RECOGNIZER_MODEL_NAME");
                    if (string.IsNullOrWhiteSpace(_documentProcessingModel)) _documentProcessingModel = "prebuilt-document";
                }
                return _documentProcessingModel;

            }
        }
        public static string ServiceBusNameSpaceName
        {
            get
            {
                if (string.IsNullOrEmpty(_serviceBusNamespaceName))
                {
                    _serviceBusNamespaceName = Environment.GetEnvironmentVariable("SERVICE_BUS_NAMESPACE_NAME");
                    if (string.IsNullOrEmpty(_serviceBusNamespaceName)) storageLogger.LogError("SERVICE_BUS_NAMESPACE_NAME setting is empty!");
                }
                return _serviceBusNamespaceName;

            }
        }
        public static BlobContainerClient SourceContainerClient
        {
            get
            {
                if (_sourceContainerClient == null)
                {

                    _sourceContainerClient = new StorageHelper(storageLogger).CreateBlobContainerClient(SourceContainerName, StorageAccountName);
                }
                return _sourceContainerClient;
            }
        }
        public static BlobContainerClient ProcessedContainerClient
        {
            get
            {
                if (_processedContainerClient == null)
                {
                    _processedContainerClient = new StorageHelper(storageLogger).CreateBlobContainerClient(ProcessedContainerName, StorageAccountName);
                }
                return _processedContainerClient;
            }
        }
        public static BlobContainerClient OutputContainerClient
        {
            get
            {
                if (_outputContainerClient == null)
                {
                    _outputContainerClient = new StorageHelper(storageLogger).CreateBlobContainerClient(OutputContainerName, StorageAccountName);
                }
                return _outputContainerClient;
            }
        }
        public static ServiceBusSender ServiceBusSenderClient
        {
            get
            {
                if (_serviceBusSenderClient == null)
                {
                    _serviceBusSenderClient = new ServiceBusHelper(sblogger).CreateServiceBusSender(ServiceBusNameSpaceName, QueueName);
                }
                return _serviceBusSenderClient;
            }
        }

        public static ServiceBusSender ServiceBusProcessedSenderClient
        {
            get
            {
                if (_serviceBusProcessedSenderClient == null)
                {
                    _serviceBusProcessedSenderClient = new ServiceBusHelper(sblogger).CreateServiceBusSender(ServiceBusNameSpaceName, ProcessedQueueName);
                }
                return _serviceBusProcessedSenderClient;
            }
        }

        public static List<DocumentAnalysisClient> FormRecognizerClients
        {
            get
            {
                if(_formRecognizerClients.Count == 0)
                {
                    foreach(var key in Keys)
                    {
                        var  credential = new AzureKeyCredential(key);
                        var  formRecogClient = new DocumentAnalysisClient(new Uri(Settings.Endpoint), credential);
                        _formRecognizerClients.Add(formRecogClient);
                    }    
                }
                return _formRecognizerClients;
            }
        }
    }
}
