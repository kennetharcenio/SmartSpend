# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the entire solution
dotnet build

# Run the API (from root or SmartSpend.API directory)
dotnet run --project SmartSpend.API

# Run tests (when test projects are added)
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Restore dependencies
dotnet restore

# Clean build artifacts
dotnet clean
```

## Development URLs

- HTTP: `http://localhost:5135`
- HTTPS: `https://localhost:7121`

## Architecture

This is a .NET 10.0 Web API following Clean Architecture with three projects:

- **SmartSpend.API** - ASP.NET Core Web API presentation layer. Handles HTTP requests, controllers, and OpenAPI/Swagger documentation.
- **SmartSpend.Core** - Business logic and domain models. Pure C# with no external dependencies. Other projects depend on this layer.
- **SmartSpend.Infrastructure** - Data access using Entity Framework Core with SQL Server. Implements interfaces defined in Core.

**Dependency flow:** API → Core ← Infrastructure (Infrastructure implements Core interfaces)

## Key Dependencies

- `Microsoft.AspNetCore.OpenApi` - OpenAPI documentation
- `Microsoft.EntityFrameworkCore.SqlServer` - SQL Server database access
- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT authentication
- `BCrypt.Net-Next` - Password hashing

## Git Commits

Do not include AI attribution, co-author lines, or any AI-related footers in commit messages.

---------------------------------------------------------------------------------------------------------------------------------------
---

# SmartSpend — AI Expense Tracker

## Tech stack
- Backend: .NET 8 Web API
- Database: SQL Server (SSMS + EF Core migrations)
- Frontend: Angular 17
- AI: OpenAI GPT-4o via .NET SDK + Semantic Kernel
- Auth: JWT bearer tokens
- Testing: xUnit + Playwright

## Solution structure
```
SmartSpend/
├── SmartSpend.sln
├── SmartSpend.API/            ← entry point, controllers, Program.cs
├── SmartSpend.Core/           ← entity models, interfaces, DTOs
├── SmartSpend.Infrastructure/ ← AppDbContext, EF migrations, services
└── smartspend-ui/             ← Angular 17 (Sprint 2)
```

## Database tables
- Users (Id, Email, PasswordHash, FullName, CreatedAt, UpdatedAt)
- Categories (Id, Name, Icon, IsDefault, UserId nullable)
- Expenses (Id, UserId, CategoryId, Amount, Description, Merchant, ExpenseDate, IsAIParsed, RawInput, CreatedAt, UpdatedAt)
- AIInsights (Id, UserId, MonthYear char(7), InsightText, GeneratedAt, ExpiresAt)

## Conventions
- All DB queries scoped by UserId — never leak another user's data
- Use DTOs for all API requests/responses — never expose entities directly
- All dates in UTC
- Async/await throughout

## Test-Driven Development (TDD)

**This project follows TDD. Always write tests BEFORE implementation.**

### TDD Workflow
1. **RED** - Write a failing test first
2. **GREEN** - Write minimal code to make the test pass
3. **REFACTOR** - Improve code while keeping tests green

### Test Commands
```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~AuthServiceTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Structure
- Location: `SmartSpend.Tests/`
- Naming: `[ClassName]Tests.cs`
- Method naming: `MethodName_Scenario_ExpectedResult`

### When Adding New Features
1. Write interface in Core first
2. Write tests against the interface
3. Implement service to pass tests
4. Write controller tests
5. Implement controller

## Current progress
- [x] SQL schema scripts written
- [x] Solution scaffolded (API + Core + Infrastructure)
- [x] EF Core models in SmartSpend.Core/Models (User, Category, Expense, AIInsight)
- [x] AppDbContext in SmartSpend.Infrastructure/Data with entity configurations
- [x] Connection string + Program.cs DI setup
- [x] AddEntityModels migration + applied to SQL Server
- [x] Auth endpoints (register/login + JWT)
- [ ] CURRENT: Expense CRUD endpoints
- [ ] Angular scaffold (Sprint 2)
- [ ] AI features: expense parsing, monthly insights, chat sidebar (Sprint 3)

## Next task
Implement Expense CRUD endpoints:
1. Create ExpenseController with CRUD operations
2. Create DTOs (CreateExpenseRequest, UpdateExpenseRequest, ExpenseResponse)
3. Create IExpenseService interface in Core
4. Create ExpenseService in Infrastructure
5. Add [Authorize] attribute to protect endpoints
