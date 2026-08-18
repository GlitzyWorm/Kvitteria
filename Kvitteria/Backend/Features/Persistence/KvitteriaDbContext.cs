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

	/// <summary>Money is kroner with øre; two decimals is the domain, not a guess.</summary>
	private const int MoneyPrecision = 12;
	private const int MoneyScale = 2;

	/// <summary>Quantities can be fractional (kg), but never need more than grams of resolution.</summary>
	private const int QuantityPrecision = 10;
	private const int QuantityScale = 3;

	private const int NameLength = 200;
	private const int RawNameLength = 300;

	/// <summary>Matches MinDaysCovered/MaxDaysCovered in ReceiptEndpoints.</summary>
	private const int MaxDaysCovered = 31;

	// Delete policy, stated once so it is a decision rather than an accident:
	//  - Lines and meal coverage are owned by their receipt      -> Cascade.
	//  - An alias is meaningless without its canonical item      -> Cascade.
	//  - Classification is optional metadata, never owning        -> SetNull.
	//  - Anything a row depends on for identity or history        -> Restrict.
	// Receipt -> Merchant is the important one: by convention it was Cascade, so deleting a
	// merchant silently deleted every receipt for that shop.
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.HasPostgresExtension("citext");

		modelBuilder.Entity<Receipt>(entity => {
			entity.HasKey(e => e.Id);

			entity.Property(e => e.GrandTotal).HasPrecision(MoneyPrecision, MoneyScale);
			entity.Property(e => e.PantTotal).HasPrecision(MoneyPrecision, MoneyScale);

			// Stored as text so a value in the database is self-describing and cannot drift out of
			// step with the enum's ordering.
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

			// citext + unique, matching Category.DisplayName, so the same product cannot be created
			// twice under different casing.
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

			// The alias upsert and GetCanonicalIdsByRawNames both already assumed RawName was unique;
			// without the index a duplicate made the dictionary build throw and killed the worker.
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

			// Naming the inverse navigation is what keeps EF from resolving Receipt.MealCoverage as a
			// second relationship with its own shadow foreign key.
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

			// citext + unique turns the check-then-insert race in merchant resolution into a conflict
			// the database rejects, and stops "Netto" and "NETTO" being two shops.
			entity.Property(e => e.Name).HasColumnType("citext").HasMaxLength(NameLength);
			entity.HasIndex(e => e.Name).IsUnique();
		});
	}
}
