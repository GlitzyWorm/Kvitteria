using System.Runtime.CompilerServices;

// The mail body converter is internal to the app but is worth testing directly: silent corruption
// there degrades extraction accuracy without surfacing an error anywhere.
[assembly: InternalsVisibleTo("Backend.Tests")]
