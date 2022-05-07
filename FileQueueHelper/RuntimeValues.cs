using System;

namespace FileQueueUtility
{

    public class RuntimeValues
    {
        string cmdStorageName, cmdContainerName, cmdServicebusNamespace, cmdQueueName;
        string storageName = string.Empty, containerName = string.Empty, servicebusNamespace = string.Empty, queueName = string.Empty;
        public RuntimeValues(string storageName, string containerName, string servicebusNamespace, string queueName)
        {
            this.cmdStorageName = storageName;
            this.cmdContainerName = containerName;
            this.cmdServicebusNamespace = servicebusNamespace;
            this.cmdQueueName = queueName;
        }

        public string StorageName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(storageName))
                {
                    if (!string.IsNullOrWhiteSpace(cmdStorageName))
                    {
                        storageName = cmdStorageName;
                        if(Environment.OSVersion.Platform.ToString().ToLower().Contains("wim"))
                        {
                            Environment.SetEnvironmentVariable(EnvVariableNames.FILE_QUEUE_UTILITY_STORAGE_NAME, cmdContainerName, EnvironmentVariableTarget.User);
                        }
                    }
                    else
                    {
                        var tmp = Environment.GetEnvironmentVariable(EnvVariableNames.FILE_QUEUE_UTILITY_STORAGE_NAME);
                        if (tmp != null)
                        {
                            storageName = tmp;
                        }
                    }
                }
                return storageName;
            }
        }
        public string ContainerName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(containerName))
                {
                    if (!string.IsNullOrWhiteSpace(cmdContainerName))
                    {
                        containerName = cmdContainerName;
                        if (Environment.OSVersion.Platform.ToString().ToLower().Contains("wim"))
                        {
                            Environment.SetEnvironmentVariable(EnvVariableNames.FILE_QUEUE_UTILITY_CONTAINER_NAME, cmdContainerName, EnvironmentVariableTarget.User);
                        }
                    }
                    else
                    {
                        var tmp = Environment.GetEnvironmentVariable(EnvVariableNames.FILE_QUEUE_UTILITY_CONTAINER_NAME);
                        if (tmp != null)
                        {
                            containerName = tmp;
                        }
                    }
                }
                return containerName;
            }
        }
        public string ServicebusNamespace
        {
            get
            {
                if (string.IsNullOrWhiteSpace(servicebusNamespace))
                {
                    if (!string.IsNullOrWhiteSpace(cmdServicebusNamespace))
                    {
                        servicebusNamespace = cmdServicebusNamespace;
                        if (Environment.OSVersion.Platform.ToString().ToLower().Contains("wim"))
                        {
                            Environment.SetEnvironmentVariable(EnvVariableNames.FILE_QUEUE_UTILITY_SERVICEBUS_NAMESPACE, cmdServicebusNamespace, EnvironmentVariableTarget.User);
                        }
                    }
                    else
                    {
                        var tmp = Environment.GetEnvironmentVariable(EnvVariableNames.FILE_QUEUE_UTILITY_SERVICEBUS_NAMESPACE);
                        if (tmp != null)
                        {
                            servicebusNamespace = tmp;
                        }
                    }
                }
                return servicebusNamespace;
            }
        }

        public string QueueName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(queueName))
                {
                    if (!string.IsNullOrWhiteSpace(cmdQueueName))
                    {
                        queueName = cmdQueueName;
                        if (Environment.OSVersion.Platform.ToString().ToLower().Contains("wim"))
                        {
                            Environment.SetEnvironmentVariable(EnvVariableNames.FILE_QUEUE_UTILITY_SERVICEBUS_QUEUE, cmdQueueName, EnvironmentVariableTarget.User);
                        }
                    }
                    else
                    {
                        var tmp = Environment.GetEnvironmentVariable(EnvVariableNames.FILE_QUEUE_UTILITY_SERVICEBUS_QUEUE);
                        if (tmp != null)
                        {
                            queueName = tmp;
                        }
                    }
                }
                return queueName;
            }
        }
    }
}

