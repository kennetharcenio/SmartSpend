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

## Git Commits

Do not include AI attribution, co-author lines, or any AI-related footers in commit messages.
