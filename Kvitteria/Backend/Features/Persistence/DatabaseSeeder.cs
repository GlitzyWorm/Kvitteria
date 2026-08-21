using Backend.Features.Auth;
using Backend.Features.Persistence.Model;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Persistence.Model.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Persistence;

/// <summary>
/// Seeds the Danish meal and category taxonomy, and the household settings row, when the tables are
/// empty. Invoked through <c>UseSeeding</c>/<c>UseAsyncSeeding</c>, which EF fires from <c>Migrate()</c>.
/// </summary>
// TODO: This could be part of an onboarding flow rather than fixed seed data.
internal static class DatabaseSeeder
{
	private static readonly (string Name, bool TracksDayCoverage)[] SeedMeals =
		[("Morgenmad", false), ("Frokost", false), ("Aftensmad", true), ("Snacks", false)];

	/// <summary>
	/// What members may do before an admin decides otherwise. Sorting new items is the whole point of
	/// having more than one account; changing the taxonomy is not.
	/// </summary>
	private static readonly Permission[] DefaultMemberPermissions = [Permission.CategorizeLines];

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
		bool needsHouseholdSettings = !context.Set<HouseholdSettings>().Any();
		bool needsPipelineSettings = !context.Set<PipelineSettings>().Any();

		if (!needsMeals && !needsCategories && !needsHouseholdSettings && !needsPipelineSettings)
			return;

		using IDbContextTransaction transaction = context.Database.BeginTransaction();

		AddPending(context, needsMeals, needsCategories, needsHouseholdSettings, needsPipelineSettings);

		context.SaveChanges();
		transaction.Commit();
	}

	public static async Task SeedAsync(DbContext context, CancellationToken ct)
	{
		bool needsMeals = !await context.Set<Meal>().AnyAsync(ct);
		bool needsCategories = !await context.Set<Category>().AnyAsync(ct);
		bool needsHouseholdSettings = !await context.Set<HouseholdSettings>().AnyAsync(ct);
		bool needsPipelineSettings = !await context.Set<PipelineSettings>().AnyAsync(ct);

		if (!needsMeals && !needsCategories && !needsHouseholdSettings && !needsPipelineSettings)
			return;

		await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

		AddPending(context, needsMeals, needsCategories, needsHouseholdSettings, needsPipelineSettings);

		await context.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);
	}

	private static void AddPending(
		DbContext context, bool needsMeals, bool needsCategories, bool needsHouseholdSettings, bool needsPipelineSettings)
	{
		if (needsMeals)
			context.Set<Meal>().AddRange(BuildMeals());

		if (needsCategories)
			context.Set<Category>().AddRange(BuildCategories());

		if (needsHouseholdSettings)
			context.Set<HouseholdSettings>().Add(new HouseholdSettings {
				MemberPermissions = [.. DefaultMemberPermissions]
			});

		if (needsPipelineSettings)
			context.Set<PipelineSettings>().Add(new PipelineSettings());
	}

	private static IEnumerable<Meal> BuildMeals() =>
		SeedMeals.Select((meal, index) => new Meal {
			DisplayName = meal.Name,
			TracksDayCoverage = meal.TracksDayCoverage,
			SortOrder = index
		});

	private static IEnumerable<Category> BuildCategories() =>
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
