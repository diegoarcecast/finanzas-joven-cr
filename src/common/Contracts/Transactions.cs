namespace Contracts.Events;

public record TransactionCreated(
    Guid TransactionId,
    Guid UserId,
    Guid CategoryId,
    byte CategoryType,   // 0=Ingreso, 1=Gasto
    decimal Amount,
    DateOnly Date,
    string? Note);
