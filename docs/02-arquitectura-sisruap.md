# Arquitectura estilo SISRUAP — v0.1
(Fecha: YYYY-MM-DD)

## Vista general
- Microservicios: auth.api, finanzas.api, reportes.api, archivos.api detrás de un API Gateway.
- DB por servicio. Sin FKs cruzados; integración por IDs y eventos.
- JWT emitido por auth.api; validación en los demás.

## Capas por servicio
Web → Application → Domain → Infrastructure (EF Core/Dapper/Outbox).

## ERD por servicio
### auth.api (AuthDb)
- Identity (AspNetUsers/AspNetRoles/...) con UserId GUID y JWT.

### finanzas.api (FinanzasDb, esquema fin)
- Category(Id, UserId, Name, Type, IsDeleted, CreatedAt, UpdatedAt) UNIQUE(UserId,Type,Name)
- Transaction(Id, UserId, CategoryId, Amount, Date, Note, PaymentMethod, CreatedAt)
- MonthlySavingGoal(Id, UserId, Year, Month, TargetAmount) UNIQUE(UserId,Year,Month)
- Outbox(Id, Type, Payload, OccurredOn, Processed, ProcessedOn)

### reportes.api (ReportesDb)
- MonthlySummary(UserId, Year, Month, IncomeTotal, ExpenseTotal, UpdatedAt)
- CategorySpending(UserId, Year, Month, CategoryId, CategoryName, ExpenseTotal)

### archivos.api (ArchivosDb + Blob)
- Receipt(Id, UserId, TransactionId, BlobKey, FileName, ContentType, SizeBytes, CreatedAt)

## Endpoints v1 (resumen)
- auth.api: POST /auth/register, /auth/login, /auth/refresh; GET /auth/me
- finanzas.api: /categories, /transactions, /goals
- reportes.api: /summary/monthly, /summary/top-categories
- archivos.api: /receipts/upload, /receipts/{id}

## Eventos (finanzas → reportes)
- TransactionCreated, TransactionUpdated, TransactionDeleted, CategoryRenamed (opcional).

## Seguridad
- JWT (issuer auth.api), roles user/admin, policies por área.

## Observabilidad
- Serilog, correlation-id, health checks, API versioning.

## Notas
- En dev: bus opcional (polling Outbox + HTTP); en prod: Service Bus/Kafka.
- Locale: es-CR, CRC (₡), TZ America/Costa_Rica.
