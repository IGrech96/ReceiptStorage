using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using ReceiptStorage.Tags;

namespace ReceiptStorage.Tests;

public class TagResolverTests
{
    [Fact]
    public async Task ReadRulesFromJsonTest()
    {
        var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, leaveOpen:true))
        {
            await writer.WriteLineAsync("""
                                        {
                                        
                                          "TagResolverSettings": {
                                            "Rules":{
                                                "Title": {
                                                  "Tag": "$propertyvalue",
                                                  "PropertyName": {
                                                    "Equals": "Title"
                                                  }
                                                },
                                                "NewBor": {
                                                  "Tag": "newbor",
                                                  "PropertyValue": {
                                                    "Contains": "Минский р-н, Тестовый с/с, Тест, ул. Тестовая, д.777"
                                                  }
                                                }
                                              }
                                          }
                                        }
                                        """);
        }

        stream.Position = 0;
        var configurationManager = new ConfigurationManager();
        configurationManager.AddJsonStream(stream);

        var tagSettings = new TagResolverSettings();

        configurationManager.Bind(nameof(TagResolverSettings), tagSettings);

        Assert.NotEmpty(tagSettings.Rules);

        var titleRule = Assert.Contains("Title", tagSettings.Rules);
        var newBorRule = Assert.Contains("NewBor", tagSettings.Rules);

        var expectedTitleRule = new TagResolverRule()
        {
            Tag = "$propertyvalue",
            PropertyName = new()
            {
                Equals = "Title"
            }
        };

        var expectedNewBorRule = new TagResolverRule()
        {
            Tag = "newbor",
            PropertyValue = new()
            {
                Contains = "Минский р-н, Тестовый с/с, Тест, ул. Тестовая, д.777"
            }
        };

        Assert.Equivalent(expectedTitleRule, titleRule, true);
        Assert.Equivalent(expectedNewBorRule, newBorRule, true);
    }

    [Fact]
    public async Task TagResolverTest()
    {
        var settings = new TagResolverSettings()
        {
            Rules = new()
            {
                {
                    "Title", new TagResolverRule()
                    {
                        Tag = "$propertyvalue",
                        PropertyName = new()
                        {
                            Equals = "Title"
                        }
                    }
                },

                {
                    "NewBor", new TagResolverRule()
                    {
                        Tag = "newbor",
                        PropertyValue = new()
                        {
                            Contains = "Минскийр-н,Тестовыйс/с,Тест,ул.Тестовая,д.777",
                            IgnoreLineBreaks = true,
                            IgnoreWhiteSpaces = true
                        }
                    }
                }
            }
        };

        var options = Substitute.For<IOptionsMonitor<TagResolverSettings>>();
        options.CurrentValue.Returns(settings);

        var resolver = new TagResolver(options);

        var details1 = new ReceiptDetails()
        {
            Title = "Test Title",
            Amount = 100.0,
            Currency = "USD",
            Timestamp = DateTime.Now,
            Type = "Test"
        };
        var tags1 = await resolver.ResolveTagsAsync(details1, CancellationToken.None);
        Assert.Equivalent(new []{"Test_Title"}, tags1, true);

        var details2 = new ReceiptDetails()
        {
            Title = "Test Title2",
            Details = new []{("Address","Минский р-н, Тестовый с/с, Тест, ул. Тестовая, д.777")},
            Amount = 100.0,
            Currency = "USD",
            Timestamp = DateTime.Now,
            Type = "Test"
        };
        var tags2 = await resolver.ResolveTagsAsync(details2, CancellationToken.None);
        Assert.Equivalent(new []{"Test_Title2", "newbor"}, tags2, true);

        var details3 = new ReceiptDetails()
        {
            Title = "Test Title3",
            Details = new []{("Address","Минский р-н, Тестовый с/с, Тест, ул. Тестовая2, д.777")},
            Amount = 100.0,
            Currency = "USD",
            Timestamp = DateTime.Now,
            Type = "Test"
        };
        var tags3 = await resolver.ResolveTagsAsync(details3, CancellationToken.None);
        Assert.Equivalent(new []{"Test_Title3"}, tags3, true);
    }
}