using System.Text.Json.Serialization;

namespace ECSPros.Api.Services.AiReporting;

// This is a recipe, never SQL or a cached result. Unknown properties fail closed.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportDefinition
{
    public int Version { get; init; }
    public string? Subject { get; init; }
    public string[]? Metrics { get; init; }
    public string[]? Dimensions { get; init; }
    public ReportFilter[]? Filters { get; init; }
    public string? Presentation { get; init; }
    public int Limit { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportFilter
{
    public string? Field { get; init; }
    public string? Operator { get; init; }
    public string[]? Values { get; init; }
}

public sealed record ReportField(string Id, string Label, string Kind, string Permission,
    string Description, IReadOnlyList<string> Operators)
{
    public string? DataType { get; init; }
    public IReadOnlyList<string>? MatchedValues { get; init; }
}
