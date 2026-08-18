using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class MealEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapMealEndpoints()
		{
			RouteGroupBuilder mealGroup = app.MapGroup("/meals");

			// GET /meals
			mealGroup.MapGet("/", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				         List<Meal> meals = await dbContext.Meals
				                                           .AsNoTracking()
				                                           .OrderBy(m => m.SortOrder)
				                                           .ThenBy(m => m.DisplayName)
				                                           .ToListAsync(ct);

				         return Results.Ok(meals.Select(m => m.ToDto()).ToList());
			         })
			         .WithName("GetMeals")
			         .Produces<List<MealDto>>();

			// GET /meals/id
			mealGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				         Meal? meal = await dbContext.Meals
				                                     .AsNoTracking()
				                                     .FirstOrDefaultAsync(m => m.Id == id, ct);

				         return meal != null
				         ? Results.Ok(meal.ToDto())
				         : Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Meal with id {id} not found");
			         })
			         .WithName("GetMealById")
			         .Produces<MealDto>()
			         .ProducesProblem(StatusCodes.Status404NotFound);

			// PUT /meals/order
			mealGroup.MapPut("/order", async (PutMealOrderDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				         if (dto.OrderedIds == null || dto.OrderedIds.Count == 0)
					         return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"OrderedIds is required and must not be empty.");

				         List<Meal> meals = await dbContext.Meals.ToListAsync(ct);

				         if (meals.Count != dto.OrderedIds.Count || !meals.All(m => dto.OrderedIds.Contains(m.Id)))
					         return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"OrderedIds must contain all meal ids");

				         Dictionary<Guid, Meal> mealsById = meals.ToDictionary(m => m.Id);

				         for (var i = 0; i < dto.OrderedIds.Count; i++)
					         mealsById[dto.OrderedIds[i]].SortOrder = i;

				         await dbContext.SaveChangesAsync(ct);

				         return Results.Ok(meals.Select(m => m.ToDto()).OrderBy(m => m.SortOrder).ToList());
			         })
			         .WithName("ReorderMeals")
			         .Produces<List<MealDto>>()
			         .ProducesProblem(StatusCodes.Status400BadRequest);
		}
	}
}