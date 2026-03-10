using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.ApproveReturn;

public class ApproveReturnCommandHandler : IRequestHandler<ApproveReturnCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;

    public ApproveReturnCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(ApproveReturnCommand request, CancellationToken cancellationToken)
    {
        var @return = await _context.Returns
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken);

        if (@return is null)
            return Result.Failure<bool>("İade talebi bulunamadı.");

        if (@return.Status != "requested")
            return Result.Failure<bool>($"'{@return.Status}' durumundaki iade onaylanamaz.");

        @return.Status = "approved";
        @return.UpdatedAt = DateTime.UtcNow;
        @return.UpdatedBy = request.ApprovedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
