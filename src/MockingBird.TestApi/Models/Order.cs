using System.Text.Json.Serialization;

namespace MockingBird.TestApi.Models;

/// <summary>
/// Schema-only model: describes the shape of the /v1/orders responses in the OpenAPI document, but
/// has no controller behind it. MockingBird generates data matching this shape at request time.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
}

public sealed class OrderLineItem
{
    public required string ProductName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}

public sealed class Order
{
    public required string Id { get; init; }
    public required OrderStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required List<OrderLineItem> Items { get; init; }
    public required decimal Total { get; init; }
}

public sealed class CreateOrderRequest
{
    public required List<OrderLineItem> Items { get; init; }
}
