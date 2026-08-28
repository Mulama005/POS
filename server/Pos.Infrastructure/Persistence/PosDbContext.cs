using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Infrastructure.Identity;

namespace Pos.Infrastructure.Persistence;

/// <summary>
/// Inherits IdentityDbContext rather than plain DbContext so Identity's own tables
/// (AspNetUsers, AspNetRoles, AspNetUserRoles, etc.) live in the same database and
/// the same migration history as the business tables — one connection string, one
/// `dotnet ef database update` to keep in sync, no risk of the two drifting apart.
/// Uses Guid keys throughout to match every other entity in this schema.
/// </summary>
public class PosDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<StockUnit> StockUnits => Set<StockUnit>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Register> Registers => Set<Register>();
    public DbSet<TillSession> TillSessions => Set<TillSession>();
    public DbSet<User> DomainUsers => Set<User>(); // named to avoid colliding with IdentityDbContext's own Users (DbSet<ApplicationUser>)
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CreditLedger> CreditLedgers => Set<CreditLedger>();
    public DbSet<Repair> Repairs => Set<Repair>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RepairJob> RepairJobs => Set<RepairJob>();
    public DbSet<RepairStatusHistory> RepairStatusHistories => Set<RepairStatusHistory>();
    public DbSet<RepairPartUsed> RepairPartsUsed => Set<RepairPartUsed>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<PricingTier> PricingTiers => Set<PricingTier>();
    public DbSet<ProductTierPrice> ProductTierPrices => Set<ProductTierPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // must run first — this is what builds the AspNetUsers/AspNetRoles tables

        // ---------- RefreshToken ----------
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade); // deleting an Identity user removes their refresh tokens
        });

        // ---------- Category ----------
        modelBuilder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.HasOne(x => x.ParentCategory)
                .WithMany(x => x.ChildCategories)
                .HasForeignKey(x => x.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict); // don't cascade-delete a whole category tree by accident
        });

        // ---------- Product ----------
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.SalePrice).HasColumnType("decimal(18,2)");

            e.HasIndex(x => x.Sku).IsUnique();
            e.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL AND \"Barcode\" <> ''");

            e.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // a category with products in it shouldn't be deletable
        });
      

        // ---------- StockUnit ----------
        modelBuilder.Entity<StockUnit>(e =>
        {
            e.Property(x => x.SerialNumber).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.SerialNumber).IsUnique();
            e.Property(x => x.Status).IsRequired().HasMaxLength(30);

            e.HasOne(x => x.Product)
                .WithMany(p => p.StockUnits)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Register ----------
        modelBuilder.Entity<Register>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        // ---------- TillSession ----------
        modelBuilder.Entity<TillSession>(e =>
        {
            e.Property(x => x.OpeningFloat).HasColumnType("decimal(18,2)");
            e.Property(x => x.ExpectedCashAtClose).HasColumnType("decimal(18,2)");
            e.Property(x => x.CountedCashAtClose).HasColumnType("decimal(18,2)");
            e.Property(x => x.VarianceAtClose).HasColumnType("decimal(18,2)");

            // Partial unique index: at most one Open session per register at a time.
            // This is the actual DB-level guarantee behind "at most one open till per
            // register" — TillController's own check is a courtesy for a clean error
            // message, this index is what stops a race from creating two.
            e.HasIndex(x => x.RegisterId)
                .IsUnique()
                .HasFilter("\"Status\" = 0"); // TillSessionStatus.Open

            e.HasOne(x => x.Register)
                .WithMany(x => x.TillSessions)
                .HasForeignKey(x => x.RegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.OpenedByUser)
                .WithMany()
                .HasForeignKey(x => x.OpenedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.ClosedByUser)
                .WithMany()
                .HasForeignKey(x => x.ClosedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- User ----------
        // Id is NOT auto-generated here — it is expected to be set to match the
        // corresponding Identity ApplicationUser.Id (shared primary key, see User.cs).
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.MfaSecret).HasMaxLength(512);
            e.HasIndex(x => x.Email).IsUnique();

            e.HasOne(x => x.AssignedRegister)
                .WithMany()
                .HasForeignKey(x => x.AssignedRegisterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- Customer ----------
        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            e.Property(x => x.CreditLimit).HasColumnType("decimal(18,2)");
            e.Property(x => x.CurrentCreditBalance).HasColumnType("decimal(18,2)");
        });

        // ---------- Sale ----------
        modelBuilder.Entity<Sale>(e =>
        {
            e.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.DiscountTotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.TaxTotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.Total).HasColumnType("decimal(18,2)");

            e.HasIndex(x => x.EtimsInvoiceNumber);

            e.HasOne(x => x.Register)
                .WithMany(x => x.Sales)
                .HasForeignKey(x => x.RegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Cashier)
                .WithMany(x => x.Sales)
                .HasForeignKey(x => x.CashierId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Customer)
                .WithMany(x => x.Sales)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.TillSession)
                .WithMany(x => x.Sales)
                .HasForeignKey(x => x.TillSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- SaleItem ----------
        modelBuilder.Entity<SaleItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TaxAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Sale)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Cascade); // deleting a sale (rare/test-only) removes its line items

            e.HasOne(x => x.Product)
                .WithMany(x => x.SaleItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.StockUnit)
                .WithMany()
                .HasForeignKey(x => x.StockUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Payment ----------
        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.ExternalReference);

            e.HasOne(x => x.Sale)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- CreditLedger ----------
        modelBuilder.Entity<CreditLedger>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.BalanceAfter).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Customer)
                .WithMany(x => x.CreditLedgerEntries)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Sale)
                .WithMany()
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RecordedByUser)
                .WithMany()
                .HasForeignKey(x => x.RecordedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Repair ----------
        modelBuilder.Entity<Repair>(e =>
        {
            e.Property(x => x.DeviceDescription).IsRequired().HasMaxLength(300);
            e.Property(x => x.EstimatedCost).HasColumnType("decimal(18,2)");
            e.Property(x => x.ActualCost).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Customer)
                .WithMany(x => x.Repairs)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AssignedTechnician)
                .WithMany(x => x.AssignedRepairs)
                .HasForeignKey(x => x.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- InventoryAdjustment ----------
        modelBuilder.Entity<InventoryAdjustment>(e =>
        {
            e.Property(x => x.Reason).IsRequired().HasMaxLength(300);

            e.HasOne(x => x.Product)
                .WithMany(x => x.InventoryAdjustments)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.StockUnit)
                .WithMany()
                .HasForeignKey(x => x.StockUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Repair)
                .WithMany(x => x.PartsConsumed)
                .HasForeignKey(x => x.RepairId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.AdjustedByUser)
                .WithMany()
                .HasForeignKey(x => x.AdjustedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- PricingTier ----------
        modelBuilder.Entity<PricingTier>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ---------- ProductTierPrice ----------
        modelBuilder.Entity<ProductTierPrice>(e =>
        {
            e.HasKey(x => new { x.ProductId, x.PricingTierId });

            e.Property(x => x.Price).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Product)
                .WithMany(p => p.TierPrices)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Tier)
                .WithMany(t => t.ProductTierPrices)
                .HasForeignKey(x => x.PricingTierId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- AuditLog ----------
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.ActionType).IsRequired().HasMaxLength(100);
            e.Property(x => x.EntityName).IsRequired().HasMaxLength(100);
            e.HasIndex(x => new { x.EntityName, x.EntityId });
            e.HasIndex(x => x.Timestamp);

            e.HasOne(x => x.User)
                .WithMany(x => x.AuditLogEntries)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}