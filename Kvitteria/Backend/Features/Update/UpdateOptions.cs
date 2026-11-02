using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Update;

public sealed class UpdateOptions: IOptionsSection
{
	public static string SectionName => "Update";

	/// <summary>False means the backend never contacts GitHub, and reports no update.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// <c>owner/name</c>. Has a default, like <c>Auth:Audience</c>, because it is this project's identity
	/// rather than a deployment address; a fork names itself.
	/// </summary>
	[Required(AllowEmptyStrings = false)]
	[RegularExpression("^[^/\\s]+/[^/\\s]+$")]
	public string Repository { get; set; } = "GlitzyWorm/Kvitteria";
}
