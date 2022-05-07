using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
namespace AzureUtilities
{
    public class ServiceBusHelper
    {
        private readonly ILogger<ServiceBusHelper> logger;
        public ServiceBusHelper(ILogger<ServiceBusHelper> logger)
        {
            this.logger = logger;

        }

        private ServiceBusClient CreateServiceBusClient(string serviceBusNamespace, string queueName)
        {
            var fullyQualified = $"{serviceBusNamespace}.servicebus.windows.net";
            return new ServiceBusClient(fullyQualified, AadHelper.TokenCredential);
        }

        public ServiceBusSender CreateServiceBusSender(string serviceBusNamespace, string queueName)
        {
            var sbc = CreateServiceBusClient(serviceBusNamespace, queueName);
            return sbc.CreateSender(queueName);
        }
    }
}
