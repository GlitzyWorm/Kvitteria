namespace Backend.Features.Shared;

/// <summary>Turns a pipeline exception into the text stored on a failed mail.</summary>
public static class PipelineError
{
	private const int MaxLength = 1_000;

	/// <summary>
	/// Joins the messages along the <c>InnerException</c> chain. The extraction loop wraps the real
	/// cause ("Ollama refused the connection") in a generic "failed after N attempts", so the outer
	/// message alone would hide the one thing worth reading.
	/// </summary>
	public static string Describe(Exception exception)
	{
		var messages = new List<string>();

		for (Exception? current = exception; current is not null; current = current.InnerException)
		{
			string message = current.Message.Trim();

			if (message.Length > 0 && !messages.Contains(message))
				messages.Add(message);
		}

		string joined = messages.Count == 0 ? exception.GetType().Name : string.Join(" → ", messages);

		return joined.Length <= MaxLength ? joined : joined[..(MaxLength-1)]+"…";
	}
}
