# Pehlione (.NET)

Pehlione is an ASP.NET Core MVC e-commerce project with MySQL + EF Core.

## Completed Work

- Implemented anonymous catalog/cart browsing.
- Added login-required checkout completion flow.
- Added cart variant support (color/size) and fixed cart runtime issues.
- Implemented a 4-step checkout wizard:
  1. User Information
  2. Address
  3. Payment
  4. Confirmation
- Added order persistence (`Order`, `OrderItem`) and order creation from cart.
- Added order confirmation email integration (MailHog SMTP support in development).
- Added user address persistence with German-style address fields:
  - Street + House Number
  - PLZ (postal code)
  - City, State, Country Code
  - Linked to authenticated user
- Added user payment method persistence:
  - Klarna, PayPal, Visa, Mastercard
  - Secure storage approach (no full card number, no CVV)
  - Supports display label + last4 metadata for cards
- Applied and synced migrations for new commerce/user-data models.

## Next

- Visual/UI enhancement pass across customer checkout and account flows:
  - Cleaner spacing and typography
  - Better form hierarchy and feedback states
  - More modern card/list styling
  - Improved mobile responsiveness and consistency
