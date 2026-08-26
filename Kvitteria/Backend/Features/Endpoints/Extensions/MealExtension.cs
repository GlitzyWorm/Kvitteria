using Backend.Features.Persistence.Model.Receipts;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class MealDtoExtension
{
	public static MealDto ToDto(this Meal meal)
	{
		return new MealDto(
			meal.Id,
			meal.DisplayName,
			meal.SortOrder,
			meal.TracksDayCoverage
		);
	}
}