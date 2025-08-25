using finanzas.api.Domain;

namespace finanzas.api.Contracts;

public sealed record UpdateMovementRequest(
    Guid CategoryId,
    DateTime Date,
    decimal Amount,
    MovementType Type,
    string? Note
);
