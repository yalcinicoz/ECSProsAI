using ECSPros.Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Crm.Infrastructure.Persistence.Configurations;

// Müşteri İlişkileri (talep/şikayet) tabloları — 2026-09-07, plan docs/crm-musteri-iliskileri-plani.md v2
public class TicketStatusConfiguration : IEntityTypeConfiguration<TicketStatus>
{
    public void Configure(EntityTypeBuilder<TicketStatus> b)
    {
        b.ToTable("crm_ticket_statuses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Color).HasMaxLength(20);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TicketSubjectConfiguration : IEntityTypeConfiguration<TicketSubject>
{
    public void Configure(EntityTypeBuilder<TicketSubject> b)
    {
        b.ToTable("crm_ticket_subjects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Type).HasMaxLength(20).IsRequired();
        b.Property(x => x.RequiredFields).HasColumnType("jsonb");
        b.HasIndex(x => x.LegacyId).IsUnique().HasFilter("\"LegacyId\" IS NOT NULL");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("crm_tickets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(20).IsRequired();
        b.Property(x => x.CustomerName).HasMaxLength(255);
        b.Property(x => x.CustomerPhone).HasMaxLength(30);
        b.Property(x => x.CallerName).HasMaxLength(255);
        b.Property(x => x.CallerPhone).HasMaxLength(30);
        b.Property(x => x.OrderNumber).HasMaxLength(50);
        b.Property(x => x.CreatedByName).HasMaxLength(150);
        b.Property(x => x.UpdatedByName).HasMaxLength(150);
        b.Property(x => x.Attachments).HasColumnType("jsonb");
        b.HasIndex(x => x.TrackingNo).IsUnique();
        b.HasIndex(x => x.LegacyId).IsUnique().HasFilter("\"LegacyId\" IS NOT NULL");
        b.HasIndex(x => x.OrderNumber);
        b.HasIndex(x => x.MemberId);
        b.HasIndex(x => x.OrderId);
        b.HasIndex(x => x.CustomerPhone);
        b.HasIndex(x => x.CallerPhone);
        b.HasIndex(x => new { x.StatusId, x.LastActivityAt });
        b.HasIndex(x => x.CreatedAt);
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId);
        b.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
        b.HasMany(x => x.Activities).WithOne(x => x.Ticket).HasForeignKey(x => x.TicketId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TicketActivityConfiguration : IEntityTypeConfiguration<TicketActivity>
{
    public void Configure(EntityTypeBuilder<TicketActivity> b)
    {
        b.ToTable("crm_ticket_activities");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserName).HasMaxLength(150);
        b.Property(x => x.TaggedUserName).HasMaxLength(150);
        b.Property(x => x.Attachments).HasColumnType("jsonb");
        b.HasIndex(x => new { x.TicketId, x.CreatedAt });
        b.HasIndex(x => x.LegacyId).IsUnique().HasFilter("\"LegacyId\" IS NOT NULL");
        b.HasIndex(x => x.TaggedUserId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TicketReadConfiguration : IEntityTypeConfiguration<TicketRead>
{
    public void Configure(EntityTypeBuilder<TicketRead> b)
    {
        b.ToTable("crm_ticket_reads");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserName).HasMaxLength(150);
        b.HasIndex(x => new { x.ActivityId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.TicketId, x.UserId });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TicketNotificationConfiguration : IEntityTypeConfiguration<TicketNotification>
{
    public void Configure(EntityTypeBuilder<TicketNotification> b)
    {
        b.ToTable("crm_ticket_notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(30).IsRequired();
        b.Property(x => x.Message).HasMaxLength(500).IsRequired();
        // çan sayacı: kullanıcının görülmemiş ya da açılmamış bildirimleri
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.TicketId });
        b.HasIndex(x => x.LegacyId).IsUnique().HasFilter("\"LegacyId\" IS NOT NULL");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TicketLegacyStaffConfiguration : IEntityTypeConfiguration<TicketLegacyStaff>
{
    public void Configure(EntityTypeBuilder<TicketLegacyStaff> b)
    {
        b.ToTable("crm_ticket_legacy_staff");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.Username).HasMaxLength(100);
        b.HasIndex(x => x.LegacyId).IsUnique();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
