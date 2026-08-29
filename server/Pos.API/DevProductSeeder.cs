using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api;

/// <summary>
/// Dev-only sample products so Step 24 (checkout) has something real to search/scan/add to
/// a cart against. There's no product data anywhere else in the repo yet — Step 18 (full
/// product CRUD, CSV bulk import) will eventually be the real way products get in, but
/// until it exists, checkout is untestable against an empty catalog without this.
///
/// Stock is seeded to match each product's actual tracking mode (see
/// Category.RequiresSerialTracking / Product.StockQuantity): serialized categories get real
/// StockUnit rows with generated serials; bulk categories get Product.BulkQuantityOnHand set
/// directly. Getting this wrong is exactly what silently zeroed out every product's stock
/// after the Phase 4 rework — don't reintroduce a plain int assignment here.
///
/// Idempotent (checked by SKU) and, like IdentitySeeder, gated to development only — this
/// should never run against a real store's database. Because it's idempotent by SKU, a
/// product already seeded before this stock logic existed won't be backfilled by re-running
/// this — delete it from the dev DB (or add stock via the Receive Stock screens) to pick up
/// the new seed quantities.
/// </summary>
public static class DevProductSeeder
{
    public static async Task SeedAsync(IServiceProvider services, bool isDevelopment)
    {
        if (!isDevelopment) return;

        var db = services.GetRequiredService<PosDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DevProductSeeder));

        var phonesCategory = await GetOrCreateCategoryAsync(db, "Phones", requiresSerialTracking: true, defaultWarrantyMonths: 12);
        var accessoriesCategory = await GetOrCreateCategoryAsync(db, "Accessories", requiresSerialTracking: false, defaultWarrantyMonths: 3);
        var computingCategory = await GetOrCreateCategoryAsync(db, "Computing", requiresSerialTracking: true, defaultWarrantyMonths: 12);

        // (Sku, Barcode, Name, Category, CostPrice, SalePrice, TaxClass, StockQuantity)
        // Prices are VAT-inclusive, matching SalesController's pricing assumption.
        // StockQuantity means: for a serialized category, how many StockUnit rows to
        // generate; for a bulk category, the BulkQuantityOnHand value to set.
        var seedProducts = new (string Sku, string Barcode, string Name, Category Category, decimal CostPrice, decimal SalePrice, TaxClass TaxClass, int StockQuantity)[]
        {
            ("PHN-TCN20", "6009123456781", "Tecno Camon 20", phonesCategory, 18000m, 22999m, TaxClass.Standard, 15),
            ("PHN-ITEL60", "6009123456782", "Itel A60", phonesCategory, 7500m, 9499m, TaxClass.Standard, 25),
            ("PHN-SAMA15", "6009123456783", "Samsung Galaxy A15", phonesCategory, 16500m, 20999m, TaxClass.Standard, 10),
            ("ACC-CHG65W", "6009123456784", "65W Fast Charger", accessoriesCategory, 800m, 1499m, TaxClass.Standard, 60),
            ("ACC-CBLTC", "6009123456785", "USB-C Cable 1m", accessoriesCategory, 150m, 349m, TaxClass.Standard, 120),
            ("ACC-CASE-A15", "6009123456786", "Galaxy A15 Case", accessoriesCategory, 200m, 499m, TaxClass.Standard, 40),
            ("ACC-EBUDS", "6009123456787", "Wireless Earbuds", accessoriesCategory, 1200m, 2299m, TaxClass.Standard, 30),
            ("COM-MOUSE", "6009123456788", "Wireless Mouse", computingCategory, 500m, 999m, TaxClass.Standard, 45),
            ("COM-32SSD", "6009123456789", "32GB Flash Drive", computingCategory, 350m, 699m, TaxClass.Standard, 50),
            ("COM-HP15", "6009123456790", "HP 15 Laptop (Core i5, 8GB/512GB)", computingCategory, 62000m, 74999m, TaxClass.Standard, 6),
        };

        var addedCount = 0;
        foreach (var seed in seedProducts)
        {
            var exists = await db.Products.AnyAsync(p => p.Sku == seed.Sku);
            if (exists) continue;

            var product = new Product
            {
                Sku = seed.Sku,
                Barcode = seed.Barcode,
                Name = seed.Name,
                CategoryId = seed.Category.Id,
                CostPrice = seed.CostPrice,
                SalePrice = seed.SalePrice,
                TaxClass = seed.TaxClass,
                ReorderThreshold = 5,
                IsActive = true,
            };

            if (seed.Category.RequiresSerialTracking)
            {
                // One StockUnit per unit of seed stock, each with a generated dev-only
                // serial — mirrors what StockController.ReceiveStock would create for a
                // real goods-receiving event.
                for (var i = 1; i <= seed.StockQuantity; i++)
                {
                    product.StockUnits.Add(new StockUnit
                    {
                        SerialNumber = $"DEV-{seed.Sku}-{i:D3}",
                        Status = "InStock",
                    });
                }
            }
            else
            {
                product.BulkQuantityOnHand = seed.StockQuantity;
            }

            db.Products.Add(product);
            addedCount++;
        }

        if (addedCount > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} dev sample product(s).", addedCount);
        }
    }

    private static async Task<Category> GetOrCreateCategoryAsync(
        PosDbContext db, string name, bool requiresSerialTracking, int defaultWarrantyMonths)
    {
        var existing = await db.Categories.FirstOrDefaultAsync(c => c.Name == name);
        if (existing is not null) return existing;

        var category = new Category
        {
            Name = name,
            RequiresSerialTracking = requiresSerialTracking,
            DefaultWarrantyMonths = defaultWarrantyMonths,
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }
}