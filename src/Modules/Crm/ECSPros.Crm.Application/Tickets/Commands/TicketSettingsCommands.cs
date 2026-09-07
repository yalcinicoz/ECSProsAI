using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Commands;

public record UpsertTicketSubjectCommand(Guid? Id, string Name, string Type, int SortOrder, bool IsActive, List<string> RequiredFields) : IRequest<Result<Guid>>;
public class UpsertTicketSubjectCommandHandler(ICrmDbContext db) : IRequestHandler<UpsertTicketSubjectCommand, Result<Guid>>
{
    private static readonly HashSet<string> Alanlar = ["orderNumber", "callerName", "callerPhone", "body", "image"];
    public async Task<Result<Guid>> Handle(UpsertTicketSubjectCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return Result.Failure<Guid>("Konu adı zorunludur.");
        if (r.Type is not ("complaint" or "request")) return Result.Failure<Guid>("Tür complaint ya da request olmalı.");
        var fields = r.RequiredFields.Where(Alanlar.Contains).Distinct().ToList();
        TicketSubject s;
        if (r.Id is { } id)
        {
            s = await db.TicketSubjects.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        }
        else { s = new TicketSubject(); db.TicketSubjects.Add(s); }
        s.Name = r.Name.Trim(); s.Type = r.Type; s.SortOrder = r.SortOrder; s.IsActive = r.IsActive; s.RequiredFields = fields;
        await db.SaveChangesAsync(ct);
        return Result.Success(s.Id);
    }
}

public record UpsertTicketStatusCommand(Guid? Id, string Code, string Name, string Color, int SortOrder, bool IsHidden, bool IsResolved,
    bool ExemptFromDuplicateCheck, bool IsDefault) : IRequest<Result<Guid>>;
public class UpsertTicketStatusCommandHandler(ICrmDbContext db) : IRequestHandler<UpsertTicketStatusCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpsertTicketStatusCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Code)) return Result.Failure<Guid>("Kod ve ad zorunludur.");
        var code = r.Code.Trim().ToLowerInvariant();
        if (await db.TicketStatuses.AnyAsync(x => x.Code == code && x.Id != r.Id, ct)) return Result.Failure<Guid>("Bu kod zaten var.");
        TicketStatus s;
        if (r.Id is { } id) s = await db.TicketStatuses.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        else { s = new TicketStatus(); db.TicketStatuses.Add(s); }
        s.Code = code; s.Name = r.Name.Trim(); s.Color = string.IsNullOrWhiteSpace(r.Color) ? "#6b7280" : r.Color.Trim(); s.SortOrder = r.SortOrder;
        s.IsHidden = r.IsHidden; s.IsResolved = r.IsResolved; s.ExemptFromDuplicateCheck = r.ExemptFromDuplicateCheck; s.IsDefault = r.IsDefault;
        if (r.IsDefault)
            foreach (var d in await db.TicketStatuses.Where(x => x.IsDefault && x.Id != s.Id).ToListAsync(ct)) d.IsDefault = false;
        await db.SaveChangesAsync(ct);
        return Result.Success(s.Id);
    }
}

/// <summary>Kaydı gizle/geri al (eski Gizle bayrağı; silme yok). Gizli kayıt listede yalnız filtreyle görünür, mükerrer kontrolüne girmez.</summary>
public record SetTicketHiddenCommand(Guid TicketId, bool Hidden, Guid UserId, string UserName) : IRequest<Result<bool>>;
public class SetTicketHiddenCommandHandler(ICrmDbContext db) : IRequestHandler<SetTicketHiddenCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetTicketHiddenCommand r, CancellationToken ct)
    {
        var t = await db.Tickets.FirstOrDefaultAsync(x => x.Id == r.TicketId, ct);
        if (t is null) return Result.Failure<bool>("Kayıt bulunamadı.");
        t.IsHidden = r.Hidden; t.UpdatedByUserId = r.UserId; t.UpdatedByName = r.UserName; t.UpdatedBy = r.UserId;
        await db.SaveChangesAsync(ct);
        return Result.Success(r.Hidden);
    }
}
