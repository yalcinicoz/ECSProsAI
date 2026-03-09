using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Pos.Application.Queries.GetPosRegisters;

public record GetPosRegistersQuery(bool ActiveOnly = true) : IRequest<Result<List<PosRegisterDto>>>;

public record PosRegisterDto(
    Guid Id,
    string Code,
    string Name,
    string ReceiptPrefix,
    Guid WarehouseId,
    Guid FirmPlatformId,
    bool IsActive);
