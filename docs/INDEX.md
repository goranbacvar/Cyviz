# Cyviz Documentation Index

Welcome to the Cyviz documentation! This comprehensive guide explains how Razor components in the browser communicate with the SQLite database in your Blazor Server application.

## Viewing in Browser

**Best way to view documentation:**

1. **Double-click `docs-portal.html`** in the root folder - Opens beautiful HTML portal
2. **OR** use markdown preview in VS Code (`Ctrl+Shift+V`)
3. **OR** see [HOW-TO-VIEW-DOCS.md](../HOW-TO-VIEW-DOCS.md) for all options

## All Documentation Files

| Document | Purpose | Time | Level |
|----------|---------|------|-------|
| **[Cheat Sheet](CHEAT-SHEET.md)** | Quick reference card | 5 min | All |
| **[Quick Reference](QUICK-REFERENCE.md)** | Code patterns & examples | 10 min | Beginner |
| **[Visual Flow](VISUAL-FLOW.md)** | Step-by-step diagrams | 15 min | Beginner |
| **[Data Flow Guide](DATA-FLOW.md)** | Complete explanation | 30 min | Intermediate |
| **[Architecture Diagrams](ARCHITECTURE-DIAGRAMS.md)** | System architecture | 45 min | Advanced |
| **[Main README](../README.md)** | Project overview | 10 min | All |
| **[Architecture Overview](../ARCHITECTURE.md)** | High-level design | 20 min | Intermediate |
| **[Documentation Summary](../DOCUMENTATION-SUMMARY.md)** | What was created | 5 min | All |
| **[How to View Docs](../HOW-TO-VIEW-DOCS.md)** | Browser viewing guide | 2 min | All |

## Documentation Structure

### For Beginners

Start here if you're new to Blazor Server or want to understand the basics:

1. **[Quick Reference](QUICK-REFERENCE.md)** — START HERE
   - Common code patterns
   - Copy-paste examples
   - Best practices checklist
   - 10-minute read

2. **[Visual Flow](VISUAL-FLOW.md)**
   - Step-by-step diagrams
   - Complete data journey from browser to database
   - ASCII art diagrams
   - 15-minute read

### For Intermediate Users

Once you understand the basics, dive deeper:

3. **[Data Flow Guide](DATA-FLOW.md)**
   - Detailed explanation of Blazor Server architecture
   - CRUD operation examples
   - Real-time SignalR integration
   - Performance considerations
   - 30-minute read

4. **[Architecture Diagrams](ARCHITECTURE-DIAGRAMS.md)**
   - System overview
   - Component architecture
   - SignalR communication flows
   - Command pipeline
   - Database schema
   - 45-minute read

### For Advanced Users

Deep technical documentation:

5. **[Architecture Overview](../ARCHITECTURE.md)**
   - High-level system design
   - Communication patterns
   - Security model
   - Protocol adapters
   - 20-minute read

## Quick Navigation

### By Topic

| Topic | Document | Section |
|-------|----------|---------|
| **Database Access** | [Quick Reference](QUICK-REFERENCE.md#how-to-access-database-in-razor-components) | Basic patterns |
| **CRUD Operations** | [Data Flow Guide](DATA-FLOW.md#complete-example-crud-operations) | Examples |
| **SignalR Real-Time** | [Data Flow Guide](DATA-FLOW.md#real-time-updates-with-signalr-hubs) | Integration |
| **Performance Tips** | [Quick Reference](QUICK-REFERENCE.md#performance-tips) | Optimization |
| **Security** | [Data Flow Guide](DATA-FLOW.md#security-model) | Security |
| **Troubleshooting** | [Quick Reference](QUICK-REFERENCE.md#common-pitfalls) | Common issues |
| **Visual Diagrams** | [Visual Flow](VISUAL-FLOW.md) | All diagrams |
| **Command Pipeline** | [Architecture Diagrams](ARCHITECTURE-DIAGRAMS.md#command-pipeline) | Flow details |

### By Use Case

#### "I want to display data from the database"
[Quick Reference - Pattern 1: Read-Only List](QUICK-REFERENCE.md#pattern-1-read-only-list)

#### "I want to create a form to add data"
[Quick Reference - Pattern 2: Create Form](QUICK-REFERENCE.md#pattern-2-create-form)

#### "I want to edit existing data"
[Quick Reference - Pattern 3: Edit Form](QUICK-REFERENCE.md#pattern-3-edit-form-with-parameter)

#### "I want real-time updates"
[Quick Reference - Pattern 5: Real-Time Updates](QUICK-REFERENCE.md#pattern-5-real-time-updates-with-signalr)

#### "I want to understand how it all works"
[Visual Flow](VISUAL-FLOW.md#complete-round-trip-journey)

#### "My component isn't updating after database changes"
[Quick Reference - Pitfall 3: Not Calling StateHasChanged](QUICK-REFERENCE.md#pitfall-3-not-calling-statehaschanged)

## Reading Paths

### Path 1: Quick Start (30 minutes)
For developers who want to start coding immediately:

1. [Quick Reference](QUICK-REFERENCE.md) (10 min)
2. [Visual Flow - Scenario](VISUAL-FLOW.md#scenario-user-views-device-list) (10 min)
3. [Data Flow Guide - Key Concepts](DATA-FLOW.md#key-concepts) (10 min)

### Path 2: Complete Understanding (2 hours)
For developers who want comprehensive knowledge:

1. [Visual Flow](VISUAL-FLOW.md) (15 min)
2. [Data Flow Guide](DATA-FLOW.md) (45 min)
3. [Architecture Diagrams](ARCHITECTURE-DIAGRAMS.md) (45 min)
4. [Quick Reference](QUICK-REFERENCE.md) (15 min - as reference)

### Path 3: Problem Solving (15 minutes)
For developers debugging an issue:

1. [Quick Reference - Troubleshooting](QUICK-REFERENCE.md#debugging) (5 min)
2. [Quick Reference - Common Pitfalls](QUICK-REFERENCE.md#common-pitfalls) (10 min)
3. If still stuck — [Data Flow Guide - Security Model](DATA-FLOW.md#security-model)

## Key Concepts Summary

### Core Principle

**Blazor Server = Server-Side Execution**

```
Browser (Displays HTML) — SignalR — Server (Runs C# Code) — EF Core — SQLite
```

### The Three Layers

1. **Browser (Client)**
   - Displays HTML
   - Captures user events
   - No C# code execution
   - No database access

2. **ASP.NET Core Server**
   - Executes Razor components
   - Handles database queries
   - Business logic
   - Real-time SignalR hubs

3. **SQLite Database**
   - Persistent storage
   - Single file (app.db)
   - Entity Framework Core

### Essential Code Pattern

```csharp
@page "/devices"
@inject IDbContextFactory<AppDbContext> DbFactory

<h1>Devices</h1>

@foreach (var device in devices)
{
    <div>@device.Name</div>
}

@code {
    private List<Device> devices = new();

    protected override async Task OnInitializedAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();
    }
}
```

**This code runs entirely on the server. The browser only sees the rendered HTML.**

## Learning Resources

### Official Documentation
- [Blazor Documentation](https://docs.microsoft.com/aspnet/core/blazor/)
- [SignalR Documentation](https://docs.microsoft.com/aspnet/core/signalr/)
- [Entity Framework Core](https://docs.microsoft.com/ef/core/)

### Cyviz-Specific Guides
- [README.md](../README.md) - Project overview and quick start
- [ARCHITECTURE.md](../ARCHITECTURE.md) - High-level architecture

### Video Tutorials (Recommended)
- Blazor University: https://blazor-university.com/
- .NET YouTube Channel: https://www.youtube.com/@dotnet

## Getting Help

### Common Questions

**Q: How does the browser communicate with the database?**  
A: It doesn't directly. The browser sends events to the server via SignalR, the server queries the database, then sends HTML back to the browser.  
See: [Visual Flow](VISUAL-FLOW.md)

**Q: Where does my @code block execute?**  
A: On the server, not in the browser.  
See: [Data Flow Guide - Key Concepts](DATA-FLOW.md#1-server-side-execution)

**Q: Why use IDbContextFactory instead of DbContext?**  
A: Blazor Server needs proper lifetime management for concurrent requests.  
See: [Quick Reference - DbContext Lifetime](QUICK-REFERENCE.md#dbcontext-lifetime-management)

**Q: How do I update the UI when data changes?**  
A: Call `StateHasChanged()` or `await InvokeAsync(StateHasChanged)`.  
See: [Quick Reference - Pitfall 3](QUICK-REFERENCE.md#pitfall-3-not-calling-statehaschanged)

**Q: Can I use Blazor Server with other databases?**  
A: Yes! Just change the provider in Program.cs (SQL Server, PostgreSQL, MySQL, etc.).  
See: [Data Flow Guide - Configuration](DATA-FLOW.md#configuration)

### Still Stuck?

1. Check [Quick Reference - Common Pitfalls](QUICK-REFERENCE.md#common-pitfalls)
2. Enable SQL logging: [Quick Reference - Debugging](QUICK-REFERENCE.md#debugging)
3. Review [Visual Flow](VISUAL-FLOW.md) to understand the complete data path
4. Open an issue on GitHub with:
   - Error message
   - Relevant code snippet
   - What you've tried

## Documentation Statistics

| Document | Purpose | Read Time | Complexity |
|----------|---------|-----------|------------|
| [Quick Reference](QUICK-REFERENCE.md) | Code patterns | 10 min | Beginner |
| [Visual Flow](VISUAL-FLOW.md) | Step-by-step diagrams | 15 min | Beginner |
| [Data Flow Guide](DATA-FLOW.md) | Comprehensive explanation | 30 min | Intermediate |
| [Architecture Diagrams](ARCHITECTURE-DIAGRAMS.md) | System architecture | 45 min | Advanced |
| [Architecture Overview](../ARCHITECTURE.md) | High-level design | 20 min | Intermediate |

## Next Steps

### For New Users
1. Read [Quick Reference](QUICK-REFERENCE.md)
2. Try the examples in your project
3. Refer back as needed

### For Intermediate Users
1. Study [Data Flow Guide](DATA-FLOW.md)
2. Implement real-time features with SignalR
3. Optimize performance

### For Advanced Users
1. Review [Architecture Diagrams](ARCHITECTURE-DIAGRAMS.md)
2. Understand the command pipeline
3. Contribute to the project

## Feedback

Found an error? Have a suggestion?
- Open an issue on GitHub
- Submit a pull request
- Contact the maintainers

---

**Happy coding!**

Last updated: 2024-01-15
