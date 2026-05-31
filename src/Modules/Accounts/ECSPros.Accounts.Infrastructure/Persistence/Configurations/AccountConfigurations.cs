using ECSPros.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ECSPros.Accounts.Infrastructure.Persistence.Configurations;

public class CurrentAccountLedgerConfiguration : IEntityTypeConfiguration<CurrentAccountLedger>
{
    public void Configure(EntityTypeBuilder<CurrentAccountLedger> b)
    {
        b.ToTable("current_account_ledgers");
        b.HasKey(l => l.Id);
        b.Property(l => l.Currency).HasMaxLength(3).IsRequired();
        b.Property(l => l.Description).HasMaxLength(200);
        b.Property(l => l.Balance).HasColumnType("numeric(18,2)");
        b.HasOne(l => l.CurrentAccount).WithMany()
            .HasForeignKey(l => l.CurrentAccountId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(l => new { l.CurrentAccountId, l.Currency }).IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}


public class CurrentAccountGroupConfiguration : IEntityTypeConfiguration<CurrentAccountGroup>
{
    public void Configure(EntityTypeBuilder<CurrentAccountGroup> b)
    {
        b.ToTable("current_account_groups");
        b.HasKey(g => g.Id);
        b.Property(g => g.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(g => g.Code).IsUnique();
        b.Property(g => g.Name).HasMaxLength(200).IsRequired();
        b.Property(g => g.GroupType).HasMaxLength(20).IsRequired();
        b.Property(g => g.Description).HasMaxLength(500);
        b.HasMany(g => g.Accounts).WithOne(a => a.Group)
            .HasForeignKey(a => a.GroupId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CurrentAccountConfiguration : IEntityTypeConfiguration<CurrentAccount>
{
    public void Configure(EntityTypeBuilder<CurrentAccount> b)
    {
        b.ToTable("current_accounts");
        b.HasKey(a => a.Id);
        b.Property(a => a.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(a => a.Code).IsUnique();
        b.Property(a => a.Title).HasMaxLength(300).IsRequired();
        b.Property(a => a.AccountType).HasMaxLength(20).IsRequired();
        b.Property(a => a.TaxNumber).HasMaxLength(20);
        b.Property(a => a.TaxOffice).HasMaxLength(100);
        b.Property(a => a.ContactName).HasMaxLength(200);
        b.Property(a => a.Phone).HasMaxLength(30);
        b.Property(a => a.Email).HasMaxLength(200);
        b.Property(a => a.Address).HasMaxLength(500);
        b.Property(a => a.City).HasMaxLength(100);
        b.Property(a => a.Country).HasMaxLength(10);
        b.Property(a => a.CreditLimit).HasColumnType("numeric(18,2)");
        b.Property(a => a.Currency).HasMaxLength(3);
        b.Property(a => a.Notes).HasMaxLength(1000);
    }
}
