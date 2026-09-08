using System.Text.Json;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetMyPreferences;

// DataGrid F5 (docs/datagrid-standardi-plani.md §2.9, K7): kişisel panel tercihleri — iam.Users.Preferences jsonb.
// Anahtar bazlı okuma/yazma (örn. "grids.orders" → { views: [...] }); tüm sözlük tek istekte döner (panel açılışında bir kez).
// Yalnız kendi kaydı: userId JWT'den gelir, başka kullanıcı tercihi okunamaz/yazılamaz.

public record GetMyPreferencesQuery(Guid UserId) : IRequest<Result<Dictionary<string, object>>>;

public class GetMyPreferencesQueryHandler(IIamDbContext db) : IRequestHandler<GetMyPreferencesQuery, Result<Dictionary<string, object>>>
{
    public async Task<Result<Dictionary<string, object>>> Handle(GetMyPreferencesQuery r, CancellationToken ct)
    {
        var prefs = await db.Users.AsNoTracking().Where(u => u.Id == r.UserId).Select(u => u.Preferences).FirstOrDefaultAsync(ct);
        return Result.Success(prefs ?? new Dictionary<string, object>());
    }
}

/// <summary>Tek anahtarı değiştirir (Value null → anahtar silinir). Anahtar: harf/rakam/nokta/alt çizgi/tire, ≤ 80 karakter; değer ≤ 64 KB.</summary>
public record SetMyPreferenceCommand(Guid UserId, string Key, JsonElement? Value) : IRequest<Result<bool>>;

public class SetMyPreferenceCommandHandler(IIamDbContext db) : IRequestHandler<SetMyPreferenceCommand, Result<bool>>
{
    public const int MaxValueBytes = 64 * 1024;

    public async Task<Result<bool>> Handle(SetMyPreferenceCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Key) || r.Key.Length > 80 || !r.Key.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-'))
            return Result.Failure<bool>("Geçersiz tercih anahtarı.");
        if (r.Value is { } v && v.GetRawText().Length > MaxValueBytes)
            return Result.Failure<bool>("Tercih değeri çok büyük (en fazla 64 KB).");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null) return Result.Failure<bool>("Kullanıcı bulunamadı.");

        var prefs = new Dictionary<string, object>(user.Preferences ?? new Dictionary<string, object>());
        if (r.Value is null || r.Value.Value.ValueKind == JsonValueKind.Null) prefs.Remove(r.Key);
        else prefs[r.Key] = r.Value.Value.Clone();
        user.Preferences = prefs;   // yeni sözlük örneği → EF değişikliği görür (jsonb)
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
