using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureUtilities
{
    public class FileQueueMessage
    {
        public string FileName { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;

    }
}
