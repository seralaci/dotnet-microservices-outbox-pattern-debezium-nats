using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using OutboxDemo.Order.Api.Contracts;

namespace OutboxDemo.Order.Api.Endpoints;

/// <summary>
/// HTTP endpoints exposed by the Order service. The endpoints demonstrate two
/// outbox publishing strategies — the canonical "insert and forget" pattern and
/// a "no additional space" variant that immediately deletes the outbox row.
/// </summary>
internal static class OrderEndpoints
{
    /// <summary>
    /// Maps the <c>/orders</c> endpoint group onto the supplied route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", CreateOrder);
        group.MapPost("/no-additional-space", CreateOrderWithoutAdditionalSpace);
        group.MapGet("/{id:guid}", GetOrder);

        return app;
    }

    /// <summary>
    /// Canonical outbox handler: persists the order and the outbox row in a single
    /// EF Core transaction. Debezium will read the row from the WAL afterwards.
    /// </summary>
    private static async Task<Created<PurchaseOrder>> CreateOrder(
        CreateOrderRequest request,
        OrderDbContext db,
        CancellationToken cancellationToken)
    {
        var order = PurchaseOrder.Create(request.FirstName, request.LastName, request.Email);

        // Both inserts share the same SaveChangesAsync call → they commit (or rollback) atomically.
        db.Orders.Add(order);
        db.OutboxMessages.Add(BuildOutboxMessage(order));

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/orders/{order.Id}", order);
    }

    /// <summary>
    /// Alternative handler that inserts the outbox row and deletes it again inside
    /// the same transaction.
    /// </summary>
    /// <remarks>
    /// The row never becomes visible to other readers, yet it is still emitted through
    /// the WAL — so Debezium captures the event without leaving any garbage behind.
    /// This trades disk/replication noise for not having to maintain an outbox cleanup job.
    /// </remarks>
    private static async Task<Results<Created<PurchaseOrder>, ProblemHttpResult>> CreateOrderWithoutAdditionalSpace(
        CreateOrderRequest request,
        OrderDbContext db,
        CancellationToken cancellationToken)
    {
        var order = PurchaseOrder.Create(request.FirstName, request.LastName, request.Email);

        // Use the provider's execution strategy so transient PostgreSQL failures are retried as a unit.
        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                db.Orders.Add(order);

                // First SaveChanges materializes the outbox row in the WAL (so Debezium sees it),
                // then the second SaveChanges deletes it — both within one transaction.
                var outboxMessage = BuildOutboxMessage(order);
                db.OutboxMessages.Add(outboxMessage);

                await db.SaveChangesAsync(cancellationToken);

                db.OutboxMessages.Remove(outboxMessage);
                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            });

            return TypedResults.Created($"/orders/{order.Id}", order);
        }
        catch (Exception exc)
        {
            // Surfacing the exception message as a ProblemDetails keeps the demo simple;
            // production code should sanitize this and map well-known failures explicitly.
            return TypedResults.Problem(exc.Message);
        }
    }

    /// <summary>
    /// Returns the order with the requested identifier, or <c>404 Not Found</c> if missing.
    /// </summary>
    private static async Task<Results<Ok<PurchaseOrder>, NotFound>> GetOrder(
        Guid id,
        OrderDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders.FindAsync([id], cancellationToken);

        return order is not null
            ? TypedResults.Ok(order)
            : TypedResults.NotFound();
    }

    /// <summary>
    /// Builds the outbox row corresponding to an order using the schema expected
    /// by the Debezium <c>outbox</c> SMT and the catalog in <see cref="NatsSubjects"/>.
    /// </summary>
    private static OutboxMessage BuildOutboxMessage(PurchaseOrder order)
    {
        var payload = JsonSerializer.Serialize(new OrderCreatedEvent(
            order.Id,
            order.FirstName,
            order.LastName,
            order.Email,
            order.OrderDate));

        return OutboxMessage.Create(
            aggregateType: NatsSubjects.Order.AggregateType,
            aggregateId: order.Id,
            type: EventTypes.OrderCreated,
            payload: payload);
    }
}
