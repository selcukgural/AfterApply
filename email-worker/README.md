# e-kariyerim Email Worker

Cloudflare Email Worker that receives mail forwarded by users (via a Gmail filter they set up
themselves) to their personal `<token>@application.ekariyerim.com` address, and relays it to the
backend's `POST /api/email-forwarding/inbound` endpoint. Not part of the .NET solution — deployed
independently via Wrangler.

Manual, documented deploy — not CI-automated (matches this project's bootstrap-stage approach to
other one-off infra).

## One-time setup

1. `npm install`
2. `wrangler login`
3. `wrangler secret put INBOUND_WEBHOOK_SECRET`
   Paste the same value configured in the backend as `EmailForwarding:WebhookSecret`
   (`dotnet user-secrets set "EmailForwarding:WebhookSecret" "<value>" --project src/AfterApply.Api`
   locally, or the production secret store).
4. If the backend URL differs from `wrangler.toml`'s `INBOUND_WEBHOOK_URL`, edit that value there
   (it's a plain var, not a secret).

## Deploy

```
npm run deploy
```

## Attach to the domain (Cloudflare dashboard, one-time)

Email Routing must already be enabled on the `ekariyerim.com` zone (it is). In
**Email Routing → Routing rules → Create routing rule**:
- Email pattern: `*` (catch-all) @ `application.ekariyerim.com`
- Action: Send to a Worker → `ekariyerim-email-worker`

## Verify

`npm run tail` streams live logs while sending a test email to any
`<anything>@application.ekariyerim.com` address — confirms the Worker fires and see the
`console.error` output if the webhook call fails (wrong secret, backend down, etc.).
