using Microsoft.Extensions.Configuration;
using ReceiptStorage.Links;

namespace ReceiptStorage.Tests;

public class LinksTests
{
    [Fact]
    public async Task ReadRulesFromJsonTest()
    {
        var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, leaveOpen:true))
        {
            await writer.WriteLineAsync("""
                                        {
                                            "LinkSettings": {
                                              "Rules": {
                                                "Communal": {
                                                  "Лицевой счет": "Номер лицевого счета",
                                                  "Период": "Период"
                                                },
                                                "Communal2": {
                                                  "Лицевой счет": "Лицевой счет",
                                                  "Период": "Период"
                                                }
                                              }
                                            }
                                        }
                                        """);
        }

        stream.Position = 0;
        var configurationManager = new ConfigurationManager();
        configurationManager.AddJsonStream(stream);

        var tagSettings = new LinkSettings();

        configurationManager.Bind(nameof(LinkSettings), tagSettings);

        Assert.NotEmpty(tagSettings.Rules);

        var communalRule = Assert.Contains("Communal", tagSettings.Rules);
        var communalRule2 = Assert.Contains("Communal2", tagSettings.Rules);

        var expectedRule1 = new Dictionary<string, string>()
        {
            {"Лицевой счет", "Номер лицевого счета"},
            {"Период", "Период"}
        };

        var expectedRule2 = new Dictionary<string, string>()
        {
            {"Лицевой счет", "Лицевой счет"},
            {"Период", "Период"}
        };

        Assert.Equivalent(expectedRule1, communalRule, true);
        Assert.Equivalent(expectedRule2, communalRule2, true);
    }
}