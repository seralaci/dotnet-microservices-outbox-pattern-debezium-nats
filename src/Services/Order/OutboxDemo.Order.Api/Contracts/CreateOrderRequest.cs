namespace OutboxDemo.Order.Api.Contracts;

/// <summary>
/// Request body accepted by the <c>POST /orders</c> endpoint.
/// </summary>
/// <param name="FirstName">First name of the customer placing the order.</param>
/// <param name="LastName">Last name of the customer placing the order.</param>
/// <param name="Email">Email address used for order notifications.</param>
internal sealed record CreateOrderRequest(string FirstName, string LastName, string Email);
