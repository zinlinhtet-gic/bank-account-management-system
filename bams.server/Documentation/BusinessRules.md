# Business Rules

## Accounts

- Account names are required and capped by `AccountConstants.AccountNameMaximumLength`.
- Opening balances cannot be below `AccountConstants.MinimumOpeningBalance`.
- Account types must be valid `AccountType` enum values.
- New accounts are created with `AccountStatus.Active`.
- Account numbers are generated server-side using `AccountConstants.AccountNumberPrefix`.

## Users

- Every user has a `Status`: `Active`, `Disabled` or `Deleted`; new users default to `Active`.
- A disabled or deleted user cannot log in, fetch permissions, change their password, or call any `[RequirePermission]`
  endpoint, even with an unexpired token. These requests fail with `UserAccountDisabled` or `UserAccountDeleted` (403).
  The check lives in one place: `UserStatusExtensions.EnsureCanSignIn()`.
- On login, the status check runs only after the password is verified, so account state is not revealed to callers
  who do not know the password.
- Passwords are hashed only through `Utils/Security/PasswordHasher` (login, change password, user management, seeding).
- Change password: the new password must meet the complexity rules, must not be any role default password in
  `UserConstants.DefaultPasswordsByRole` (`DefaultPasswordNotAllowed`; managers know these), and must differ from the
  current password (`NewPasswordSameAsCurrent`). The default check runs first, so retyping the default gets that message.

## User Management (`api/users`, permission `user_management`: managers)

- Managers can list, view, create, edit, reset the password of, and delete manager, officer and auditor accounts.
- The list never includes deleted users and is sorted by username. Filters: search (part of username, full name or
  email), role, and a created-date range (`createdFrom` inclusive, `createdBefore` exclusive; `from >= before` is
  `InvalidDateRange`).
- Create / update validation (`UserConstants`): full name, username, email and role are required; phone is optional.
  Full name ≤ 150 characters (the desktop form merges first and last name), username matches
  `UserConstants.UsernamePattern`, email matches `EmailPattern`, phone matches `PhonePattern`. Only roles listed in
  `UserConstants.DefaultPasswordsByRole` can be assigned (`InvalidRole` otherwise).
- Usernames and emails are unique across **all** users, including deleted ones, so a deleted user's username stays
  reserved (`UsernameAlreadyExists` / `EmailAlreadyExists`).
- New users and password resets get the role's default password (`Manager123!`, `Officer123!`, `Auditor123!`) with
  `MustChangePassword = true`, so the user must choose a new password at the next login.
- Delete is a soft delete: `Status = Deleted`, the row is kept. A deleted user who logs in with the correct password
  gets `UserAccountDeleted`; any token they still hold stops working at once.
- Online presence (the list's Status column): a user is **online** when `OnlineStatus = Active` and `LastSeenAt` is
  within `UserConstants.OnlinePresenceTimeout` (3 minutes). Login sets both; the desktop calls
  `POST api/auth/heartbeat` every minute while signed in; `POST api/auth/logout` sets `OnlineStatus = Inactive`.
  An app closed without logging out drops to offline once its heartbeats stop. The rule lives only in
  `UserMappings.IsOnline`.
- Self-delete is allowed, but the bank must keep one active manager: deleting the only active manager, or changing
  that manager's role, fails with `LastManagerCannotBeRemoved` (422).
