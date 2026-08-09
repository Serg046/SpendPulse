# SpendPulse

![SpendPulse](SpendPulse.png)

A self-hosted personal spending-control app built on .NET 10 (Blazor Web App + MongoDB). It syncs transactions from your bank account via [EnableBanking](https://enablebanking.com), so it works with any bank EnableBanking supports (not just the one it was originally built against), as long as you configure it for your own bank and your own EnableBanking application.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A running MongoDB instance
- An [EnableBanking](https://enablebanking.com) account with an application registered in their control panel

## 1. Set up EnableBanking

1. Create an account at EnableBanking and register a new application in their control panel.
2. Generate an RSA key pair and upload the **public** key to your EnableBanking application. Keep the **private** key file (PEM format) — you'll point the app at its file path, not paste its contents into config.
3. Note the **Application ID** EnableBanking assigns to your application (a UUID) — there's no UI in this app to enter it, it goes directly into MongoDB (step 3 below).
4. Set the application's redirect URL in EnableBanking's control panel to match the base URL where you'll run this app (e.g. `https://your-domain.example/`) — it must match `EnableBanking:RedirectUrl` in config exactly.
5. Find your bank in EnableBanking's ASPSP directory for your country: `GET https://api.enablebanking.com/aspsps?country=<YOUR_COUNTRY_CODE>`. Note the bank's name (or a distinctive substring of it) — the app searches this list itself and picks the first name match.

## 2. Configure the app

Edit `SpendPulse.Server/appsettings.json` (or override via environment variables / `appsettings.Production.json`):

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "SpendPulse"
  },
  "EnableBanking": {
    "PrivateKey": "/path/to/your/private-key.pem",
    "AspspCountry": "XX",
    "AspspNameContains": "YourBankName",
    "RedirectUrl": "https://your-domain.example/",
    "TokenLifetimeDays": 90
  },
  "Auth": {
    "Users": [
      { "Username": "you", "Password": "choose-a-password", "IsAdmin": true }
    ]
  }
}
```

- **MongoDb** — connection to your own MongoDB instance.
- **EnableBanking.PrivateKey** — filesystem path to the PEM private key from step 1.2.
- **EnableBanking.AspspCountry** / **AspspNameContains** — country code and bank-name substring from step 1.5. These select your bank; the first matching name is used.
- **EnableBanking.RedirectUrl** — must match what you set in EnableBanking's control panel (step 1.4).
- **Auth.Users** — accounts for logging into the app itself (unrelated to your bank login). Passwords are stored in plaintext in config, so keep this file private. At least one user needs `IsAdmin: true` to be able to trigger bank sync.

## 3. Create the settings document in MongoDB

There's no setup UI for this — insert it once by hand via `mongosh`:

```js
use SpendPulse
db.settings.insertOne({
  bankSession: {
    accountId: "",
    applicationId: "<your-enablebanking-application-id>",
    lastTokenUpdate: new Date(0),
    lastDataUpdate: new Date(0)
  }
})
```

`accountId` is filled in automatically the first time you complete the bank-linking flow below.

## 4. Run it

```bash
dotnet run --project SpendPulse.Server/SpendPulse.Server.csproj
```

Then:

1. Log in with the user you configured in `Auth.Users`.
2. As an admin, use the "Refresh Enable Banking token" button in the status bar — this redirects you to your bank to authorize access (a PSD2 consent flow), then back to `RedirectUrl` to complete the link.
3. Use the "Sync" button to pull transactions.

## Deploying

A `Dockerfile`, `werf.yaml`, and Helm charts under `.helm/` are included, but they reflect the author's own opinionated Kubernetes/werf deployment pipeline (tied to his own cluster and CI secrets) rather than a turnkey setup for everyone. For your own deployment, it's simplest to build the provided `Dockerfile` yourself and supply your own `MongoDb`, `EnableBanking`, and `Auth` configuration directly (via `appsettings.json` or environment variables), rather than relying on the CI-specific secret-injection step in that Dockerfile.

If you do use the Helm chart as-is: the app Deployment mounts `/etc/keys` from the **k8s node's own filesystem** (a `hostPath` volume) into the container at the same path, since `EnableBanking:PrivateKey` is a file path, not raw key content baked into config. Before deploying, place your private key file at `/etc/keys/` on the node itself (this only works cleanly on a single-node cluster — a `hostPath` volume ties the pod to whichever specific node has the file). Forgetting this step fails with `DirectoryNotFoundException: Could not find a part of the path '/etc/keys/...'` the moment bank sync runs.

The chart also includes a `spendpulse-sync-cronjob` (hourly, `0 * * * *`) that triggers `/api/sync-status/sync` via a plain `curl` container hitting the in-cluster `spendpulse-service` directly, authenticated with HTTP Basic auth (see `BasicAuthenticationHandler` in `SpendPulse.Server/Authentication/`). No dedicated service account or Kubernetes Secret is needed: the CI pipeline (`prod.yml`) picks the first `IsAdmin: true` entry out of your existing `AUTH` secret's JSON with `jq` and passes its username/password to `werf converge` as Helm values (`cronUsername`/`cronPassword`), which the CronJob template inlines directly as env vars on its container. This does mean the cron job authenticates as whichever admin user happens to be first in that list, and its credential is only as fresh as the last deploy — if you ever rotate that admin's password, redeploy so the CronJob picks up the change too.
