using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ReceiptStorage.Tags;

public class TagResolver : ITagResolver
{
    private readonly IOptionsMonitor<TagResolverSettings> _options;

    public TagResolver(IOptionsMonitor<TagResolverSettings> options)
    {
        _options = options;
    }

    public ValueTask<string[]> ResolveTagsAsync(ReceiptDetails details, CancellationToken cancellationToken)
    {
        var tags = new List<string>();
        foreach (var (_,rule) in _options.CurrentValue.Rules)
        {
            if (TryMatch(details, rule, out var tag))
            {
                tags.AddRange(tag.Select(NormilizeTag));
            }
        }

        return ValueTask.FromResult(tags.Order().Distinct().ToArray());
    }

    private string NormilizeTag(string tag) => tag.Replace(" ", "_");

    private bool TryMatch(ReceiptDetails details, TagResolverRule rule, [NotNullWhen(true)] out string[]? tags)
    {
        var properties = IterateProperties()
            .Where(p => IsMatch(p.key, rule.PropertyName) &&
                        IsMatch(p.value, rule.PropertyValue))
            .ToArray();

        tags = null;
        if (!properties.Any())
        {
            return false;
        }

        if (string.Equals(rule.Tag, "$propertyname", StringComparison.InvariantCultureIgnoreCase))
        {
            tags = properties.Select(p => p.key).ToArray();
            return true;
        }

        if (string.Equals(rule.Tag, "$propertyvalue", StringComparison.InvariantCultureIgnoreCase))
        {
            tags = properties.Select(p => p.value).ToArray();
            return true;
        }

        tags = [rule.Tag];
        return true;

        bool IsMatch(string value, TagResolverMatchRule? rule)
        {
            if (rule?.IgnoreLineBreaks == true)
            {
                value = value.ReplaceLineEndings("");
            }

            if (rule?.IgnoreWhiteSpaces == true)
            {
                value = value.Replace(" ", "");
            }

            if (rule?.Equals != null)
            {
                return value.Equals(rule.Equals, rule.Comparison);
            }

            if (rule?.Contains != null)
            {
                return value.Contains(rule.Contains, rule.Comparison);
            }

            if (rule?.Match != null)
            {
                var regexOptions = rule.Comparison switch
                {
                    StringComparison.CurrentCulture => RegexOptions.None,
                    StringComparison.CurrentCultureIgnoreCase => RegexOptions.IgnoreCase,
                    StringComparison.InvariantCulture => RegexOptions.None,
                    StringComparison.InvariantCultureIgnoreCase => RegexOptions.IgnoreCase,
                    StringComparison.Ordinal => RegexOptions.None,
                    StringComparison.OrdinalIgnoreCase => RegexOptions.IgnoreCase,
                    _ => throw new ArgumentOutOfRangeException()
                };
                return Regex.IsMatch(value, rule.Match, regexOptions);
            }

            return true;
        }


        IEnumerable<(string key, string value)> IterateProperties()
        {
            yield return (nameof(ReceiptDetails.Title), details.Title);
            yield return (nameof(ReceiptDetails.Type), details.Type);
            foreach (var data in details.Details)
            {
                yield return data;
            }
        }
    }
}

public class TagResolverSettings
{
    public Dictionary<string, TagResolverRule> Rules { get; set; } = new();
}

public class TagResolverRule
{
    public string Tag { get; set; }

    public TagResolverMatchRule? PropertyName { get; set; }

    public TagResolverMatchRule? PropertyValue { get; set; }
}

public class TagResolverMatchRule
{
    public bool IgnoreLineBreaks { get; set; }

    public bool IgnoreWhiteSpaces { get; set; }

    public string? Equals { get; set; }

    public string? Contains { get; set; }

    public string? Match { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StringComparison Comparison { get; set; } = StringComparison.InvariantCultureIgnoreCase;
}