using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OutboxDemo.Order.Api.Infrastructure.Data;

/// <summary>
/// EF Core context for the Order service. Owns both the business <c>orders</c> table
/// and the transactional <c>outbox_messages</c> table that Debezium tails.
/// </summary>
public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    /// <summary>Set of <see cref="PurchaseOrder"/> aggregates.</summary>
    internal DbSet<PurchaseOrder> Orders => Set<PurchaseOrder>();

    /// <summary>Set of pending outbox rows awaiting CDC capture.</summary>
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PurchaseOrderConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}

/// <summary>
/// EF Core mapping for <see cref="PurchaseOrder"/>. Column names are snake-cased
/// to match the SQL conventions used by the rest of the schema.
/// </summary>
internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.OrderDate).HasColumnName("order_date").IsRequired();
    }
}

/// <summary>
/// EF Core mapping for <see cref="OutboxMessage"/>. The <c>payload</c> column uses
/// PostgreSQL <c>jsonb</c> so that Debezium can deliver the body unchanged downstream.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(200).IsRequired();
        // jsonb keeps the payload queryable and avoids re-encoding through Debezium's JSON converter.
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DateOccurred).HasColumnName("date_occurred").IsRequired();

        // Lookups by aggregate are the most common access pattern (replay, debugging).
        builder.HasIndex(x => x.AggregateId).HasDatabaseName("ix_outbox_aggregate_id");
    }
}
