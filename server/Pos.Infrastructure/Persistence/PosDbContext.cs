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
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Register> Registers => Set<Register>();
    public DbSet<User> DomainUsers => Set<User>(); // named to avoid colliding with IdentityDbContext's own Users (DbSet<ApplicationUser>)
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CreditLedger> CreditLedgers => Set<CreditLedger>();
    public DbSet<Repair> Repairs => Set<Repair>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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

        // ---------- Unit ----------
        modelBuilder.Entity<Unit>(e =>
        {
            e.Property(x => x.SerialNumber).IsRequired().HasMaxLength(100);
            e.Property(x => x.Imei).HasMaxLength(20);

            e.HasIndex(x => x.SerialNumber).IsUnique();
            e.HasIndex(x => x.Imei).IsUnique().HasFilter("\"Imei\" IS NOT NULL");

            e.HasOne(x => x.Product)
                .WithMany(x => x.Units)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // A Unit references the SaleItem it was sold on; that item references
            // the Unit back (Step 20/24 return-verification flow). Break the cycle for
            // cascade-delete purposes on this side.
            e.HasOne(x => x.SoldOnSaleItem)
                .WithOne()
                .HasForeignKey<Unit>(x => x.SoldOnSaleItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Register ----------
        modelBuilder.Entity<Register>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        // ---------- User ----------
        // Id is NOT auto-generated here — it is expected to be set to match the
        // corresponding Identity ApplicationUser.Id (shared primary key, see User.cs).
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
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

            e.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
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

            e.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
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