using ECSPros.Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Crm.Infrastructure.Persistence.Configurations;

public class MemberGroupConfiguration : IEntityTypeConfiguration<MemberGroup>
{
    public void Configure(EntityTypeBuilder<MemberGroup> builder)
    {
        builder.ToTable("crm_member_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.MinOrderAmount).HasPrecision(18, 2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Members).WithOne(x => x.MemberGroup).HasForeignKey(x => x.MemberGroupId);
    }
}

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("crm_members");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Gender).HasMaxLength(10);
        builder.Property(x => x.TaxOffice).HasMaxLength(200);
        builder.Property(x => x.TaxNumber).HasMaxLength(20);
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        builder.HasIndex(x => x.Phone).IsUnique().HasFilter("\"Phone\" IS NOT NULL");
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Addresses).WithOne(x => x.Member).HasForeignKey(x => x.MemberId);
        builder.HasMany(x => x.Carts).WithOne(x => x.Member).HasForeignKey(x => x.MemberId);
        builder.HasOne(x => x.Wallet).WithOne(x => x.Member).HasForeignKey<Wallet>(x => x.MemberId);
        builder.HasOne(x => x.LoyaltyAccount).WithOne(x => x.Member).HasForeignKey<LoyaltyAccount>(x => x.MemberId);
        builder.HasOne(x => x.Credit).WithOne(x => x.Member).HasForeignKey<MemberCredit>(x => x.MemberId);
        builder.HasMany(x => x.Prices).WithOne(x => x.Member).HasForeignKey(x => x.MemberId);
        builder.HasMany(x => x.Discounts).WithOne(x => x.Member).HasForeignKey(x => x.MemberId);
        builder.HasMany(x => x.OrderTemplates).WithOne(x => x.Member).HasForeignKey(x => x.MemberId);
    }
}

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("crm_countries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(5).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PhoneCode).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Cities).WithOne(x => x.Country).HasForeignKey(x => x.CountryId);
    }
}

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("crm_cities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.CountryId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Districts).WithOne(x => x.City).HasForeignKey(x => x.CityId);
    }
}

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("crm_districts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.CityId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Neighborhoods).WithOne(x => x.District).HasForeignKey(x => x.DistrictId);
    }
}

public class NeighborhoodConfiguration : IEntityTypeConfiguration<Neighborhood>
{
    public void Configure(EntityTypeBuilder<Neighborhood> builder)
    {
        builder.ToTable("crm_neighborhoods");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PostalCode).HasMaxLength(10);
        builder.HasIndex(x => new { x.DistrictId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Streets).WithOne(x => x.Neighborhood).HasForeignKey(x => x.NeighborhoodId);
    }
}

public class StreetConfiguration : IEntityTypeConfiguration<Street>
{
    public void Configure(EntityTypeBuilder<Street> builder)
    {
        builder.ToTable("crm_streets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.NeighborhoodId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Buildings).WithOne(x => x.Street).HasForeignKey(x => x.StreetId);
    }
}

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("crm_buildings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BuildingNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AddressCode).HasMaxLength(50);
        builder.Property(x => x.PostalCode).HasMaxLength(10);
        builder.HasIndex(x => x.AddressCode).IsUnique().HasFilter("\"AddressCode\" IS NOT NULL");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("crm_addresses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CountryName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CityName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DistrictName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NeighborhoodName).HasMaxLength(100);
        builder.Property(x => x.StreetName).HasMaxLength(200);
        builder.Property(x => x.BuildingNumber).HasMaxLength(20);
        builder.Property(x => x.DoorNumber).HasMaxLength(20);
        builder.Property(x => x.AddressCode).HasMaxLength(50);
        builder.Property(x => x.PostalCode).HasMaxLength(10);
        builder.Property(x => x.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecipientPhone).HasMaxLength(20).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("crm_carts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SessionId).HasMaxLength(200);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Cart).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("crm_cart_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AddedPrice).HasPrecision(18, 2).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("crm_wallets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Balance).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Transactions).WithOne(x => x.Wallet).HasForeignKey(x => x.WalletId);
    }
}

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("crm_wallet_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Debit).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Credit).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(50);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        builder.ToTable("crm_loyalty_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.PointsToCurrencyRate).HasPrecision(18, 6);
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Transactions).WithOne(x => x.LoyaltyAccount).HasForeignKey(x => x.LoyaltyAccountId);
    }
}

public class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("crm_loyalty_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(50);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class MemberCreditConfiguration : IEntityTypeConfiguration<MemberCredit>
{
    public void Configure(EntityTypeBuilder<MemberCredit> builder)
    {
        builder.ToTable("crm_member_credits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreditLimit).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.UsedCredit).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Ignore(x => x.AvailableCredit);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class MemberPriceConfiguration : IEntityTypeConfiguration<MemberPrice>
{
    public void Configure(EntityTypeBuilder<MemberPrice> builder)
    {
        builder.ToTable("crm_member_prices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Price).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(x => new { x.MemberId, x.VariantId, x.MinQuantity }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class MemberDiscountConfiguration : IEntityTypeConfiguration<MemberDiscount>
{
    public void Configure(EntityTypeBuilder<MemberDiscount> builder)
    {
        builder.ToTable("crm_member_discounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DiscountRate).HasPrecision(5, 2).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class OrderTemplateConfiguration : IEntityTypeConfiguration<OrderTemplate>
{
    public void Configure(EntityTypeBuilder<OrderTemplate> builder)
    {
        builder.ToTable("crm_order_templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Template).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderTemplateItemConfiguration : IEntityTypeConfiguration<OrderTemplateItem>
{
    public void Configure(EntityTypeBuilder<OrderTemplateItem> builder)
    {
        builder.ToTable("crm_order_template_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitType).HasMaxLength(20).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class MemberSessionConfiguration : IEntityTypeConfiguration<MemberSession>
{
    public void Configure(EntityTypeBuilder<MemberSession> b)
    {
        b.ToTable("member_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.RefreshTokenHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(50);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
    }
}
