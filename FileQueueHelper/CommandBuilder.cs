using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Invocation;
using System.Text;

namespace FileQueueUtility
{
    public class CommandBuilder
    {
        ILogger log;
        public CommandBuilder(ILogger<CommandBuilder> log)
        {
            this.log = log;
        }
        public RootCommand ConfigureRootCommand(Worker worker)
        {
            var storageNameOption = new Option<string>(new string[] { "-s", "--storage-name" }, $"Name of the storage account to use (or environment variable: {EnvVariableNames.FILE_QUEUE_UTILITY_STORAGE_NAME})");
            var containerNameOption = new Option<string>(new string[] { "-c", "--container-name" }, $"Name of the container to scan for files to queue (or environment variable: {EnvVariableNames.FILE_QUEUE_UTILITY_CONTAINER_NAME})");
            var serviceBusNamespaceOption = new Option<string>(new string[] { "--sb", "--servicebus-namespace" }, $"Namespace of the service bus hosting the target queue (or environment variable: {EnvVariableNames.FILE_QUEUE_UTILITY_SERVICEBUS_NAMESPACE})");
            var queueNameOption = new Option<string>(new string[] { "-q", "--queue-name" }, $"Name of the queue to send messages to (or environment variable: {EnvVariableNames.FILE_QUEUE_UTILITY_SERVICEBUS_QUEUE})");
            var forceOption = new Option<bool>(new string[] { "-f", "--force" }, () => false, "Force queueing even if the metadata IsQueued exists");
            var keyVaultOption = new Option<string>(new string[] { "--kv", "--keyvault-name" }, $"Name of the Key vault to extract the storage key from");

          RootCommand rootCommand = new RootCommand(description: $"Utility application to scan an Azure storage account container and queue file pointers in a Service Bus for processing{Environment.NewLine}The options values can also be set as Environment Variables vs. command line arguments");
            rootCommand.Handler = CommandHandler.Create<string,string,string,string,string, bool>(worker.QueueFormsForProcessing);
            rootCommand.Add(storageNameOption);
            rootCommand.Add(containerNameOption);
            rootCommand.Add(serviceBusNamespaceOption);
            rootCommand.Add(queueNameOption);
            rootCommand.Add(keyVaultOption);
            rootCommand.Add(forceOption);

            return rootCommand;
        }
    }
}
