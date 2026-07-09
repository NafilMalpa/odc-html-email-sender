# HtmlEmailSender

An OutSystems Developer Cloud (ODC) **External Library** (custom code, .NET 10) that sends raw HTML emails via SMTP — bypassing ODC's native Email UI element, which escapes HTML and doesn't support injecting pre-built HTML strings/templates.

## Why this exists

ODC's built-in `Send Email` node requires designing the email body visually using an **Email** UI element (widgets, expressions, etc.). There's no supported way to pass a raw HTML string (e.g. a token-replaced template) and have it render as-is — the Reactive email editor escapes HTML tags instead of interpreting them.

This library exposes a `SendHtmlEmail` server action that accepts a raw HTML string and sends it via SMTP directly, giving full control over the email body.

## Architecture

- **.NET 10**, built with the [OutSystems ExternalLibraries SDK](https://www.nuget.org/packages/OutSystems.ExternalLibraries.SDK)
- **MailKit** for SMTP (not `System.Net.Mail.SmtpClient`, which is obsolete and had unreliable behavior in ODC's Lambda-based execution environment)
- Connects to the SMTP server through ODC's **Private Gateway**, via the `SECURE_GATEWAY` environment variable that ODC injects into the External Library's execution context at runtime (see [Gotchas](#gotchas) below)
- SMTP host is **not** a parameter — it's resolved automatically from `SECURE_GATEWAY`. Port, sender address/name, and SSL flag are passed in as parameters so they can be driven by ODC Configuration values per environment (DEV/TEST/PROD) without rebuilding the library

## Server Action

### `SendHtmlEmail`

| Parameter | Type | Description |
|---|---|---|
| `smtpPort` | int | SMTP port (mapped via Private Gateway / Cloud Connector tunnel) |
| `fromEmail` | string | Sender email address |
| `fromName` | string | Sender display name |
| `enableSsl` | bool | Whether to use STARTTLS |
| `to` | string | Recipient(s), comma or semicolon separated |
| `subject` | string | Email subject |
| `htmlBody` | string | Raw HTML body. Supports `src='cid:logo'` for an embedded logo (see below) |
| `cc` | string | CC recipient(s), optional |
| `bcc` | string | BCC recipient(s), optional |
| `logoBytes` | byte[] | Optional logo image, embedded inline via CID (no external image hosting needed) |

## Project structure

```
HtmlEmailSender/
├── HtmlEmailSender.csproj
├── HtmlEmailSender.cs      # Interface + implementation
├── icon.png                # Library icon shown in ODC Portal
└── .gitignore
```

## Build & package

```powershell
dotnet publish -c Release
```

Zip the **contents** of `bin\Release\net10.0\publish\` (not the folder itself — files must sit at the zip root) and upload via **ODC Portal → Integrate → External Libraries → Upload**.

## Consuming in ODC

1. Add `HtmlEmailSender` as a dependency in your module
2. Build your HTML template with `{{TokenName}}` placeholders
3. Use a chain of `Replace()` (or sequential `Assign` nodes) to substitute tokens with real values
4. Call `SendHtmlEmail` with the resulting string as `htmlBody`

## Gotchas

- **`secure-gateway` (hardcoded string) does NOT work from External Library code.** Even though it's the correct hostname for native ODC app logic (Send Email, REST calls made from the app runtime), External Libraries execute as a **separate, isolated service** (Lambda-based) from the main app. That separate execution context needs the gateway address via the **`SECURE_GATEWAY` environment variable**, injected automatically by ODC when Private Gateway is active for the stage. Read it with:
  ```csharp
  var smtpHost = Environment.GetEnvironmentVariable("SECURE_GATEWAY");
  ```
  Using the hardcoded string instead produces a low-level `SocketException: Unknown socket error` with no useful detail — this cost significant debugging time before finding the (undocumented outside one best-practices page) fix.

- **Embedded resource naming for the icon**: `IconResourceName` must match `{RootNamespace}.{filename}`, not just the filename. If your `RootNamespace` is `ODC.HtmlEmail` and the file is `icon.png`, the value must be `"ODC.HtmlEmail.icon.png"` — otherwise upload fails with `OS-ELG-MODL-05009`.

- **OutSystems Expression editor uses double quotes as string delimiters.** If pasting raw HTML into an Expression, use single quotes for HTML attributes (`style='...'` instead of `style="..."`) to avoid breaking the expression.

- **Local testing (`dotnet run` against `secure-gateway`) will not work.** Private Gateway addresses only resolve for a deployed, running app in an ODC stage — not from a local dev machine, and not from within ODC Studio's own preview/debug. The earliest point you can actually test the SMTP call is a deployed app in a stage with Private Gateway active.

## Requirements

- .NET 10 SDK
- ODC Private Gateway configured and **Active** for the target stage, tunneled to the SMTP server
- `OutSystems.ExternalLibraries.SDK` and `MailKit` NuGet packages
