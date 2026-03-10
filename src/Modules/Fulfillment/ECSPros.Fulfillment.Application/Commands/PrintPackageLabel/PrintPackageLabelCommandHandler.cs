using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.PrintPackageLabel;

public class PrintPackageLabelCommandHandler : IRequestHandler<PrintPackageLabelCommand, Result<bool>>
{
    private readonly IFulfillmentDbContext _context;

    public PrintPackageLabelCommandHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(PrintPackageLabelCommand request, CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, cancellationToken);

        if (package is null)
            return Result.Failure<bool>("Paket bulunamadı.");

        package.LabelPrintedAt = DateTime.UtcNow;
        package.Status = "label_printed";
        package.UpdatedAt = DateTime.UtcNow;
        package.UpdatedBy = request.PrintedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
