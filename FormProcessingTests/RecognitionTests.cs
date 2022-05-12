using Microsoft.VisualStudio.TestTools.UnitTesting;
using FormProcessorFunction;
using System.Threading.Tasks;
using AzureUtilities;

namespace FormProcessingTests
{
    [TestClass]
    public class RecognitionTests
    {
        [DataRow("000001-BOL933.PDF")]
        [DataTestMethod]
        public async Task TestMethod1(string fileName)
        {
            var fpf = new FormProcessorFunction.Recognition();
            var uri = fpf.GetSourceFileUrl(fileName);
            var result = await fpf.ProcessFormRecognition(uri, 0);
            Assert.IsTrue(result.Length > 0, "Recognition result was empty!");
        }

        [DataRow("000001-BOL1005.PDF")]
        [DataTestMethod]
        public async Task End_to_End_processing(string fileName)
        {
            var fpf = new FormProcessorFunction.Recognition();
             var result = await fpf.ProcessMessage(new FileQueueMessage() { FileName = fileName });
            Assert.IsTrue(result, "Recognition process failure!");
        }
    }
}