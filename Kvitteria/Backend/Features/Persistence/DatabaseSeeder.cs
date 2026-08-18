using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Persistence;

/// <summary>
/// Seeds the Danish meal and category taxonomy on first run. Invoked through
/// <c>UseSeeding</c>/<c>UseAsyncSeeding</c>, which EF fires from <c>Migrate()</c> — including the
/// <c>dotnet ef database update</c> path this project uses.
/// </summary>
// TODO: This could be part of an onboarding flow rather than fixed seed data.
internal static class DatabaseSeeder
{
	private static readonly (string Name, bool TracksDayCoverage)[] SeedMeals =
		[("Morgenmad", false), ("Frokost", false), ("Aftensmad", true), ("Snacks", false)];

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

	public static void Seed(DbContext context)
	{
		bool needsMeals = !context.Set<Meal>().Any();
		bool needsCategories = !context.Set<Category>().Any();

		if (!needsMeals && !needsCategories)
			return;

		// Both inserts commit together. Previously they were two independent SaveChanges calls, so a
		// crash between them left meals seeded and categories not — and the "already seeded" guard
		// then blocked the categories forever.
		using IDbContextTransaction transaction = context.Database.BeginTransaction();

		AddPending(context, needsMeals, needsCategories);

		context.SaveChanges();
		transaction.Commit();
	}

	public static async Task SeedAsync(DbContext context, CancellationToken ct)
	{
		bool needsMeals = !await context.Set<Meal>().AnyAsync(ct);
		bool needsCategories = !await context.Set<Category>().AnyAsync(ct);

		if (!needsMeals && !needsCategories)
			return;

		await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

		AddPending(context, needsMeals, needsCategories);

		await context.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);
	}

	private static void AddPending(DbContext context, bool needsMeals, bool needsCategories)
	{
		if (needsMeals)
			context.Set<Meal>().AddRange(BuildMeals());

		if (needsCategories)
			context.Set<Category>().AddRange(BuildCategories());
	}

	internal static IEnumerable<Meal> BuildMeals() =>
		SeedMeals.Select((meal, index) => new Meal {
			DisplayName = meal.Name,
			TracksDayCoverage = meal.TracksDayCoverage,
			SortOrder = index
		});

	internal static IEnumerable<Category> BuildCategories() =>
		SeedCategories.Select((category, index) => {
			var root = new Category {
				DisplayName = category.Name,
				SortOrder = index
			};

			for (var child = 0; child < category.Children.Length; child++)
			{
				root.ChildCategories.Add(new Category {
					DisplayName = category.Children[child],
					SortOrder = child
				});
			}

			return root;
		});
}
