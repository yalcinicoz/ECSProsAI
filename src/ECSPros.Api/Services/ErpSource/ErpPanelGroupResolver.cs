using ECSPros.Api.Services.Marketplace.Mapping;

namespace ECSPros.Api.Services.ErpSource;

public static class ErpPanelGroupResolver
{
    public static Guid? Resolve(string? sourceName, IReadOnlyDictionary<string, List<string>> codesByName,
        IReadOnlyDictionary<string, List<Guid>> groupsByCode)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return null;
        if (!codesByName.TryGetValue(ErpSourceSyncService.Normalize(sourceName), out var codes)) return null;
        if (codes.Count != 1)
            throw new InvalidOperationException($"ERP grup adı birden fazla sözlük koduna ait: {sourceName}");
        return ErpGroupMappingTargets.Resolve(groupsByCode.GetValueOrDefault(codes[0]) ?? []);
    }
}
