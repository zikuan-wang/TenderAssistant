# TenderAssistant

TenderAssistant is now a local-only WPF side helper for bid document work. It has no backend server, no API service, and no network sync flow.

## Solution Layout

- `src/TenderAssistant.Client`: WPF client, vertical side-tool UI.
- `src/TenderAssistant.Authorizer`: offline authorization file generator.
- `src/TenderAssistant.Licensing`: shared offline license request, signing, and validation models.

## Local Development

Requirements:

- Windows 10/11
- .NET SDK 8.0.420 or later
- Microsoft Word or WPS for document insertion

Common commands:

```powershell
dotnet restore TenderAssistant.sln
dotnet build TenderAssistant.sln
dotnet run --project .\src\TenderAssistant.Client\TenderAssistant.Client.csproj
dotnet run --project .\src\TenderAssistant.Authorizer\TenderAssistant.Authorizer.csproj
```

## Offline Authorization Flow

1. Open the client and go to `设置`.
2. Export the offline authorization request file.
3. Open `TenderAssistant.Authorizer` and import the request file.
4. Select the authorization expiry date and generate the activation file.
5. Import the activation file back into the client.

## File Library

The bid-assist file library is local only. Set its location in `设置与日志`; the client creates `technical`, `business`, `qualification`, and `custom` folders under that location.
