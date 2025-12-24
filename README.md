# 🛒 Commerce Platform Backend

This repository contains the **backend implementation** of a **production-grade commerce platform** built with **ASP.NET Core Web API**, **Clean Architecture**, and **PostgreSQL**.

It is designed to be **scalable, maintainable, and secure**, serving as the foundational backend for multi-phase e-commerce projects.

---

## Table of Contents

* [Project Overview](#project-overview)
* [Architecture](#architecture)
* [Technology Stack](#technology-stack)
* [Project Structure](#project-structure)
* [Database Setup](#database-setup)
* [Authentication & Security](#authentication--security)
* [Running the Project](#running-the-project)
* [API Endpoints](#api-endpoints)
* [Verification](#verification)
* [Phase 2 & Beyond](#phase-2--beyond)
* [License](#license)

---

## Project Overview

This backend provides:

* **Core domain entities** for commerce: products, product variants, inventory, orders, carts, and customers
* **Authentication & authorization** via JWT + ASP.NET Identity
* **Role-based access control (RBAC)** for admins, customers, and support roles
* **Clean Architecture** separation: Domain, Application, Infrastructure, API
* **PostgreSQL database integration** using Entity Framework Core (Code-First)
* **Extensible design** for future features like recommendations, payments, and analytics

---

## Architecture

This project follows **Clean Architecture** principles:

```
Commerce.Domain         -> Core business entities (framework-agnostic)
Commerce.Application    -> DTOs, services, application logic, contracts
Commerce.Infrastructure -> Data access, repositories, EF Core, Identity
Commerce.API            -> Controllers, middleware, startup configuration
```

**Dependency rules:**

* Domain → no external dependencies
* Application → depends on Domain only
* Infrastructure → depends on Application + Domain
* API → depends on Application only

This ensures **testability, maintainability, and clear separation of concerns**.

---

## Technology Stack

**Backend:**

* ASP.NET Core 7 / Web API
* Entity Framework Core (Code-First)
* PostgreSQL
* ASP.NET Identity + JWT
* Clean Architecture (Domain, Application, Infrastructure, API)

**Tools & Utilities:**

* Visual Studio / VS Code
* PowerShell / Git Bash
* Docker (optional for future deployment)

---

## Project Structure

```
Commerce.sln
├─ Commerce.Domain          # Core business entities, enums, value objects
├─ Commerce.Application     # DTOs, interfaces, validation, services
├─ Commerce.Infrastructure  # DbContext, EF configurations, repositories, Identity
├─ Commerce.API             # Controllers, middleware, startup configuration
├─ .gitignore
├─ README.md
```

**Highlights:**

* `Entities` → Core models: Product, ProductVariant, Inventory, Cart, Order, CustomerProfile
* `Enums` → Statuses like OrderStatus, PaymentStatus
* `ValueObjects` → Address
* `DTOs` → Data transfer objects for API requests/responses
* `IRepository` → Generic repository interface for aggregate roots only

---

## Database Setup

1. Ensure **PostgreSQL** is installed locally or accessible remotely.
2. Update `appsettings.json` with your PostgreSQL connection string.
3. Run **EF Core migrations**:

```bash
dotnet ef migrations add InitialCreate --project Commerce.Infrastructure --startup-project Commerce.API
dotnet ef database update --project Commerce.Infrastructure --startup-project Commerce.API
```

4. Verify tables created:

* Identity tables: `AspNetUsers`, `AspNetRoles`
* Domain tables: `Products`, `ProductVariants`, `Inventory`, `Orders`, `Carts`, `CustomerProfiles`

---

## Authentication & Security

* JWT authentication for stateless API access
* ASP.NET Identity for user management
* Roles:

```
SuperAdmin
Admin
Operations
Support
Customer
```

* Refresh tokens are **hashed** and support rotation and revocation
* Cart ownership is strictly enforced (either `CustomerProfileId` or `AnonymousId`)

---

## Running the Project

1. Open solution in Visual Studio or VS Code
2. Restore NuGet packages:

```bash
dotnet restore
```

3. Build solution:

```bash
dotnet build
```

4. Run API:

```bash
dotnet run --project Commerce.API
```

5. Navigate to Swagger UI:

```
https://localhost:5001/swagger
```

---

## API Endpoints (Phase 1)

### Authentication (`/api/v1/auth`)

* `POST /register` → Register new user
* `POST /login` → Login user and receive JWT
* `POST /refresh` → Refresh access token
* `POST /revoke` → Revoke refresh token

### Products (`/api/v1/products`)

* `GET /` → Get all products
* `GET /{id}` → Get product by ID
* `POST /` → Create product (Admin only)
* `PUT /{id}` → Update product (Admin only)
* `DELETE /{id}` → Delete product (Admin only)

> More endpoints for Orders, Carts, Inventory will be implemented in Phase 2

---

## Verification

* Swagger UI accessible and working
* User registration/login flow verified
* JWT tokens enforce role-based access
* Database tables created successfully
* Clean Architecture dependency rules maintained

---

## Phase 2 & Beyond

Future phases will include:

* Orders workflow and inventory management
* Payment integration (eSewa, Khalti, Fonepay, COD)
* Notifications (Email, SMS, Push)
* Background jobs (Hangfire, RabbitMQ)
* Analytics and monitoring
* Recommendations and personalization

---

## License

This project is **open for internal/commercial use**. License can be added as required.
