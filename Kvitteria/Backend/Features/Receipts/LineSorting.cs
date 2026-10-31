using Kvitteria.Contracts.DTO;
namespace Backend.Features.Receipts;

/// <summary>The checks on a sort request that need no database.</summary>
public static class LineSorting
{
	public const int MaxItemNameLength = 200;

	public static bool TryValidate(SortReceiptLineDto dto, out IResult problem)
	{
		bool hasNewName = dto.NewItemName != null;

		if (dto.CanonicalItemId == null && !hasNewName)
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: "Name either an existing canonical item or a new item's name.");
			return false;
		}

		if (dto.CanonicalItemId != null && hasNewName)
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: "Name either an existing canonical item or a new item's name, not both.");
			return false;
		}

		if (hasNewName && (string.IsNullOrWhiteSpace(dto.NewItemName) || dto.NewItemName.Trim().Length > MaxItemNameLength))
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: $"NewItemName must not be blank and at most {MaxItemNameLength} characters.");
			return false;
		}

		problem = Results.Empty;
		return true;
	}
}
