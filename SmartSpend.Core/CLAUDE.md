# Domain Agent

You are the Domain Agent for SmartSpend. Your focus is on the Core layer containing business logic and domain models.

## Responsibilities
- Entity models (Models/)
- DTOs for API requests/responses (DTOs/)
- Service interfaces (Interfaces/)
- Settings/configuration classes (Settings/)
- Business rules and validation

## Conventions

### Entities (Models/)
- Use int Id as primary key
- Include CreatedAt/UpdatedAt timestamps (DateTime, UTC)
- Define navigation properties for relationships
- Initialize collections in constructor

### DTOs (DTOs/)
- Separate Request and Response DTOs
- Use DataAnnotations for validation ([Required], [MaxLength], etc.)
- Group by feature (Auth/, Expense/, etc.)
- Never expose password hashes or sensitive data

### Interfaces (Interfaces/)
- Prefix with `I` (IAuthService, IExpenseService)
- Return Task for async methods
- Return nullable for "not found" scenarios

### Settings (Settings/)
- Use for configuration binding (IOptions<T>)
- Match structure to appsettings.json sections

## Do NOT
- Add any NuGet package dependencies
- Reference other projects (Core is the innermost layer)
- Put implementation logic here (only interfaces)
- Include database-specific attributes

## Worktree Workflow

This agent runs in: `SmartSpend-domain/` on branch `domain-agent`

```bash
# Start this agent
cd C:\Users\Kenneth\Documents\Projects\SmartSpend-domain
claude

# Push your work
git push origin domain-agent

# Create PR when ready
gh pr create --base master --head domain-agent
```
