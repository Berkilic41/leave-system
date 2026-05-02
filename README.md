# Employee & Leave Management System

[![CI](https://github.com/Berkilic41/leave-system/actions/workflows/ci.yml/badge.svg)](https://github.com/Berkilic41/leave-system/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

Multi-role HR system built with ASP.NET Core MVC (.NET 8), SQL Server, and ADO.NET (no ORM). Three-layer architecture: `LeaveSystem.Data` → `LeaveSystem.Bll` → `LeaveSystem.Web`. Built across **feature branches** to demonstrate a real git workflow.

## Why I Built This

Leave management is a daily workflow in every organisation, yet it's surprisingly hard to model correctly: managers approve only their team's requests, teams can span multiple hierarchy levels, and the same employee can be both a Manager and a subordinate. I built this system to tackle **recursive organisational hierarchy**, concurrency-safe approvals, and SOLID service design — problems I encounter regularly in enterprise backend work.

## 🔑 Technical Highlights

- **Recursive team hierarchy** — `sp_GetTeamHierarchy` (recursive CTE) resolves multi-level manager chains; managers approve all direct and indirect reports
- **Concurrency-safe decisions** — `sp_DecideLeaveRequest` runs with `UPDLOCK/HOLDLOCK` to prevent two managers simultaneously approving the same request
- **SOLID refactor** — Decomposed a 186-line `Services.cs` mega-file into 7 single-responsibility service classes (AuthService, LeaveRequestService, EmployeeService, …)
- **35+ unit tests** — LeaveRequestService (balance checks, team-scope authorization), AuthService, Manager service layer (xUnit + Moq)
- **Integration tests** — `WebApplicationFactory` verifying role-based redirects for Employee/Manager/HR routes
- **CI/CD** — GitHub Actions with test coverage upload
- **Containerized** — Multi-stage Dockerfile + docker-compose with SQL Server 2022

---

## Roles

| Role | Capabilities |
|---|---|
| **HR** | Edit any employee, see all leave requests, manage department/leave-type catalogs, view dashboard. Can approve/reject any request. |
| **Manager** | Approve / reject leave requests for direct **and indirect** reports (recursive hierarchy). View team's monthly calendar. Submit own leave (approved by their own manager / HR). |
| **Employee** | View profile, leave balance, request new leave (with client + server validation), cancel own pending requests. |

---

## Workflow diagram

```
┌──────────────┐
│   Employee   │
│  submits     │
│  request     │  StartDate, EndDate, Type, Reason
└──────┬───────┘
       │  status: Pending
       ▼
┌──────────────────┐
│   sp_GetLeaveBalance  ◀── enforces quota in BLL before insert
└──────┬───────────┘
       │
       ▼
┌──────────────┐    approve    ┌──────────────┐
│   Manager    │───────────────▶│   Approved   │  → balance reduces
│   reviews    │                └──────────────┘
│  (or HR)     │    reject     ┌──────────────┐
└──────────────┘───────────────▶│   Rejected   │  → no balance change
                                └──────────────┘
       Employee  cancel        ┌──────────────┐
       (only Pending)──────────▶│  Cancelled   │
                                └──────────────┘

Each transition is recorded in AuditLog (entity, actor, old → new, timestamp).
```

---

## Architecture

```
LeaveSystem/
├── Database/
│   ├── 001_Schema.sql            ← tables, indexes, hierarchical FK
│   ├── 002_StoredProcedures.sql  ← sp_GetLeaveBalance, sp_GetTeamHierarchy (CTE), sp_DecideLeaveRequest, sp_GetHrDashboard
│   └── 003_SeedData.sql          ← 3 roles, 4 departments, 8 employees with hierarchy, sample leave history
└── src/
    ├── LeaveSystem.Data/          ← entities + repositories (ADO.NET)
    │   ├── DbConnectionFactory.cs
    │   ├── Entities/
    │   └── Repositories/
    │       └── Interfaces/
    ├── LeaveSystem.Bll/           ← business logic (validation, authorization, audit)
    │   ├── Services/Interfaces/
    │   ├── DTOs/
    │   └── Helpers/               ← PasswordHasher (HMAC-SHA512)
    └── LeaveSystem.Web/           ← MVC web app
        ├── Controllers/           ← Account, Home, Employees, Leave, Manager, Hr
        ├── Views/                 ← Razor with shared `_Layout`, `_ValidationScriptsPartial`
        ├── ViewModels/
        ├── wwwroot/css, wwwroot/js
        ├── Program.cs
        └── appsettings.json
```

### Key SQL features
- **Self-referencing `Employees(ManagerId → Employees.Id)`** — recursive CTE in `sp_GetTeamHierarchy` returns direct + indirect reports
- **`sp_GetLeaveBalance(@EmployeeId, @Year)`** — annual quota minus approved + pending leave days, with optional per-employee override via `LeaveQuotas`
- **`sp_DecideLeaveRequest`** — wraps approve/reject in a transaction, locks the row with `(UPDLOCK, HOLDLOCK)`, writes to `AuditLog` atomically
- **`sp_GetHrDashboard`** — returns 4 result sets in one round-trip (headcount, on-leave-today, usage, top counts)
- **Critical indexes**: `IX_LR_Employee_Range`, `IX_LR_Status_Start`, `IX_Audit_Entity`

---

## Setup

### 1. Database

```bash
sqlcmd -S "(localdb)\mssqllocaldb" -i Database/001_Schema.sql
sqlcmd -S "(localdb)\mssqllocaldb" -i Database/002_StoredProcedures.sql
sqlcmd -S "(localdb)\mssqllocaldb" -i Database/003_SeedData.sql
```

> Schema script drops & recreates `LeaveDb`. All scripts use `SET QUOTED_IDENTIFIER ON`.

### 2. Run

```bash
cd src/LeaveSystem.Web
dotnet run
```

### 3. Demo accounts (password: `password123`)

| Email | Role | Notes |
|---|---|---|
| `hr@leave.test`     | HR       | Sees everything |
| `sara@leave.test`   | Manager  | Engineering manager — Alice, Bob, Carol report to her |
| `mike@leave.test`   | Manager  | Sales manager — Dan, Eve report to him |
| `alice@leave.test`  | Employee | Has approved + pending requests |
| `carol@leave.test`  | Employee | Currently on leave |

---

## Git workflow

This repo was built across feature branches; check `git log --graph --oneline --all` to see the merge history:

```
* Merge feature/docs
* feat(docs): comprehensive README
* Merge feature/hr-dashboard
* feat(hr-dashboard): department headcount, on-leave-today, leave usage stats
* Merge feature/leave-requests
* feat(leave-requests): submit/cancel/details, manager approvals + team monthly calendar
* Merge feature/web-foundation
* feat(web): foundation — auth, layout, employee list/details/edit, home with leave balance
* Merge feature/data-bll
* feat(data,bll): repositories with raw ADO.NET, services with role-based authorization
* fix(database): correct sp_GetLeaveBalance to use scalar subquery
* Merge feature/database
* feat(database): schema with hierarchical employees, stored procs, seed data
* chore: initial scaffold (3-project solution + .gitignore)
```

Each feature branch was created from main, committed in isolation, and merged with `--no-ff` to preserve the merge commit (clearer history).

---

## Routes

| Path | Auth | Description |
|---|---|---|
| `/` | All | Personal dashboard (employee) or HR redirect |
| `/Employees` | All | Browse, search, filter by department |
| `/Employees/Details/{id}` | All | Profile + leave history + balance + audit log |
| `/Employees/Edit/{id}` | HR | Update profile, change manager / department |
| `/Leave/Mine` | All | Own leave requests + balance |
| `/Leave/Create` | All | Submit new leave (live day count, validation) |
| `/Leave/Details/{id}` | Owner / Manager / HR | Single request view + cancel |
| `/Manager/Approvals` | Manager / HR | Approve / reject queue (filterable by status) |
| `/Manager/Calendar` | Manager / HR | Team monthly leave grid |
| `/Hr` | HR | Headcount, usage, on-leave-today |

---

## Validation

- **Server-side**: `[Required]`, `[EmailAddress]`, `[Compare]`, custom checks in services (date range, balance, role).
- **Client-side**: jQuery Validation Unobtrusive (built into MVC scaffolding) + custom `submit` handler in `Leave/Create.cshtml` for live day count + end-date check.

## Security
- HMAC-SHA512 + per-user salt
- Cookie auth, HTTP-only, 14-day sliding expiration
- Anti-forgery tokens on every state-changing form
- All SQL parameterized
- Decision SP runs at lock isolation to prevent double-decisions
- Manager-can-only-decide-own-team enforced in service layer
