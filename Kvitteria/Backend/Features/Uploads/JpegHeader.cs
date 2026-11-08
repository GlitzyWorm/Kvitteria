namespace Backend.Features.Uploads;

/// <summary>
/// Reads a JPEG's dimensions from its frame header without decoding a pixel. Enough to refuse an
/// oversize or truncated upload on a machine whose memory is already spoken for by the model.
/// </summary>
public static class JpegHeader
{
	private const byte Marker = 0xFF;
	private const byte StartOfImage = 0xD8;
	private const byte EndOfImage = 0xD9;
	private const byte StartOfScan = 0xDA;
	private const byte Temporary = 0x01;
	private const byte FirstRestart = 0xD0;
	private const byte LastRestart = 0xD7;

	// SOF0–SOF15 share C0–CF with three markers that are not frames.
	private const byte FirstFrame = 0xC0;
	private const byte LastFrame = 0xCF;
	private const byte DefineHuffmanTable = 0xC4;
	private const byte JpegExtension = 0xC8;
	private const byte DefineArithmeticConditioning = 0xCC;

	/// <summary>
	/// True when <paramref name="data"/> starts with SOI, ends with EOI, and has a frame header before
	/// the first scan; the frame's dimensions are returned. Anything else, including a PNG, an empty
	/// body or a file cut off in transit, is false.
	/// </summary>
	public static bool TryRead(ReadOnlySpan<byte> data, out int width, out int height)
	{
		width = 0;
		height = 0;

		if (data.Length < 4 || data[0] != Marker || data[1] != StartOfImage)
			return false;

		if (data[^2] != Marker || data[^1] != EndOfImage)
			return false;

		int position = 2;

		while (position < data.Length)
		{
			if (data[position] != Marker)
				return false;

			// Any number of fill bytes may precede a marker.
			while (position < data.Length && data[position] == Marker)
				position++;

			if (position >= data.Length)
				return false;

			byte marker = data[position++];

			// Standalone markers carry no length.
			if (marker == Temporary || marker is >= FirstRestart and <= LastRestart)
				continue;

			// A scan or the end before any frame header means there is no frame to read.
			if (marker is StartOfScan or EndOfImage or StartOfImage)
				return false;

			if (position+2 > data.Length)
				return false;

			int length = (data[position] << 8) | data[position+1];

			if (length < 2 || position+length > data.Length)
				return false;

			if (IsFrame(marker))
			{
				// Length (2), precision (1), height (2), width (2).
				if (length < 7)
					return false;

				height = (data[position+3] << 8) | data[position+4];
				width = (data[position+5] << 8) | data[position+6];

				return width > 0 && height > 0;
			}

			position += length;
		}

		return false;
	}

	private static bool IsFrame(byte marker) =>
		marker is >= FirstFrame and <= LastFrame
		&& marker != DefineHuffmanTable
		&& marker != JpegExtension
		&& marker != DefineArithmeticConditioning;
}
