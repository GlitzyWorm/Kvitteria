using Backend.Features.Extraction;
using Backend.Features.Extraction.LLM;
namespace Backend.Tests;

public class ReceiptRowsTests
{
	private const string Product = ReceiptRows.Product;
	private const string Discount = ReceiptRows.Discount;
	private const string Deposit = ReceiptRows.Deposit;

	private static ReceiptRow Row(string text, string amount, string kind, decimal quantity = 1) =>
		new(text, amount, quantity, kind);

	[Fact]
	public void Photo_with_discounts_and_a_deposit_reconciles()
	{
		const string text = "Netto\n26 10 26 13:07\nMÆLK 30,00\nRABAT 5,00-";
		ReceiptRowsAnswer answer = new("Netto Scan&Go", "DKK",
		[
			Row("MÆLK", "30,00", Product),
			Row("RABAT", "5,00-", Discount),
			Row("BRØD", "20,00", Product),
			Row("RABAT", "2,00", Discount),   // printed without a sign: still negative
			Row("KAFFE", "40,00", Product),
			Row("RABAT", "15,00-", Discount),
			Row("PANT", "1,50", Deposit),
			Row("TOTAL", "69,50", "total"),
			Row("DANKORT", "69,50", "payment")
		], "69,50");

		ExtractedReceipt result = ReceiptRows.ToExtracted(answer, text);

		Assert.Equal("Netto", result.Merchant);
		Assert.Equal("2026-10-26 13:07:00", result.PurchaseDate);
		Assert.Equal(6, result.LineItems.Count);
		Assert.Equal(-2.00m, result.LineItems[3].LineTotal);
		Assert.Equal(1.50m, result.PantTotal);
		Assert.Equal(69.50m, result.GrandTotal);
		Assert.Equal(68.00m, result.LineItems.Sum(l => l.LineTotal));
	}

	[Fact]
	public void A_negative_paid_amount_is_taken_as_its_absolute_value()
	{
		ReceiptRowsAnswer answer = new("SuperBrugsen", "DKK",
		[
			Row("SMØR", "58,95", Product),
			Row("OST", "50,00", Product),
			Row("I alt", "-108,95", "total"),
			Row("DANKORT", "-108,95", "payment")
		], "-108,95");

		ExtractedReceipt result = ReceiptRows.ToExtracted(answer, "Super Brugsen\n...");

		Assert.Equal("SuperBrugsen", result.Merchant);
		Assert.Equal(108.95m, result.GrandTotal);
		Assert.Equal(2, result.LineItems.Count);
		Assert.Equal("", result.PurchaseDate);
	}

	[Fact]
	public void The_mail_totals_block_gives_the_pant_from_deposit_total()
	{
		ReceiptRowsAnswer answer = new("Netto Scan&Go", "kr",
		[
			Row("KYLLING", "246,44", Product),
			Row("HERAF PANT IALT", "0,00", ReceiptRows.DepositTotal),
			Row("HERAF MOMS", "61,61", "vat"),
			Row("I ALT", "308,05", "total")
		], "308,05");

		ExtractedReceipt result = ReceiptRows.ToExtracted(answer, "Netto Scan&Go");

		Assert.Equal(0m, result.PantTotal);
		Assert.Equal(308.05m, result.GrandTotal);
		Assert.Single(result.LineItems);
	}

	[Fact]
	public void Pant_is_counted_once_when_lines_and_a_summary_are_both_printed()
	{
		ReceiptRowsAnswer answer = new("Netto", "DKK",
		[
			Row("COLA", "20,00", Product),
			Row("PANT", "3,00", Deposit),
			Row("PANT", "3,00", Deposit),
			Row("HERAF PANT IALT", "6,00", ReceiptRows.DepositTotal)
		], "26,00");

		Assert.Equal(6.00m, ReceiptRows.ToExtracted(answer, "Netto").PantTotal);
	}

	[Fact]
	public void Quantity_divides_the_total_into_a_unit_price()
	{
		ReceiptRowsAnswer answer = new("Netto", "DKK", [Row("BACON", "24,00", Product, 2), Row("X", "5,00", Product, 0)], "29,00");

		ExtractedReceipt result = ReceiptRows.ToExtracted(answer, "Netto");

		Assert.Equal(12.00m, result.LineItems[0].UnitPrice);
		Assert.Equal(24.00m, result.LineItems[0].LineTotal);
		Assert.Equal(1m, result.LineItems[1].Quantity);
	}

	[Fact]
	public void A_quantity_row_with_a_discount_amount_is_unusable()
	{
		ReceiptRowsAnswer answer = new("Netto", "DKK", [Row("2 x 12,00", "5,00-", Discount)], "19,00");

		Assert.Throws<OllamaResponseException>(() => ReceiptRows.ToExtracted(answer, "Netto"));
	}

	[Theory]
	[InlineData("MÆLK", "2 x 10,05", "product")]
	[InlineData("MÆLK", "", "product")]
	[InlineData("MÆLK", "12,00", "snack")]
	public void Unreadable_amounts_and_unknown_kinds_are_unusable(string text, string amount, string kind)
	{
		ReceiptRowsAnswer answer = new("Netto", "DKK", [Row(text, amount, kind)], "12,00");

		Assert.Throws<OllamaResponseException>(() => ReceiptRows.ToExtracted(answer, "Netto"));
	}

	[Fact]
	public void An_unknown_shop_keeps_the_models_name()
	{
		ReceiptRowsAnswer answer = new(" Brugsen i Byen ", "", [], "0,00");

		ExtractedReceipt result = ReceiptRows.ToExtracted(answer, "Min lokale købmand");

		Assert.Equal("Brugsen i Byen", result.Merchant);
		Assert.Equal("DKK", result.Currency);
	}
}
