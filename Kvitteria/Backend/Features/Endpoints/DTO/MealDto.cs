using Backend.Features.Persistence.Model.Receipts;
namespace Backend.Features.Endpoints.DTO;

public record MealDto(
	Guid Id,
	string DisplayName,
	int SortOrder,
	bool TracksDayCoverage
);

public static class MealDtoDtoExtension
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