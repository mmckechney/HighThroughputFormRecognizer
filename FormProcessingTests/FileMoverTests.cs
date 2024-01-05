using FormProcessorFunction;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FormProcessingTests
{
    [TestClass]
    internal class FileMoverTests
    {
        [DataRow("000001-BOL933.PDF")]
        [DataTestMethod]
        public async Task TestMethod1(string fileName)
        {
            var factory = LoggerFactory.Create(builder => builder.AddConsole());
            var fpf = new FormProcessorFunction.Recognition(factory.CreateLogger<Recognition>());
            var uri = fpf.GetSourceFileUrl(fileName);
            var result = await fpf.ProcessFormRecognition(uri, 0);
            Assert.IsTrue(result.Length > 0, "Recognition result was empty!");
        }
    }
}
