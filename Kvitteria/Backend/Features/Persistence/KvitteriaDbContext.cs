using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Persistence;

public sealed class KvitteriaDbContext(DbContextOptions<KvitteriaDbContext> options) : DbContext(options)
{
	public DbSet<Receipt> Receipts { get; set; }
	public DbSet<ReceiptLine> ReceiptLines { get; set; }
	public DbSet<CanonicalItem> CanonicalItems { get; set; }
	public DbSet<ItemAlias> ItemAliases { get; set; }
	public DbSet<CanonicalItemCategory> ItemCategories { get; set; }
	public DbSet<Category> Categories { get; set; }
	public DbSet<Merchant> Merchants { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Receipt>(entity => {
			entity.HasKey(e => new {e.Id});
		});
		
		modelBuilder.Entity<ReceiptLine>(entity => {
			entity.HasKey(e => new {e.Id});
		});
		
		modelBuilder.Entity<CanonicalItem>(entity => {
			entity.HasKey(e => new {e.Id});
		});
		
		modelBuilder.Entity<ItemAlias>(entity => {
			entity.HasKey(e => new {e.RawName});
		});
		
		modelBuilder.Entity<Category>(entity => {
			entity.HasKey(e => new {e.Id});
		});
		
		modelBuilder.Entity<CanonicalItemCategory>(entity => {
			entity.HasKey(e => new {e.CategoryId, e.CanonicalItemId});
		});
		
		modelBuilder.Entity<Merchant>(entity => {
			entity.HasKey(e => new {e.Id});
		});
	}
}