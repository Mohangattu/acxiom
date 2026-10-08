# Acxiom CRM - Customer Relationship Management System

An academic final-year B.Tech CSE project developed with ASP.NET Core 8.0 MVC, Entity Framework Core, ASP.NET Core Identity, and Bootstrap 5.

---

## Stage 2: Authentication and Role-Based Authorization

### 1. Pre-configured Development / Test User Credentials

The system seeds three primary roles and corresponding test users automatically during database initialization.

| Role | Email | Password | Full Name | Department | Permissions / Access |
|---|---|---|---|---|---|
| **Admin** | `admin@crm.com` | `Admin@123` | John Admin | Administration | Full system administration, user management, audit logs, all CRM records |
| **Manager** | `manager@crm.com` | `Manager@123` | Sarah Connor | Sales Management | Team pipeline, deals, opportunities, audit log review, no user administration |
| **SalesExecutive** | `sales@crm.com` | `Sales@123` | Michael Scott | Direct Sales | Operational CRM: leads, customers, deals, tasks and follow-up activities |

> **Note:** All seeded test passwords adhere to the configured ASP.NET Core Identity policy: minimum 8 characters, uppercase, lowercase, number, and special character.

---

### 2. Password Policy Configuration

Configured in `Program.cs` via ASP.NET Core Identity:
- **Minimum Length:** 8 characters
- **Require Digit:** Yes
- **Require Lowercase:** Yes
- **Require Uppercase:** Yes
- **Require Non-Alphanumeric:** Yes (`@`, `$`, `!`, etc.)
- **Plaintext Storage:** Disabled (Uses standard PBKDF2 password hashing with cryptographically secure salts)

---

### 3. Account Lockout Protection

Configured in `Program.cs` and enforced in `AccountController`:
- **Allowed For New Users:** `true`
- **Max Failed Access Attempts:** `5`
- **Lockout Duration:** `15 minutes`
- **Lockout on Failure:** Enabled (`lockoutOnFailure: true` on `PasswordSignInAsync`)
- **Behavior:** Upon 5 consecutive failed login attempts, the account is temporarily locked, and a friendly notice is shown to the user without disclosing internal details. A security audit entry is logged.

---

### 4. Role-Based Access Control (RBAC)

Controllers enforce server-side role authorization:
- `[Authorize]` applied to all core modules: `/Dashboard`, `/Customers`, `/Leads`, `/Opportunities`, `/FollowUps`
- `[Authorize(Roles = "Admin")]` applied to `/Users` (User Management)
- `[Authorize(Roles = "Admin,Manager")]` applied to `/AuditLogs`
- `[AllowAnonymous]` applied only to public authentication endpoints (`/Account/Login`, `/Account/Register`, `/Account/AccessDenied`, `/Home/Index`)
- Anonymous users attempting to access any protected URL are redirected to `/Account/Login`.
- Authenticated users without sufficient permissions attempting to access admin-only endpoints are redirected to `/Account/AccessDenied`.

---

### 5. Authentication Audit Logging

All authentication and security events are recorded in `AuditLogs`:
- `Login` (Successful user sign-in)
- `FailedLogin` (Unsuccessful password attempt or unrecognized user)
- `AccountLockout` (Security threshold triggered)
- `Logout` (User sign-out)
- `Register` (New user account creation)
- `AdminCreateUser` (Admin user provisioning)
- *Note: Passwords and sensitive secrets are never logged.*
