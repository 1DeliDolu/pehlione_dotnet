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
  - Added standardized order status workflow:
    - `Pending -> Paid -> Processing -> Packed -> Shipped -> Courier Picked Up -> Out for Delivery -> Delivered -> Completed`
    - Return/refund path: `Cancelled -> Return Picked Up -> Return Delivered to Seller -> Refunded`

- Customer account management
  - Added `/Customer/Account` dashboard.
  - Profile update (email, username, phone).
  - Password change.
  - Order history listing.
  - Address management (create, update, delete, set default).
  - Payment/bank method management (create, update, delete, set default).
  - Customer order cancellation with workflow validation.
  - Customer notifications/communication area integration (customer relations messaging + status updates).

- Inventory and staff operations
  - Added hierarchical inventory selection flow (top category -> subgroup -> product).
  - Added stock receive flow (increase stock).
  - Added stock decrease flow with safe guard (cannot go below zero).
  - Added stock snapshots, movement history and stock table search/edit behavior.
  - Added IT product delete operation with role checks.
  - Added department-based stock constraints persisted in DB (`department_constraints`).
  - Added purchasing returns restock operation (returned goods can be added back to stock).
  - Added courier operational flow support for delivery/return pickup transitions.

- Role-based dashboards and routing
  - Role-aware login redirects for Admin, Purchasing, IT, HR, Warehouse, Accounting, Courier, CustomerRelations, Staff, Customer.
  - Added dedicated dashboards:
    - Staff/Purchasing
    - Staff/IT (constraints + personnel creation/password setup)
    - Staff/HR (personnel role/department/position management)
    - Staff/Warehouse
    - Staff/Accounting
    - Staff/Courier
    - Staff/CustomerRelations
  - Added account dropdown actions in navbar (`Dashboard`, `Sign out`).
  - Notification widget enabled on dashboards to support cross-department workflow handoff.
  - Dashboard notification feed now supports manual event creation with multi-department targeting.

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
  - Added Admin order operations page with status transition controls and shipping carrier/tracking input.
  - Added dashboard KPI expansion for order lifecycle timing analytics.
  - Added time-range filtering for dashboard analytics:
    - Presets: `5, 7, 15, 30, 60, 90, 180, 365` days
    - Custom date range (`startDate`, `endDate`)

- Roles, permissions and assignment updates
  - Added/extended roles: `Purchasing`, `IT`, `HR`, `Warehouse`, `Accounting`, `Courier`, `CustomerRelations`, `Staff`, `Customer`, `Admin`.
  - Seeded role users from config/env (including purchasing/it/hr/warehouse defaults).
  - Kept password policy relaxed for local development (`RequiredLength = 6`, no special constraints).
  - Added multi-department assignment capability for personnel via claims (`pehlione.department` can be stored multiple times per user).
  - Department names are now visible in HR and Admin user management screens.

- Database and migrations
  - Added and applied migration for `department_constraints`.
  - Added order status timeline/audit table for transition tracking (`order_status_logs`).
  - Synced EF Core model snapshot and runtime behavior with MySQL schema.
  - Added customer relations messages persistence.

- Workflow communication and notifications
  - Implemented stage-based department notifications for order lifecycle:
    - Accounting notified on new order
    - Warehouse notified after payment
    - Courier notified after packing/shipping
    - Purchasing notified on return pickup/delivery to seller
  - Added manual event creation from dashboard notification panel.
  - Added read/unread management and list filtering on Staff notifications page.

- Analytics and operational visibility
  - Added order lifecycle duration metrics in Admin dashboard:
    - Pending -> Paid (approval time)
    - Paid -> Shipped (pre-shipment handling)
    - Shipped -> Delivered (shipping time)
    - Pending -> Delivered (end-to-end time)
  - Added transition-level average duration chart for major workflow steps.

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
| Admin | `/Admin/Orders/Index` | Admin order list + status operations |
| Admin | `/Admin/Products/Index` | Product management |
| Admin | `/Admin/Categories/Index` | Category management |
| Admin | `/Admin/Users/Index` | User management |
| Staff | `/Staff/Home/Index` | Staff home |
| Staff | `/Staff/Purchasing/Index` | Purchasing dashboard |
| Staff | `/Staff/Purchasing/Returns` | Purchasing returns & restock operations |
| Staff | `/Staff/It/Index` | IT dashboard |
| Staff | `/Staff/Hr/Index` | HR dashboard |
| Staff | `/Staff/Warehouse/Index` | Warehouse dashboard |
| Staff | `/Staff/Warehouse/Orders` | Warehouse order status operations |
| Staff | `/Staff/Accounting/Index` | Accounting dashboard |
| Staff | `/Staff/Accounting/Orders` | Accounting order payment/refund operations |
| Staff | `/Staff/Courier/Index` | Courier dashboard |
| Staff | `/Staff/Courier/Orders` | Courier delivery/return operations |
| Staff | `/Staff/CustomerRelations/Index` | Customer relations dashboard |
| Staff | `/Staff/Inventory/Receive` | Inventory receive/decrease screen |
| Staff | `/Staff/Notifications/Index` | Department notifications |

## Next

- Add integration tests for critical role-based operations:
  - inventory increase/decrease
  - department constraint enforcement
  - admin quick-operation forms
  - customer account CRUD flows
- Expand audit trail usage to all non-order critical entities (products, categories, user role changes).
- Add pagination/filtering for customer order history and admin data grids.
- Improve localization consistency (TR/DE labels and validation messages).
