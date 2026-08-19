using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Persistence;

public sealed class KvitteriaDbContext(DbContextOptions<KvitteriaDbContext> options) : DbContext(options)
{
	public DbSet<Receipt> Receipts => Set<Receipt>();
	public DbSet<ReceiptLine> ReceiptLines => Set<ReceiptLine>();
	public DbSet<CanonicalItem> CanonicalItems => Set<CanonicalItem>();
	public DbSet<ItemAlias> ItemAliases => Set<ItemAlias>();
	public DbSet<Meal> Meals => Set<Meal>();
	public DbSet<ReceiptMealCoverage> ReceiptMealCoverages => Set<ReceiptMealCoverage>();
	public DbSet<Category> Categories => Set<Category>();
	public DbSet<Merchant> Merchants => Set<Merchant>();

	/// <summary>Money is kroner with øre.</summary>
	private const int MoneyPrecision = 12;
	private const int MoneyScale = 2;

	/// <summary>Quantities can be fractional (kg), but never need more than grams of resolution.</summary>
	private const int QuantityPrecision = 10;
	private const int QuantityScale = 3;

	private const int NameLength = 200;
	private const int RawNameLength = 300;

	/// <summary>Matches the range MealCoverage enforces.</summary>
	private const int MaxDaysCovered = 31;

	// Delete policy:
	//  - Lines and meal coverage are owned by their receipt      -> Cascade.
	//  - An alias is meaningless without its canonical item      -> Cascade.
	//  - Classification is optional metadata, never owning        -> SetNull.
	//  - Anything a row depends on for identity or history        -> Restrict.
	// Receipt -> Merchant is Restrict: deleting a merchant must not delete its receipts.
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.HasPostgresExtension("citext");

		modelBuilder.Entity<Receipt>(entity => {
			entity.HasKey(e => e.Id);

			entity.Property(e => e.GrandTotal).HasPrecision(MoneyPrecision, MoneyScale);
			entity.Property(e => e.PantTotal).HasPrecision(MoneyPrecision, MoneyScale);

			// Stored as text so a database value is self-describing and independent of enum ordering.
			entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
			entity.Property(e => e.SourceMessageId).HasMaxLength(RawNameLength);

			entity.HasIndex(e => e.SourceMessageId).IsUnique();

			// Both drive the GET /receipts list query: Status filters, PurchaseDate orders.
			entity.HasIndex(e => e.PurchaseDate);
			entity.HasIndex(e => e.Status);

			entity.HasOne(r => r.Merchant)
			      .WithMany()
			      .HasForeignKey(r => r.MerchantId)
			      .OnDelete(DeleteBehavior.Restrict);

			entity.HasMany(r => r.ReceiptLines)
			      .WithOne(rl => rl.Receipt)
			      .HasForeignKey(rl => rl.ReceiptId)
			      .OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<ReceiptLine>(entity => {
			entity.HasKey(e => e.Id);

			entity.Property(e => e.RawName).HasMaxLength(RawNameLength);
			entity.Property(e => e.Quantity).HasPrecision(QuantityPrecision, QuantityScale);
			entity.Property(e => e.UnitPrice).HasPrecision(MoneyPrecision, MoneyScale);
			entity.Property(e => e.LineTotal).HasPrecision(MoneyPrecision, MoneyScale);

			entity.HasOne(rl => rl.CanonicalItem)
			      .WithMany()
			      .HasForeignKey(rl => rl.CanonicalItemId)
			      .OnDelete(DeleteBehavior.SetNull);

			entity.HasOne(rl => rl.Category)
			      .WithMany()
			      .HasForeignKey(rl => rl.CategoryId)
			      .OnDelete(DeleteBehavior.SetNull);

			entity.HasOne(rl => rl.Meal)
			      .WithMany()
			      .HasForeignKey(rl => rl.MealId)
			      .OnDelete(DeleteBehavior.SetNull);
		});

		modelBuilder.Entity<CanonicalItem>(entity => {
			entity.HasKey(e => e.Id);

			// citext + unique: the same product cannot exist twice under different casing.
			entity.Property(e => e.DisplayName).HasColumnType("citext").HasMaxLength(NameLength);
			entity.HasIndex(e => e.DisplayName).IsUnique();

			entity.HasOne(ci => ci.Category)
			      .WithMany()
			      .HasForeignKey(ci => ci.CategoryId)
			      .OnDelete(DeleteBehavior.SetNull);

			entity.HasOne(ci => ci.DefaultMeal)
			      .WithMany()
			      .HasForeignKey(ci => ci.DefaultMealId)
			      .OnDelete(DeleteBehavior.SetNull);
		});

		modelBuilder.Entity<ItemAlias>(entity => {
			entity.HasKey(e => e.Id);

			// Unique: one alias per normalized raw name.
			entity.Property(e => e.RawName).HasColumnType("citext").HasMaxLength(RawNameLength);
			entity.HasIndex(e => e.RawName).IsUnique();

			entity.HasOne(ia => ia.CanonicalItem)
			      .WithMany()
			      .HasForeignKey(ia => ia.CanonicalItemId)
			      .OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<Category>(entity => {
			entity.HasKey(e => e.Id);
			entity.Property(e => e.DisplayName).HasColumnType("citext").HasMaxLength(NameLength);

			entity.HasOne(c => c.ParentCategory)
			      .WithMany(c => c.ChildCategories)
			      .HasForeignKey(c => c.ParentCategoryId)
			      .OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(c => new { c.ParentCategoryId, c.DisplayName })
			      .IsUnique()
			      .HasFilter("\"ParentCategoryId\" IS NOT NULL");

			entity.HasIndex(c => c.DisplayName)
			      .IsUnique()
			      .HasFilter("\"ParentCategoryId\" IS NULL");
		});

		modelBuilder.Entity<Meal>(entity => {
			entity.HasKey(e => e.Id);
			entity.Property(e => e.DisplayName).HasColumnType("citext").HasMaxLength(NameLength);
			entity.HasIndex(e => e.DisplayName).IsUnique();
		});

		modelBuilder.Entity<ReceiptMealCoverage>(entity => {
			entity.HasKey(e => new { e.ReceiptId, e.MealId });

			// The inverse navigation must be named, or EF maps a second relationship with a shadow key.
			entity.HasOne(e => e.Receipt)
			      .WithMany(r => r.MealCoverage)
			      .HasForeignKey(e => e.ReceiptId)
			      .OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.Meal)
			      .WithMany()
			      .HasForeignKey(e => e.MealId)
			      .OnDelete(DeleteBehavior.Restrict);

			entity.ToTable(table => table.HasCheckConstraint(
				"CK_ReceiptMealCoverages_DaysCovered",
				$"\"DaysCovered\" >= 0 AND \"DaysCovered\" <= {MaxDaysCovered}"));
		});

		modelBuilder.Entity<Merchant>(entity => {
			entity.HasKey(e => e.Id);

			// citext + unique: "Netto" and "NETTO" are the same shop.
			entity.Property(e => e.Name).HasColumnType("citext").HasMaxLength(NameLength);
			entity.HasIndex(e => e.Name).IsUnique();
		});
	}
}
