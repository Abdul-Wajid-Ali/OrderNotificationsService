# Order Notifications Service

A production-oriented ASP.NET Core Web API that manages order lifecycle events and delivers user notifications using a transactional outbox pattern and RabbitMQ-driven background processing.

This service solves a common reliability problem in distributed systems: **ensuring order status updates and downstream notifications remain consistent even when messaging infrastructure is temporarily unavailable**.

---

## 1. Project Title and Description

**Order Notifications Service** is a .NET 8 backend service that:

- creates and updates orders,
- stores domain events in an outbox table atomically with state changes,
- publishes outbox events to RabbitMQ,
- consumes order status events to generate in-app and email notifications.

The design prioritizes reliability, idempotency, and observability for notification workflows.

## 2. Architecture Overview

The project follows a **layered modular architecture** with clear separation of concerns:

- **Domain**: entities, enums, and domain events.
- **Features (Application)**: use-case handlers for orders and notifications.
- **Infrastructure**: persistence (EF Core), RabbitMQ messaging, background workers, correlation, and monitoring.
- **API (Controllers)**: HTTP endpoints for order and notification operations.

### Event flow (high-level)

1. `PUT /api/update/{orderId}/status` updates order status.
2. A corresponding `OrderStatusChangedEvent` is written to `OutboxEvents` in the same DB transaction.
3. `OutboxProcessor` publishes pending outbox events to RabbitMQ with retry/backoff.
4. `OrderStatusConsumer` reads messages and invokes `OrderStatusChangedHandler`.
5. Notification records are created/updated idempotently; email delivery is attempted.

## 3. Key Features

- Transactional outbox pattern for reliable event publishing.
- RabbitMQ fanout exchange integration for order event distribution.
- Background workers for outbox publishing and event consumption.
- Idempotent notification processing by source event ID.
- In-app and email notification tracking with delivery status.
- Retry + dead-letter behavior for failed outbox publications.
- Correlation ID middleware and trace propagation metadata.
- Built-in pipeline metrics and operational alert thresholds.

## 4. Technology Stack

- **Runtime**: .NET 8
- **API**: ASP.NET Core Web API
- **ORM**: Entity Framework Core 8
- **Database**: SQL Server
- **Messaging**: RabbitMQ (`RabbitMQ.Client`)
- **API docs**: Swagger / OpenAPI (`Swashbuckle.AspNetCore`)
- **Observability**: .NET Metrics (`IMeterFactory`), structured logging, correlation IDs

## 5. Project Structure Explanation

```text
OrderNotificationsService/
├── Controllers/                    # HTTP API endpoints
├── Domain/                         # Core domain entities, events, enums
├── Features/                       # Application use-case handlers
│   ├── Orders/                     # Create/update order use cases
│   └── Notifications/              # Notification query and processing use cases
├── Infrastructure/
│   ├── BackgroundServices/         # Outbox processing worker
│   ├── Messaging/                  # RabbitMQ publisher + consumer
│   ├── Monitoring/                 # Metrics + threshold options
│   ├── Correlation/                # Correlation ID middleware/accessor
│   ├── Notifications/              # Email sender abstractions/implementations
│   └── Persistence/                # EF Core DbContext + migrations + configurations
├── Extensions/                     # App startup/service registration extensions
├── Program.cs                      # Application entry point
└── appsettings*.json               # Environment-specific configuration
```

## 6. Getting Started

### Prerequisites

- .NET SDK 8.0+
- SQL Server instance (localdb/dev/prod)
- RabbitMQ broker

### Installation

```bash
git clone https://github.com/Abdul-Wajid-Ali/OrderNotificationsService
cd OrderNotificationsService
```

### Running the project

```bash
dotnet restore
dotnet build
cd OrderNotificationsService
dotnet ef database update
dotnet run
```

Swagger UI is available in development at:

```text
https://localhost:5001/swagger/index.html
```

> Port is assigned by ASP.NET launch settings unless explicitly configured.

## 7. Configuration

### Environment variables

You can configure settings via `appsettings*.json` or environment variables.

| Setting                                        | Description                                   | Example                                              |
| ---------------------------------------------- | --------------------------------------------- | ---------------------------------------------------- |
| `ConnectionStrings__DefaultConnection`         | SQL Server connection string                  | `Server=localhost;Database=OrderNotificationsDb;...` |
| `RabbitMq__HostName`                           | RabbitMQ host                                 | `localhost`                                          |
| `RabbitMq__ExchangeName`                       | Fanout exchange for order events              | `order-events`                                       |
| `Monitoring__FailureRateAlertThresholdPercent` | Failure-rate alert threshold                  | `25`                                                 |
| `Monitoring__QueueLagAlertThresholdSeconds`    | Queue lag alert threshold                     | `120`                                                |
| `Monitoring__MinimumEventsForFailureRateAlert` | Minimum events before evaluating failure rate | `20`                                                 |

### Database setup

1. Ensure SQL Server is reachable.
2. Set `ConnectionStrings__DefaultConnection`.
3. Apply EF Core migrations:

```bash
cd OrderNotificationsService
dotnet ef database update
```

## 8. API Documentation

### Base URL

```text
https://localhost:5001
```

### Example endpoints

#### Create order

```http
POST /api/create/order
Content-Type: application/json

{
  "userId": "9c9bdbd4-32a1-4d1d-b2c7-e7c5d44d2e57"
}
```

#### Update order status

```http
PUT /api/update/{orderId}/status
Content-Type: application/json

{
  "status": "Shipped"
}
```

Supported status values: `Placed`, `Processing`, `Shipped`, `Delivered`, `Cancelled`.

#### Get user notifications

```http
GET /api/user/notifications/{userId}?pageSize=20
```

## 9. Message Queue / Background Processing

- **OutboxProcessor** polls unprocessed outbox records, publishes to RabbitMQ, retries with exponential backoff, and dead-letters after max retries.
- **OrderStatusConsumer** consumes from durable queue `order-notifications` bound to the configured fanout exchange.
- Messages include metadata such as `CorrelationId`, `MessageId`, and trace identifiers for observability.

## 10. Observability (logging, metrics, tracing)

- **Logging**: structured ASP.NET Core logging with error/warning signaling for alert conditions.
- **Correlation**: middleware assigns/propagates correlation IDs and enriches processing scopes.
- **Metrics** (meter: `OrderNotificationsService.NotificationPipeline`):
  - `outbox_publish_success_total`
  - `outbox_publish_failure_total`
  - `consumer_process_success_total`
  - `consumer_process_failure_total`
  - `notification_processing_latency_ms`
  - `outbox_backlog_count`
  - `outbox_backlog_oldest_age_seconds`

## 11. Development Guidelines

- Keep handlers focused on one business use case.
- Preserve transactional boundaries between order updates and outbox writes.
- Maintain idempotency when handling events.
- Prefer dependency injection for all infrastructure concerns.
- Add migrations for all persistence model changes.
- Keep API contracts backward compatible when possible.

## 12. Running Tests

If test projects are present:

```bash
dotnet test
```

Current repository snapshot does not include a dedicated test project yet.

## 13. Docker Setup (if applicable)

Docker artifacts are not currently included in this repository.

Recommended next step:

- add a `Dockerfile` for the API,
- add `docker-compose.yml` for API + SQL Server + RabbitMQ local development.

## 14. Deployment Notes

- Configure production-grade SQL Server and RabbitMQ endpoints.
- Provide configuration via environment variables or secret manager.
- Run migrations as part of deployment workflow.
- Ensure health checks and metric scraping are integrated in hosting environment.
- Scale background workers based on event volume and queue throughput.

## 15. Future Improvements / Roadmap

- Add automated test coverage (unit, integration, contract tests).
- Add OpenTelemetry exporter integration (OTLP/Jaeger/Prometheus).
- Introduce authentication/authorization (JWT/OAuth2) for API endpoints.
- Implement durable email provider integration (e.g., SendGrid/SES).
- Add Docker/Kubernetes deployment manifests.
- Add API versioning and pagination metadata standards.

## 16. Contributing

1. Fork the repository.
2. Create a feature branch.
3. Commit with clear, scoped messages.
4. Add/update tests and documentation.
5. Open a pull request with implementation notes and validation steps.

## 17. License

No license file is currently included.

If this project is intended to be open source, add a `LICENSE` (for example, MIT or Apache-2.0) and update this section accordingly.
