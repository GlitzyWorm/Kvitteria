using Backend.Features.Shared;
namespace Backend.Tests;

public class PipelineErrorTests
{
	[Fact]
	public void A_single_exception_is_its_message() =>
		Assert.Equal("boom", PipelineError.Describe(new InvalidOperationException("boom")));

	[Fact]
	public void Inner_exceptions_follow_the_outer_message()
	{
		var ex = new InvalidOperationException("Failed after 3 attempt(s).", new HttpRequestException("Connection refused"));

		Assert.Equal("Failed after 3 attempt(s). → Connection refused", PipelineError.Describe(ex));
	}

	[Fact]
	public void Repeated_messages_appear_once()
	{
		var ex = new InvalidOperationException("same", new InvalidOperationException("same"));

		Assert.Equal("same", PipelineError.Describe(ex));
	}

	[Fact]
	public void An_empty_message_falls_back_to_the_type_name() =>
		Assert.Equal("ArgumentException", PipelineError.Describe(new ArgumentException("")));
}
