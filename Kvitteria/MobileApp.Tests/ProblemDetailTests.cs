// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Shared;

namespace MobileApp.Tests;

/// <summary>
/// Every mailbox failure the user reads comes through here, so a silent null would present as a blank
/// error callout — the worst possible outcome on an error path.
/// </summary>
public class ProblemDetailTests
{
	/// <summary>
	/// The shape the backend actually sends: AddProblemDetails plus Results.Problem(detail:), so the
	/// human sentence is in detail and nowhere else.
	/// </summary>
	[Fact]
	public void Reads_the_detail_of_a_real_problem_body()
	{
		const string body = """
		                    {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3",
		                     "title":"Bad Gateway","status":502,
		                     "detail":"IMAP folder 'INBOX/Bon' was not found. Available folders: INBOX, INBOX/Kvitteringer"}
		                    """;

		Assert.Equal(
			"IMAP folder 'INBOX/Bon' was not found. Available folders: INBOX, INBOX/Kvitteringer",
			ProblemDetail.Read(body));
	}

	[Fact]
	public void Returns_null_when_there_is_no_detail()
	{
		Assert.Null(ProblemDetail.Read("""{"title":"Forbidden","status":403}"""));
	}

	[Theory]
	[InlineData("""{"detail":""}""")]
	[InlineData("""{"detail":"   "}""")]
	public void Treats_an_empty_detail_as_absent(string body)
	{
		Assert.Null(ProblemDetail.Read(body));
	}

	/// <summary>A detail that is not a string is not a message, whatever it is.</summary>
	[Theory]
	[InlineData("""{"detail":42}""")]
	[InlineData("""{"detail":null}""")]
	[InlineData("""{"detail":{"nested":"no"}}""")]
	public void Ignores_a_detail_that_is_not_a_string(string body)
	{
		Assert.Null(ProblemDetail.Read(body));
	}

	/// <summary>
	/// A proxy answering with HTML, or a truncated body. Must not throw: this runs while already
	/// handling a failure.
	/// </summary>
	[Theory]
	[InlineData("<html><body>502 Bad Gateway</body></html>")]
	[InlineData("{\"detail\":")]
	[InlineData("not json at all")]
	[InlineData("[]")]
	[InlineData("\"just a string\"")]
	public void Returns_null_rather_than_throwing_on_a_body_that_is_not_a_problem(string body)
	{
		Assert.Null(ProblemDetail.Read(body));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Returns_null_for_an_empty_body(string? body)
	{
		Assert.Null(ProblemDetail.Read(body));
	}
}
