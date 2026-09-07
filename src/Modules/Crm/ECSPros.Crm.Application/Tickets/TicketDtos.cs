namespace ECSPros.Crm.Application.Tickets;

public record TicketStatusDto(Guid Id, string Code, string Name, string Color, int SortOrder, bool IsHidden, bool IsResolved,
    bool ExemptFromDuplicateCheck, bool IsDefault);
public record TicketSubjectDto(Guid Id, string Name, string Type, int SortOrder, bool IsActive, List<string> RequiredFields);
public record TicketSettingsDto(List<TicketStatusDto> Statuses, List<TicketSubjectDto> Subjects);

public record TicketListItemDto(
    Guid Id, long TrackingNo, string Type, string SubjectName, string StatusCode, string StatusName, string StatusColor,
    string CustomerName, string CustomerPhone, string CallerName, string CallerPhone,
    string? OrderNumber, Guid? OrderId, Guid? MemberId, Guid? FirmPlatformId,
    string CreatedByName, DateTime CreatedAt, string? UpdatedByName, DateTime LastActivityAt, int ActivityCount,
    bool ReadByMe,            // son işlemi ben okudum mu (eski "Kontrol Edildi")
    bool TaggedMe, bool IsHidden);

public record TicketCounterDto(string StatusCode, string StatusName, string Color, int Count);

public record TicketActivityDto(
    Guid Id, string BodyHtml, List<string> Attachments, Guid? UserId, string UserName, DateTime CreatedAt,
    string StatusName, string? PreviousStatusName, bool StatusChanged, bool IsResolvedStatus,
    Guid? TaggedUserId, string? TaggedUserName, List<TicketReadDto> Reads);
public record TicketReadDto(Guid UserId, string UserName, DateTime ReadAt);
public record TicketNotificationTraceDto(Guid UserId, string Kind, string Message, DateTime SentAt, DateTime? SeenAt, DateTime? OpenedAt);
public record TicketBriefDto(Guid Id, long TrackingNo, string SubjectName, string StatusName, string StatusColor, DateTime CreatedAt, string CreatedByName);

public record TicketDetailDto(
    Guid Id, long TrackingNo, string Type, Guid SubjectId, string SubjectName, Guid StatusId, string StatusCode, string StatusName,
    string StatusColor, bool IsResolved,
    Guid? MemberId, int? LegacyMemberId, string CustomerName, string CustomerPhone, string CallerName, string CallerPhone,
    Guid? OrderId, string? OrderNumber, Guid? FirmPlatformId,
    string BodyHtml, List<string> Attachments,
    Guid? CreatedByUserId, string CreatedByName, DateTime CreatedAt, string? UpdatedByName, DateTime? UpdatedAt,
    DateTime LastActivityAt, bool IsHidden, int? LegacyId,
    List<TicketActivityDto> Activities,
    List<TicketNotificationTraceDto> Notifications,   // kim gördü / kim kayda girdi
    List<TicketBriefDto> PreviousTickets);             // aynı müşterinin diğer kayıtları

public record TicketNotificationDto(Guid Id, Guid TicketId, long TrackingNo, string Kind, string Message, DateTime CreatedAt,
    DateTime? SeenAt, DateTime? OpenedAt);
public record TicketNotificationPageDto(List<TicketNotificationDto> Items, int TotalCount, int PendingCount);

/// <summary>İşlem ekleme sonucu: yeni işlem + üretilen bildirimler (SignalR itmesi için).</summary>
public record TicketActivityResultDto(Guid ActivityId, long TrackingNo, List<TicketNotificationPushDto> Notifications);
public record TicketNotificationPushDto(Guid NotificationId, Guid UserId, string Kind, string Message);
