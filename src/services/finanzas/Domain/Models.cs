namespace finanzas.api.Domain;

public enum MovementType { Income = 1, Expense = 2 }

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                              // dueño
    public string Name { get; set; } = default!;
    public string? Color { get; set; }
    public ICollection<Movement> Movements { get; set; } = new List<Movement>();
}

public class Movement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                              // dueño
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public MovementType Type { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;
    public string? Note { get; set; }
}
