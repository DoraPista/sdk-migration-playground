# Sage Context

What is publicly known about the project's likely background, what is only a reasonable guess, and how to use
both in an interview without overclaiming. Researched in September 2026. Check anything you plan to say out loud.

## Publicly known

- **Sage 50 (US edition) has a cloud-hosted edition.** Customers work in it through Microsoft's *Windows App*,
  the client for Azure Virtual Desktop. Sage describes it as built on Azure security standards.
- **Moving a company there is manual today:**
  1. Create a backup in the desktop version of Sage 50.
  2. Copy the backup file to the *G: drive* (the Backups folder) in the hosted session.
  3. In the hosted Sage 50: *File → Restore*, then *Create a new company using the restored data*.
  4. Choose what to restore: company data, customized forms, web transactions.
  5. Some settings are not supported in the cloud. For example, *SmartPosting* is unavailable, so the posting
     method should be set to real-time.
- **In the UK, Sage ships a migration tool** (Sage 50 Accounts → Sage Accounting). It includes a validation
  routine that checks the data will convert correctly before the migration runs.
- **The desktop products are .NET Framework applications.** Published system requirements list .NET Framework
  4.7.2 for Sage 200 Professional and Sage 50 (Sage 50 also needs 3.5). Sage's developer SDK for Sage 50 US ships
  .NET and COM samples.

## Probably true (a guess, not confirmed)

The "Migration Agent SDK" in the job ad most likely **automates the manual process above**. It would be a
DLL used by the desktop product (or by a small agent app) that:

- signs the customer in (the authentication service in the ad),
- creates or finds their cloud environment (the provisioning service),
- takes a backup and runs pre-migration checks (the "validation" in the ad, e.g. the posting method),
- uploads large files reliably, with resume, checksums and retries,
- starts and monitors the restore, and reports progress to the desktop UI,
- recovers from crashes and from results it cannot be sure about.

That would explain the requirements:

- **.NET Standard 2.0:** one DLL that loads into .NET Framework 4.7.2 hosts *and* modern .NET apps.
- **WPF:** the existing desktop UI.
- **MAUI:** perhaps a new agent app. Worth asking.
- **"Graphical assets":** possibly customized forms, logos and report layouts. Also worth asking.

**In the interview, say it as a question, not a fact:** *"I imagine the SDK automates the
backup-upload-restore flow — is that close?"* It shows you researched the company, and it is easy to correct.

## Things to be able to discuss

| Topic | Why it matters here | Practise with |
|---|---|---|
| Running inside someone else's process | You cannot change the host's config file, its binding redirects or its dependency versions. Keep dependencies minimal, never crash the host from a background thread, don't capture the UI thread, don't change global settings | 09-04, 02-04, 09-03 |
| .NET Framework HTTP defaults | `ServicePointManager.DefaultConnectionLimit` is 2 per server in desktop .NET Framework apps, so parallel uploads can be capped silently. The TLS version depends on the host's settings, not on your DLL | 03-01, 09-04 |
| One very large file | Streaming, chunking, resume, hashing, memory | 07-01, 07-02 |
| Restoring twice | Idempotency keys, and the server as the source of truth | 10-04, 07-03 |
| Accounting data integrity | Money is `decimal`, never `double`. Totals must match between source and destination. A duplicated transaction is a financial error, not a cosmetic one | 01-01, 10-01 |
| Signing in from a desktop app | A desktop app cannot keep a secret. Public-client flows (authorization code with PKCE, in the system browser) and secure token storage | 06-01, 06-02 |
| Customer networks | Proxies, TLS inspection, flaky links, laptops going to sleep | 16-03 |
| Supporting it after release | Logs without secrets, correlation IDs, a support bundle a customer can send | 06-02 |

## Words worth knowing

**Company (data file)**: one business's accounting data. **Backup / restore**: how Sage 50 data moves
between machines. **Posting**: recording transactions into the ledgers (real-time or batch). **Customized
forms**: invoice and report layouts a customer designed. **General ledger / chart of accounts**: the core
accounting records. **Fiscal year / period**: accounting periods, which are often closed. **Audit trail**: a
record of who changed what. **AR / AP**: accounts receivable (money owed to you) and payable (money you owe).

## Questions to ask Sage

- Which product and which customers is the SDK for first?
- Where does it run: inside the existing desktop product, or as a separate agent? Which .NET versions must it load into?
- What does a migration move today, and which parts are manual that the SDK should automate?
- How is a migration validated? Who decides it was correct?
- What happens today when a migration fails halfway?
- How is the SDK shipped and updated on customer machines?
- How is the team split between Sage and Brillio, and across time zones?
- What would make someone successful here in the first 90 days?

## Questions to ask Brillio

- How long is the engagement expected to last, and what happens at the end of it?
- How many Brillio people work on the Sage project, and who would I report to day to day?
- How do onboarding, equipment and access to the client's systems work?
- What are the working hours, and how much overlap is expected with the client's time zone?

## Sources

- [Sage: Migrating data from Sage 50 Desktop (Cloud customers)](https://communityhub.sage.com/us/sage50_us/sage50us-welcome-center/w/hosted-customers/4158/4-migrating-data-from-sage-50-desktop)
- [Sage: Sage 50 Accounts to Sage Accounting migration tool](https://gb-kb.sage.com/portal/app/portlets/results/view2.jsp?k2dockey=200427112448117)
- [Sage 50 U.S. Edition system requirements](https://kb.sage.com/selfservice/viewContent.do?externalId=111569&sliceId=1)
- [Sage 200 Professional system requirements (PDF)](https://cim-software.co.uk/wp-content/uploads/2023/04/Sage-200-Professional-System-Requirements-4766.pdf)
- [Sage developer community: integration with Sage 50 desktop](https://developer-community.sage.com/topic/105-integration-with-sage-50-desktop/)
