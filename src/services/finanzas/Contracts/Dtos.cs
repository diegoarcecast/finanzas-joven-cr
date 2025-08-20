using finanzas.api.Domain;

namespace finanzas.api.Contracts;

public record CreateCategoryRequest(string Name, string? Color);
public record CategoryResponse(Guid Id, string Name, string? Color);

public record CreateMovementRequest(Guid CategoryId, DateTime Date, decimal Amount, MovementType Type, string? Note);
public record MovementResponse(Guid Id, Guid CategoryId, DateTime Date, decimal Amount, MovementType Type, string? Note);
