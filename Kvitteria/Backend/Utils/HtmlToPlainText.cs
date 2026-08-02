//
// HtmlToText.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

// Obtained from https://gist.github.com/jstedfast/5c2189db1397aa776b3e

using MimeKit.Text;
namespace Backend.Utils
{
	/// <summary>
	/// An HTML to plain text converter.
	/// </summary>
	/// <remarks>
	/// Used to convert HTML into plain text.
	/// </remarks>
	public class HtmlToText : TextConverter
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HtmlToText"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new HTML to plain text converter.
		/// </remarks>
		public HtmlToText() { }

		/// <summary>
		/// Get the input format.
		/// </summary>
		/// <remarks>
		/// Gets the input format.
		/// </remarks>
		/// <value>The input format.</value>
		public override TextFormat InputFormat
		{
			get{ return TextFormat.Html; }
		}

		/// <summary>
		/// Get the output format.
		/// </summary>
		/// <remarks>
		/// Gets the output format.
		/// </remarks>
		/// <value>The output format.</value>
		public override TextFormat OutputFormat
		{
			get{ return TextFormat.Text; }
		}

		/// <summary>
		/// Get or set the text that will be appended to the end of the output.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the text that will be appended to the end of the output.</para>
		/// <para>The footer must be set before conversion begins.</para>
		/// </remarks>
		/// <value>The footer.</value>
		public string Footer
		{
			get;
			set;
		}

		/// <summary>
		/// Get or set the footer format.
		/// </summary>
		/// <remarks>
		/// Gets or sets the footer format.
		/// </remarks>
		/// <value>The footer format.</value>
		public HeaderFooterFormat FooterFormat
		{
			get;
			set;
		}

		/// <summary>
		/// Get or set text that will be prepended to the beginning of the output.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the text that will be prepended to the beginning of the output.</para>
		/// <para>The header must be set before conversion begins.</para>
		/// </remarks>
		/// <value>The header.</value>
		public string Header
		{
			get;
			set;
		}

		/// <summary>
		/// Get or set the header format.
		/// </summary>
		/// <remarks>
		/// Gets or sets the header format.
		/// </remarks>
		/// <value>The header format.</value>
		public HeaderFooterFormat HeaderFormat
		{
			get;
			set;
		}

		static void Push(ICollection<HtmlTagId> stack, HtmlTagId id)
		{
			if (id != HtmlTagId.Unknown)
				stack.Add(id);
		}

		static void Pop(IList<HtmlTagId> stack, HtmlTagId id)
		{
			if (id == HtmlTagId.Unknown)
				return;

			for (int i = stack.Count; i > 0; i--)
			{
				if (stack[i-1] == id)
				{
					stack.RemoveAt(i-1);
					return;
				}
			}
		}

		/// <summary>
		/// Convert the contents of <paramref name="reader"/> from the <see cref="InputFormat"/> to the
		/// <see cref="OutputFormat"/> and uses the <paramref name="writer"/> to write the resulting text.
		/// </summary>
		/// <remarks>
		/// Converts the contents of <paramref name="reader"/> from the <see cref="InputFormat"/> to the
		/// <see cref="OutputFormat"/> and uses the <paramref name="writer"/> to write the resulting text.
		/// </remarks>
		/// <param name="reader">The text reader.</param>
		/// <param name="writer">The text writer.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="reader"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="writer"/> is <c>null</c>.</para>
		/// </exception>
		public override void Convert(TextReader reader, TextWriter writer)
		{
			if (reader == null)
				throw new ArgumentNullException("reader");

			if (writer == null)
				throw new ArgumentNullException("writer");

			if (!string.IsNullOrEmpty(Header))
			{
				if (HeaderFormat == HeaderFooterFormat.Html)
				{
					var converter = new HtmlToText();

					using (var sr = new StringReader(Header))
						converter.Convert(sr, writer);
				}
				else
				{
					writer.Write(Header);
				}
			}

			var tokenizer = new HtmlTokenizer(reader);
			var stack = new List<HtmlTagId>();
			HtmlToken token;

			while (tokenizer.ReadNextToken(out token))
			{
				switch (token.Kind)
				{
					case HtmlTokenKind.Tag:
						var tag = (HtmlTagToken)token;

						if (tag.IsEmptyElement || tag.Id.IsEmptyElement())
						{
							if (tag.Id == HtmlTagId.Br || tag.Id == HtmlTagId.P)
								writer.WriteLine();
						}
						else if (tag.IsEndTag)
						{
							if (tag.Id == HtmlTagId.P)
								writer.WriteLine();

							Pop(stack, tag.Id);
						}
						else
						{
							if (tag.Id == HtmlTagId.P)
								writer.WriteLine();

							Push(stack, tag.Id);
						}
						break;
					case HtmlTokenKind.Data:
						var data = (HtmlDataToken)token;

						if (stack.Count == 0)
							break;

						switch (stack[stack.Count-1])
						{
							case HtmlTagId.Head:
							case HtmlTagId.Title:
							case HtmlTagId.Meta:
							case HtmlTagId.Table:
							case HtmlTagId.TR:
							case HtmlTagId.Style:
							case HtmlTagId.Script:
								// ignore
								break;
							default:
								writer.Write(data.Data);
								break;
						}
						break;
				}
			}

			if (!string.IsNullOrEmpty(Footer))
			{
				if (FooterFormat == HeaderFooterFormat.Html)
				{
					var converter = new HtmlToText();

					using (var sr = new StringReader(Footer))
						converter.Convert(sr, writer);
				}
				else
				{
					writer.Write(Footer);
				}
			}
		}
	}
}