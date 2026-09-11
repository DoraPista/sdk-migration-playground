# 16-06 Moving the Platform to Azure – Discussion Notes

**Type:** system design · **Time:** 20 min

## Hints

1. "Draw where the bytes go and who holds a credential at each hop."
2. "A 40 GB upload outlives any sensible SAS lifetime. What then?"
3. "Direct upload with a short-lived, single-blob, write-only user delegation SAS issued by your API and renewed on 403, a proxy fallback for locked-down networks, and migration state in your own database."

Architecture, not certification trivia. A candidate who has never used Azure can still do well by reasoning
about who holds the credential, where the bytes go, and where state lives. Do not reward service-name bingo.

## 1. Through the API, or straight to Blob Storage?

| | Through the API | Direct to Blob |
|---|---|---|
| For | One place for auth, validation, quota, audit, virus scanning; one hostname to allow through the customer's firewall; the platform can transform or verify content | The bytes take one hop instead of two; no API servers scaled for 100 GB; cheaper; storage handles the chunking, hashing and resumption for you |
| Against | Every byte crosses your compute: bandwidth cost, timeouts, scaling API servers for file transfer, and a 40 GB request through a web tier is miserable | The client holds a credential to storage; harder to enforce rules; some customers block `*.blob.core.windows.net`; you lose the natural place to record what happened |

The good answer is **direct upload with the API in control of permission and bookkeeping**: the API issues a
scoped, short-lived SAS for one blob, the client uploads directly, then calls the API to commit. Plus a
**fallback to proxying through the API** for customers whose network blocks storage endpoints — the scenario
says some will, so a design with no fallback is incomplete.

## 2. SAS: scope, lifetime, expiry mid-upload

- **User delegation SAS** (signed with Entra ID credentials) over an account-key SAS: revocable, auditable,
  no long-lived account key on a server that mints it, max lifetime bounded.
- Scope: **one blob, write-only (`cw`), one migration**. Not container-wide, not read-write, not `*`.
- Lifetime: short (minutes to an hour). A 40 GB upload will outlive it — so the design must **renew**: the
  client asks the API for a new SAS when the old one is close to expiry or a 403 comes back, and continues
  the same block-blob upload. That is the interesting bit; ask about it if they do not raise it.
- A SAS is a **bearer credential in a URL**: it must never be logged, never put in a crash report, never
  written to the checkpoint file. (Ties directly to 15-01.)
- Stored access policies give server-side revocation; an ad-hoc SAS cannot be revoked without rotating keys.

## 3. Where migration state lives

- The **platform** owns the authoritative state: what exists, what is committed, what the file's hash is.
  The client asking "what do you have?" on resume (07-02, 16-03) only works if the server's state is the truth.
- Blob metadata/tags are convenient but weak for querying; migration state belongs in a database
  (Azure SQL or Cosmos DB) keyed by migration id, with the blob as the payload.
- The **client** keeps a local checkpoint as a cache and for offline restart, and reconciles against the server.
- Block-blob semantics help: uncommitted blocks exist until `Put Block List` commits them, so a partial
  upload is invisible to readers and expires by itself. That is a natural fit for "nothing is visible until
  the migration confirms it".

## 4. A queue for large migrations

- Yes for server-side work (validation, thumbnailing, indexing, moving between tiers) — Azure Storage Queues
  or Service Bus, with the API returning **202 + an operation id** the client polls (08-02 already models this).
- What changes for the SDK: "upload finished" no longer means "done". There is a second phase with its own
  progress, its own failures, and its own idempotency — and the client must be able to reconnect to it after a
  restart, so the operation id goes in the checkpoint.
- Watch for: at-least-once delivery (handlers must be idempotent), poison messages and a dead-letter queue,
  visibility timeouts shorter than the work, and ordering guarantees the design should not rely on.
- Durable Functions is a reasonable answer for the orchestration; ask what it buys (state, retries, fan-out)
  and what it costs (a programming model, and debugging).

## 5. Managed identities

- Perfect for **service-to-service inside Azure**: the API reading the database, minting a user delegation
  key, the queue worker talking to storage. No secrets in configuration.
- Useless for the **consultant's laptop**, which is not an Azure resource. That device authenticates
  interactively (Entra ID, MSAL, device code / auth code + PKCE) and gets a short-lived token; long-lived
  secrets do not belong on a laptop at a customer site. A candidate who proposes a managed identity for the
  desktop app has missed what a managed identity is — a useful, gentle check.
- The SDK should accept a credential abstraction rather than a token string, so the WPF app, the MAUI app
  and the overnight runner can each plug in what suits them.

## 6. Retrying against Azure

- Use the Azure SDK's own retry/pipeline rather than wrapping calls in a bespoke loop; it knows the services'
  throttling responses and supports resumable upload primitives.
- Storage throttling (`503 ServerBusy`, `500 OperationTimedOut`) has different semantics from your API's 429,
  and per-account/per-partition limits mean the fix is often *reduce concurrency*, not *retry harder*.
- `Put Block` is naturally idempotent by block id — which removes the "was it received?" problem for uploads,
  while it remains for your own API's commit call. Idempotency keys are still needed there (07-03).
- A SAS expiry looks like a 403, not a 401: the SDK must classify it separately and renew rather than fail.
- Client clock skew breaks SAS validity: use `startsOn` a few minutes in the past.

## Follow-ups

- "The customer blocks storage endpoints." (Proxy fallback — and measure what that costs you.)
- "A SAS URL ends up in a log/crash dump." (Treat as a credential leak: revoke via stored access policy or rotate the delegation key; and fix the redaction.)
- "Who deletes the half-uploaded blobs?" (Lifecycle policy for uncommitted blocks; a cleanup job for abandoned migrations — the counterpart of 13-02's abandoned sessions.)
- "What would you keep on your own servers no matter what?" (Identity, authorisation, the record of what happened, and the audit trail.)

## Level indicators

| Level | Indicators |
|---|---|
| Weak | Lists services without a data path; puts account keys on the laptop; "just use Blob Storage" |
| Solid mid-level | Direct upload with a scoped short-lived SAS issued by the API; state in a database; knows a queue means asynchronous completion |
| Strong | Handles SAS renewal mid-upload, the firewall fallback, block-blob semantics, and idempotency of queue handlers |
| Senior | Argues both sides of the upload path before choosing, keeps authorisation and audit on their own servers, and treats the SAS as a credential with a lifecycle |
