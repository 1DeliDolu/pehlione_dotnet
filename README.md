# Pehlione (.NET)

Pehlione is an ASP.NET Core MVC e-commerce project with MySQL + EF Core.

## Completed Work

- Core commerce and checkout
  - Implemented anonymous catalog/cart browsing.
  - Added login-required checkout completion flow.
  - Added cart variant support (color/size).
  - Implemented 4-step checkout wizard (User, Address, Payment, Confirmation).
  - Added order persistence (`Order`, `OrderItem`) and order creation from cart.
  - Added order confirmation email integration (MailHog pickup flow in development).

- Customer account management
  - Added `/Customer/Account` dashboard.
  - Profile update (email, username, phone).
  - Password change.
  - Order history listing.
  - Address management (create, update, delete, set default).
  - Payment/bank method management (create, update, delete, set default).

- Inventory and staff operations
  - Added hierarchical inventory selection flow (top category -> subgroup -> product).
  - Added stock receive flow (increase stock).
  - Added stock decrease flow with safe guard (cannot go below zero).
  - Added stock snapshots, movement history and stock table search/edit behavior.
  - Added IT product delete operation with role checks.
  - Added department-based stock constraints persisted in DB (`department_constraints`).

- Role-based dashboards and routing
  - Role-aware login redirects for Admin, Purchasing, IT, HR, Warehouse, Staff, Customer.
  - Added dedicated dashboards:
    - Staff/Purchasing
    - Staff/IT (constraints + personnel creation/password setup)
    - Staff/HR (personnel role/department/position management)
    - Staff/Warehouse
  - Added account dropdown actions in navbar (`Dashboard`, `Sign out`).

- Admin management enhancements
  - `/Admin` dashboard redesigned with:
    - modern admin navbar
    - KPI cards
    - DB statistics
    - charts (category stock, monthly orders/revenue)
  - Added separate modern Admin forms (reachable from dashboard):
    - Employee create/password form
    - Product create form
    - Stock increase/decrease operation form
    - Personnel role/department operation form
  - Unified admin styling across Products/Categories/Users pages.
  - Added per-form search bar component for admin forms.

- Roles and seed updates
  - Added/extended roles: `Purchasing`, `IT`, `HR`, `Warehouse`, `Staff`, `Customer`, `Admin`.
  - Seeded role users from config/env (including purchasing/it/hr/warehouse defaults).
  - Kept password policy relaxed for local development (`RequiredLength = 6`, no special constraints).

- Database and migrations
  - Added and applied migration for `department_constraints`.
  - Synced EF Core model snapshot and runtime behavior with MySQL schema.

## Route Map

| Area | URL | Purpose |
|---|---|---|
| Public | `/` | Home page |
| Auth | `/Account/Login` | Login |
| Auth | `/Account/ChangePassword` | Password change (global) |
| Customer | `/Customer/Home/Index` | Customer home |
| Customer | `/Customer/Catalog/Index` | Catalog |
| Customer | `/Customer/Cart/Index` | Cart |
| Customer | `/Customer/Cart/Checkout` | Checkout wizard |
| Customer | `/Customer/Account/Index` | Customer account (profile/orders/address/payment/password) |
| Admin | `/Admin` | Admin dashboard |
| Admin | `/Admin/Home/UserForm` | Employee create/password form |
| Admin | `/Admin/Home/ProductForm` | Product create form |
| Admin | `/Admin/Home/StockForm` | Admin stock increase/decrease form |
| Admin | `/Admin/Home/PersonnelForm` | Personnel role/department update form |
| Admin | `/Admin/Products/Index` | Product management |
| Admin | `/Admin/Categories/Index` | Category management |
| Admin | `/Admin/Users/Index` | User management |
| Staff | `/Staff/Home/Index` | Staff home |
| Staff | `/Staff/Purchasing/Index` | Purchasing dashboard |
| Staff | `/Staff/It/Index` | IT dashboard |
| Staff | `/Staff/Hr/Index` | HR dashboard |
| Staff | `/Staff/Warehouse/Index` | Warehouse dashboard |
| Staff | `/Staff/Inventory/Receive` | Inventory receive/decrease screen |
| Staff | `/Staff/Notifications/Index` | Department notifications |

## Next

- Add integration tests for critical role-based operations:
  - inventory increase/decrease
  - department constraint enforcement
  - admin quick-operation forms
  - customer account CRUD flows
- Add server-side audit trail for admin/staff actions (who changed what, when).
- Add pagination/filtering for customer order history and admin data grids.
- Improve localization consistency (TR/DE labels and validation messages).
