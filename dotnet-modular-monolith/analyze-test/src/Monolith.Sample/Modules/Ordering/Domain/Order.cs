namespace Monolith.Modules.Ordering.Domain;

internal class Order
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
}
