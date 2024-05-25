using iText.Kernel.Pdf;
using ReceiptStorage.Templates;

namespace ReceiptStorage.Tests
{
    public class TemplateTests
    {
        [Fact]
        public async Task MtbTest()
        {
            var stream = new FileStream("Source\\test_payment.pdf", FileMode.Open);
            var template = new MtbankTemplate();
            var result = await template.TryExtractAsync(new PdfDocument(new PdfReader(stream)), CancellationToken.None);

            Assert.Equal("Пополнение счета", result.Title);
            Assert.Equal("MTB", result.Type);
            Assert.Equal("BYN", result.Currency);
            Assert.Equal(new DateTime(2024,5,24, 19,56,13), result.Timestamp);
            Assert.Equal(35.0, result.Amount, 7);
        }
    }
}