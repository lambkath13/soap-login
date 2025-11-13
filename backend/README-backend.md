# Backend (Minimal API + SOAP proxy)

## Run
```bash
dotnet restore
dotnet run --urls http://localhost:5000
```

## Endpoints
- `POST /api/auth/login` — body: `{ "username": "...", "password": "..." }`
- `POST /api/auth/register` — body: `{ "username": "...", "password": "...", "email":"...", "firstName":"...", "lastName":"..." }`

## Notes
- SOAPAction auto-guess for legacy ISAPI is implemented.
- If server returns a SOAP Fault with expected tag names, update `SoapEnvelopeTemplates` accordingly (e.g., `ol_Username` / `ol_Password`).
