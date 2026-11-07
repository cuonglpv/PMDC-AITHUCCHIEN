using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using YamlDotNet.RepresentationModel;

namespace ClaimLens.Api.Tests.Contract;

/// <summary>
/// Reads claims-api.yaml and checks a real response against it. OpenAPI 3.1 schemas are JSON Schema 2020-12, so they are
/// gathered under $defs and evaluated as they are.
/// </summary>
public sealed class ContractDocument
{
    public static readonly ContractDocument Instance = new(Path.Combine(AppContext.BaseDirectory, "contract", "claims-api.yaml"));

    private readonly JsonObject document;
    private readonly List<(Regex Path, string Template)> paths = [];
    private readonly string definitions;

    private ContractDocument(string file)
    {
        var yaml = new YamlStream();
        using (var reader = new StreamReader(file))
        {
            yaml.Load(reader);
        }

        document = ToJson(yaml.Documents[0].RootNode).AsObject();

        foreach (var (template, _) in document["paths"]!.AsObject())
        {
            var pattern = "^" + Regex.Replace(template, @"\{[^}]+\}", "[^/]+") + "$";
            paths.Add((new Regex(pattern, RegexOptions.CultureInvariant), template));
        }

        // Exact paths win over templates, as in routing.
        paths = paths.OrderBy(path => path.Template.Count(character => character == '{')).ToList();

        var defs = document["components"]!["schemas"]!.DeepClone().ToJsonString();
        definitions = defs.Replace("#/components/schemas/", "#/$defs/", StringComparison.Ordinal);
    }

    /// <summary>The problems with this response, or an empty list when the contract allows it.</summary>
    public IReadOnlyList<string> Check(string method, string path, int status, string? contentType, string body)
    {
        var template = paths.FirstOrDefault(candidate => candidate.Path.IsMatch(path)).Template;
        if (template is null)
        {
            return [$"{method} {path} is not in the contract"];
        }

        var operation = document["paths"]![template]![method.ToLowerInvariant()]?.AsObject();
        if (operation is null)
        {
            return [$"{method} {template} is not in the contract"];
        }

        var responses = operation["responses"]!.AsObject();
        var response = responses[status.ToString(System.Globalization.CultureInfo.InvariantCulture)]?.AsObject();
        if (response is null)
        {
            return [$"{operation["operationId"]} does not declare status {status}"];
        }

        if (response["$ref"]?.GetValue<string>() is { } reference)
        {
            response = document["components"]!["responses"]![reference.Split('/')[^1]]!.AsObject();
        }

        var content = response["content"]?.AsObject();
        if (content is null)
        {
            return string.IsNullOrEmpty(body) ? [] : [$"{operation["operationId"]} {status} declares no body but sent one"];
        }

        var mediaType = contentType?.Split(';')[0].Trim();
        if (mediaType is null || !content.ContainsKey(mediaType))
        {
            return [$"{operation["operationId"]} {status} does not declare {mediaType}"];
        }

        // A page image is declared as image/png with a binary string schema: the media type is all there is to check.
        if (mediaType != "application/json" && !mediaType.EndsWith("+json", StringComparison.Ordinal))
        {
            return [];
        }

        var schema = content[mediaType]!["schema"]!.DeepClone().ToJsonString()
            .Replace("#/components/schemas/", "#/$defs/", StringComparison.Ordinal);
        var evaluated = JsonSchema
            .FromText($$"""{"$schema":"https://json-schema.org/draft/2020-12/schema","$defs":{{definitions}},"allOf":[{{schema}}]}""")
            .Evaluate(JsonDocument.Parse(body).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        return evaluated.IsValid
            ? []
            : (evaluated.Details ?? [])
                .Where(detail => detail.Errors is not null)
                .SelectMany(detail => detail.Errors!.Select(error => $"{operation["operationId"]} {status} at {detail.InstanceLocation}: {error.Value}"))
                .ToList();
    }

    private static JsonNode ToJson(YamlNode node) =>
        node switch
        {
            YamlMappingNode mapping => new JsonObject(mapping.Children.Select(pair =>
                KeyValuePair.Create(((YamlScalarNode)pair.Key).Value!, (JsonNode?)ToJson(pair.Value)))),
            YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(item => (JsonNode?)ToJson(item)).ToArray()),
            YamlScalarNode scalar => Scalar(scalar),
            _ => throw new InvalidOperationException($"Unexpected YAML node: {node.NodeType}"),
        };

    private static JsonNode Scalar(YamlScalarNode scalar)
    {
        var text = scalar.Value ?? "";
        if (scalar.Style is YamlDotNet.Core.ScalarStyle.Plain)
        {
            if (text is "null" or "~" or "")
            {
                return JsonValue.Create("__null__")!;
            }

            if (text is "true" or "false")
            {
                return JsonValue.Create(text == "true");
            }

            if (long.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var whole))
            {
                return JsonValue.Create(whole);
            }

            if (double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var real))
            {
                return JsonValue.Create(real);
            }
        }

        return JsonValue.Create(text);
    }
}
