using Backend.Features.Uploads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
namespace Backend.Tests;

public class JpegHeaderTests
{
	// 7×5 baseline and 9×4 progressive JPEGs, and a 3×3 PNG, generated with Pillow.
	private static readonly byte[] Baseline = Convert.FromBase64String(
		"/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDABALDA4MChAODQ4SERATGCgaGBYWGDEjJR0oOjM9PDkzODdASFxOQERXRTc4UG1RV19iZ2hnPk1xeXBkeFxlZ2P/2wBDARESEhgVGC8aGi9jQjhCY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2P/wAARCAAFAAcDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDHooorhPqD/9k=");

	private static readonly byte[] Progressive = Convert.FromBase64String(
		"/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDABALDA4MChAODQ4SERATGCgaGBYWGDEjJR0oOjM9PDkzODdASFxOQERXRTc4UG1RV19iZ2hnPk1xeXBkeFxlZ2P/2wBDARESEhgVGC8aGi9jQjhCY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2P/wgARCAAEAAkDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAAAP/EABUBAQEAAAAAAAAAAAAAAAAAAAQF/9oADAMBAAIQAxAAAAEBTv/EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAQUCP//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Bf//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Bf//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEABj8CP//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8hP//aAAwDAQACAAMAAAAQ9//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Qf//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Qf//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8QP//Z");

	private static readonly byte[] Png = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAMAAAADCAIAAADZSiLoAAAAC0lEQVR4nGNgwAcAAB4AAfb96ZYAAAAASUVORK5CYII=");

	[Fact]
	public void Reads_the_dimensions_of_a_baseline_jpeg()
	{
		Assert.True(JpegHeader.TryRead(Baseline, out int width, out int height));
		Assert.Equal(7, width);
		Assert.Equal(5, height);
	}

	[Fact]
	public void Reads_the_dimensions_of_a_progressive_jpeg()
	{
		Assert.True(JpegHeader.TryRead(Progressive, out int width, out int height));
		Assert.Equal(9, width);
		Assert.Equal(4, height);
	}

	[Fact]
	public void Rejects_a_truncated_file() =>
		Assert.False(JpegHeader.TryRead(Baseline.AsSpan(0, Baseline.Length-10), out _, out _));

	[Fact]
	public void Rejects_a_file_cut_off_inside_its_headers() =>
		Assert.False(JpegHeader.TryRead([..Baseline.AsSpan(0, 100), 0xFF, 0xD9], out _, out _));

	[Fact]
	public void Rejects_a_png() =>
		Assert.False(JpegHeader.TryRead(Png, out _, out _));

	[Fact]
	public void Rejects_an_empty_body() =>
		Assert.False(JpegHeader.TryRead([], out _, out _));

	[Fact]
	public void Accepts_an_image_exactly_at_the_pixel_budget() =>
		Assert.True(ReceiptImageLimits.TryValidate(WithSize(1000, 1000), out _, out _, out _));

	[Fact]
	public void Rejects_an_image_one_pixel_over_the_budget()
	{
		// 101 × 9901 = 1 000 001.
		Assert.False(ReceiptImageLimits.TryValidate(WithSize(101, 9901), out _, out _, out IResult problem));
		Assert.Contains(ReceiptImageLimits.MaxPixels.ToString(), Detail(problem));
	}

	[Fact]
	public void Rejects_the_1536_by_2048_image_the_spec_names() =>
		Assert.False(ReceiptImageLimits.TryValidate(WithSize(1536, 2048), out _, out _, out _));

	[Fact]
	public void Accepts_the_864_by_1152_image_the_spec_names() =>
		Assert.True(ReceiptImageLimits.TryValidate(WithSize(864, 1152), out _, out _, out _));

	[Fact]
	public void Rejects_an_oversize_body_before_reading_it()
	{
		byte[] large = new byte[ReceiptImageLimits.MaxBytes+1];

		Assert.False(ReceiptImageLimits.TryValidate(large, out _, out _, out IResult problem));
		Assert.Contains(ReceiptImageLimits.MaxBytes.ToString(), Detail(problem));
	}

	/// <summary>The baseline JPEG with its frame header rewritten to claim other dimensions.</summary>
	private static byte[] WithSize(int width, int height)
	{
		byte[] copy = [..Baseline];
		int sof = Enumerable.Range(2, copy.Length-3).First(i => copy[i] == 0xFF && copy[i+1] == 0xC0);

		copy[sof+5] = (byte)(height >> 8);
		copy[sof+6] = (byte)height;
		copy[sof+7] = (byte)(width >> 8);
		copy[sof+8] = (byte)width;

		return copy;
	}

	private static string Detail(IResult problem) =>
		Assert.IsType<ProblemHttpResult>(problem).ProblemDetails.Detail ?? "";
}
