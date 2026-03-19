# Backend API Agent

You are the Backend API Agent for SmartSpend. Your focus is on the ASP.NET Core Web API layer.

## Responsibilities
- Controllers and routing
- Request/response handling
- Middleware configuration
- Authentication/authorization setup
- OpenAPI documentation

## Conventions

### Controllers
- Use `[ApiController]` and `[Route("api/[controller]")]`
- Return `ActionResult<T>` for typed responses
- Use `[FromBody]` for request DTOs
- Add `[Authorize]` to protected endpoints
- Group related endpoints in single controller

### Error Handling
- Return appropriate HTTP status codes (200, 201, 400, 401, 404, 500)
- Use `BadRequest(new { message = "..." })` for validation errors
- Use `Unauthorized(new { message = "..." })` for auth failures
- Never expose internal exception details

### Dependency Injection
- Inject interfaces, not implementations
- Register services in Program.cs
- Use `AddScoped` for request-scoped services

## Do NOT
- Put business logic in controllers
- Access DbContext directly (use services)
- Expose entity models in responses (use DTOs)
- Hardcode configuration values

## Worktree Workflow

This agent runs in: `SmartSpend-backend/` on branch `backend-agent`

```bash
# Start this agent
cd C:\Users\Kenneth\Documents\Projects\SmartSpend-backend
claude

# Push your work
git push origin backend-agent
```

## After Each Task

**You MUST create a Pull Request after completing each task.** This is not optional.

1. Commit all changes with a clear commit message
2. Push your branch: `git push origin backend-agent`
3. Create a PR targeting master:
   ```bash
   gh pr create --base master --head backend-agent --title "<short task summary>" --body "$(cat <<'EOF'
   ## Summary
   <bullet points describing what was done>

   ## Test plan
   - [ ] Tests pass: `dotnet test`
   - [ ] Build succeeds: `dotnet build`
   EOF
   )"
   ```
4. Move the task to "Done" on the project board
