namespace Backend.Features.Uploads;

/// <summary>
/// The fixed budget an uploaded receipt image must fit, the same numbers the app scales to. At about
/// 1.8 MP the home LXC ran out of memory with the model loaded; 0.79 MP read every character, and 1 MP
/// leaves room for a tall screenshot to keep a readable width.
/// </summary>
public static class ReceiptImageLimits
{
	public const int MaxPixels = 1_000_000;
	public const int MaxBytes = 2*1024*1024;

	/// <summary>Kestrel's cut-off for the whole multipart body: the image plus a little for the form around it.</summary>
	public const long MaxRequestBytes = MaxBytes+64*1024;

	/// <summary>
	/// Checks an upload against the budget without decoding it, yielding its dimensions or a 400 problem
	/// naming the limit it broke.
	/// </summary>
	public static bool TryValidate(ReadOnlySpan<byte> data, out int width, out int height, out IResult problem)
	{
		problem = Results.Empty;

		if (data.Length > MaxBytes)
		{
			width = height = 0;
			problem = Problem($"The image is {data.Length} bytes; at most {MaxBytes} bytes are accepted.");
			return false;
		}

		if (!JpegHeader.TryRead(data, out width, out height))
		{
			problem = Problem("The image is not a complete JPEG file.");
			return false;
		}

		if ((long)width*height > MaxPixels)
		{
			problem = Problem($"The image is {width}×{height} pixels; at most {MaxPixels} pixels are accepted. Scale it down and try again.");
			return false;
		}

		return true;
	}

	private static IResult Problem(string detail) =>
		Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: detail);
}
