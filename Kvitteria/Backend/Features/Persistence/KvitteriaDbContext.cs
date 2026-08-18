using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Persistence;

public sealed class KvitteriaDbContext(DbContextOptions<KvitteriaDbContext> options) : DbContext(options)
{
	public DbSet<Receipt> Receipts { get; set; }
	public DbSet<ReceiptLine> ReceiptLines { get; set; }
	public DbSet<CanonicalItem> CanonicalItems { get; set; }
	public DbSet<ItemAlias> ItemAliases { get; set; }
	public DbSet<Meal> Meals { get; set; }
	public DbSet<ReceiptMealCoverage> ReceiptMealCoverages { get; set; }
	public DbSet<Category> Categories { get; set; }
	public DbSet<Merchant> Merchants { get; set; }

	private static readonly (string Name, bool TracksDayCoverage)[] SeedMeals = [("Morgenmad", false), ("Frokost", false), ("Aftensmad", true), ("Snacks", false)];

	private static readonly (string Name, string[] Children)[] SeedCategories = [
		("Fødevarer", ["Frugt & Grønt", "Kød & Fisk", "Mejeri & Æg", "Brød & Bageri", "Kolonial", "Frost", "Færdigretter", "Slik & Snacks"]),
		("Drikkevarer", ["Vand & Sodavand", "Juice & Saft", "Kaffe & Te", "Øl & Vin", "Spiritus"]),
		("Husholdning", ["Rengøring", "Vask", "Papirvarer", "Poser & Folie", "Batterier & El"]),
		("Personlig Pleje", ["Hår", "Hud & Krop", "Tandpleje", "Barbering", "Hygiejne", "Kosmetik"]),
		("Sundhed", ["Medicin", "Vitaminer & Kosttilskud", "Førstehjælp"]),
		("Køkken", ["Køkkenredskaber", "Gryder & Pander", "Servise & Glas", "Opbevaring"]),
		("Tøj & Tekstiler", ["Tøj", "Sko", "Sengetøj & Håndklæder", "Tilbehør"]),
		("Dyr", ["Dyrefoder", "Dyretilbehør"]),
		("Fritid & Fornøjelser", ["Bøger & Blade", "Legetøj & Spil", "Sport & Udendørs", "Elektronik", "Blomster & Planter"]),
		("Andet", [])
	];

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.HasPostgresExtension("citext");

		modelBuilder.Entity<Receipt>(entity => {
			entity.HasKey(e => new { e.Id });
		});

		modelBuilder.Entity<ReceiptLine>(entity => {
			entity.HasKey(e => new { e.Id });

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
			entity.HasKey(e => new { e.Id });

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
			entity.HasKey(e => new { e.Id });
		});

		modelBuilder.Entity<Category>(entity => {
			entity.HasKey(e => new { e.Id });
			entity.Property(e => e.DisplayName).HasColumnType("citext");

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
			entity.HasIndex(e => e.DisplayName).IsUnique();
		});

		modelBuilder.Entity<ReceiptMealCoverage>(entity => {
			entity.HasKey(e => new { e.ReceiptId, e.MealId });

			entity.HasOne(e => e.Receipt)
			      .WithMany()
			      .HasForeignKey(e => e.ReceiptId)
			      .OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.Meal)
			      .WithMany()
			      .HasForeignKey(e => e.MealId)
			      .OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<Merchant>(entity => {
			entity.HasKey(e => new { e.Id });
		});
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		base.OnConfiguring(optionsBuilder);

		// TODO: This could be part of onboarding flow
		optionsBuilder.UseSeeding((context, _) => {
			if (!context.Set<Meal>().Any())
			{
				for (var i = 0; i < SeedMeals.Length; i++)
				{
					context.Set<Meal>().Add(new Meal {
						DisplayName = SeedMeals[i].Name,
						TracksDayCoverage = SeedMeals[i].TracksDayCoverage,
						SortOrder = i
					});
				}

				context.SaveChanges();
			}

			if (context.Set<Category>().Any()) return;

			for (var i = 0; i < SeedCategories.Length; i++)
			{
				(string name, string[] children) = SeedCategories[i];

				var root = new Category {
					DisplayName = name,
					SortOrder = i
				};

				for (var j = 0; j < children.Length; j++)
				{
					root.ChildCategories.Add(new Category {
						DisplayName = children[j],
						SortOrder = j
					});
				}

				context.Set<Category>().Add(root);
			}

			context.SaveChanges();
		});
	}
}