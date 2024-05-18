using iText.Kernel.Pdf;
using ReceiptStorage.Templates;

namespace ReceiptStorage.Tests
{
    public class TemplateTests
    {
        [Fact]
        public async Task MtbTest()
        {
            var stream = new FileStream("Source\\mtb_payment.pdf", FileMode.Open);
            var template = new MtbankTemplate();
            var result = await template.TryExtractAsync(new PdfDocument(new PdfReader(stream)), CancellationToken.None);

            Assert.Equal("Коммунальные платежи", result.Title);
            Assert.Equal("MTB", result.Type);
            Assert.Equal("BYN", result.Currency);
            Assert.Equal(new DateTime(2024,5,18, 17,29,1), result.Timestamp);
            Assert.Equal(212.73, result.Amount, 7);
        }
    }
}