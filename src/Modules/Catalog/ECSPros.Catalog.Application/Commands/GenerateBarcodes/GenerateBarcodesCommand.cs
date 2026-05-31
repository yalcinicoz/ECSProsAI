using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.GenerateBarcodes;

/// <summary>Sıralı EAN-13 barkodlar üretir ve sayacı atomik olarak artırır.</summary>
public record GenerateBarcodesCommand(int Count) : IRequest<Result<List<string>>>;

public class GenerateBarcodesCommandHandler : IRequestHandler<GenerateBarcodesCommand, Result<List<string>>>
{
    private readonly ICatalogDbContext _db;

    public GenerateBarcodesCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<string>>> Handle(GenerateBarcodesCommand request, CancellationToken ct)
    {
        if (request.Count <= 0)
            return Result.Failure<List<string>>("Barkod sayısı en az 1 olmalıdır.");

        // Mevcut sayacı oku
        var seqSetting = await _db.CatalogSettings
            .FirstOrDefaultAsync(s => s.Key == "barcode_sequence", ct);

        if (seqSetting is null)
            return Result.Failure<List<string>>("Barkod seri ayarı bulunamadı.");

        if (!long.TryParse(seqSetting.Value, out var current) || current < 1)
            return Result.Failure<List<string>>("Barkod seri değeri geçersiz.");

        // Üretilecek barkodlar: [current, current+count)
        seqSetting.Value = (current + request.Count).ToString();
        seqSetting.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var barcodes = Enumerable.Range(0, request.Count)
            .Select(i => ToEan13(current + i))
            .ToList();

        return Result.Success(barcodes);
    }

    /// <summary>12 haneli taban sayıdan EAN-13 üretir (check digit eklenir).</summary>
    public static string ToEan13(long number)
    {
        var digits = number.ToString().PadLeft(12, '0');
        if (digits.Length > 12)
            digits = digits[^12..]; // son 12 hane

        int sum = 0;
        for (int i = 0; i < 12; i++)
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        int check = (10 - (sum % 10)) % 10;
        return digits + check;
    }
}
