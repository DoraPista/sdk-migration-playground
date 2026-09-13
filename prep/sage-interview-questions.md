# Sage Interview Questions

Questions a Sage interviewer could realistically ask for this role. They are built around what the project
probably is (see [sage-context.md](sage-context.md)): an SDK, used by Sage's desktop software, that moves a
customer's Sage 50 company data onto Sage's Azure-hosted platform.

**How to use them**

- Answer out loud: about a minute for most questions, two or three for the design questions.
- Questions marked ❓ are deliberately open. Start with a clarifying question, as you would in the room.
- Then read `interviewer/prep/sage-interview-notes.md`. It says what each question is probing, what a strong
  answer covers, and which gym exercise practises it.

## What Sage's interviews look like

According to candidates' reports on Glassdoor (not official), Sage's process usually involves:

- a technical test (Codewars-style problems of varying difficulty),
- a competency interview ("tell me about a time…"),
- technical questions on async, HTTP and REST, SOLID, design patterns and architecture.

Sage's values are usually given as **Human**, **Trust** and **Bold**, sometimes with **Simplify**. Expect
competency questions aimed at them.

---

## A. The SDK itself

- **A1.** ❓ "Our desktop products run on .NET Framework 4.7.2, and we'd like a newer app, maybe MAUI, to use the
  same SDK. How would you structure it?"
- **A2.** "Show me the public API a desktop developer would call to start a migration, follow its progress and
  cancel it. Write the calling code first."
- **A3.** "Should the SDK throw exceptions or return results? Where do you draw the line?"
- **A4.** "The SDK needs version 8 of a JSON library, but Sage 50 already loads an older version. What happens
  at runtime, and what can you do about it?"
- **A5.** "Customers update Sage 50 on their own schedule. How do you release new SDK versions without breaking
  older desktop builds, or the server they talk to?"
- **A6.** "How should the SDK do logging? The desktop product already has its own."
- **A7.** "What belongs in the SDK and what belongs in the desktop app? Where does the UI go? Where are
  credentials kept?"
- **A8.** "How would you test an SDK that talks to three cloud services, without calling them in every test?"

## B. The migration workflow

- **B1.** ❓ "Design the migration of one company from Sage 50 desktop to the hosted platform. What are the stages,
  and what does the SDK remember between them?"
- **B2.** "The accountant closes Sage 50 at 60 %. What happens when they open it tomorrow?"
- **B3.** ❓ "Sage 50 is often multi-user, with the data on a shared network drive. Other users are still posting
  invoices while you migrate. What do you do?"
- **B4.** "What would you check before a migration starts? Give three checks that are specific to accounting data."
- **B5.** "The migration has finished. How do we prove the data arrived complete and correct?"
- **B6.** "Verification fails after a four-hour upload. What does the customer see, and what can they do next?"
- **B7.** "Product now wants attachments and custom report layouts migrated too. How do you add that without
  rewriting the workflow?"
- **B8.** "One company has twelve years of history and 30 GB of data. What changes compared with a 200 MB company?"

## C. Moving the files

- **C1.** "How would you upload a 20 GB company file from an office on ordinary broadband?"
- **C2.** "The upload dies at 80 %. Walk me through what happens next."
- **C3.** "On different occasions the platform answers 400, 401, 403, 409, 429 and 503. What does the SDK do for
  each?"
- **C4.** "The upload request timed out. The server may or may not have stored the file. What now?"
- **C5.** "How do you know the file that arrived is exactly the file that left?"
- **C6.** "Marketing emails 50,000 customers on a Monday, and thousands start migrating at 9:00. What can go wrong
  on our servers, and what should the SDK do about it?"
- **C7.** "The laptop goes to sleep in the middle of an upload."
- **C8.** "One customer's uploads fail with certificate errors. They are behind a corporate proxy that inspects
  TLS traffic. What do you do, and what must you never do?"

## D. Sign-in and provisioning

- **D1.** "Customers sign in with their Sage account. How should a desktop SDK authenticate them against our APIs?"
- **D2.** "A migration takes six hours and an access token lasts one. Eight uploads are in progress when it expires."
- **D3.** "Where do you keep tokens on the customer's PC?"
- **D4.** "Creating the customer's hosted environment takes five to fifteen minutes. How does the SDK deal with
  that, and what does the user see?"
- **D5.** "Provisioning returns 409: the environment already exists. What does that tell you?"
- **D6.** "The data contains personal and financial details of customers and employees. What does that change
  about how you build the SDK, and about what it logs?"

## E. Inside the desktop app

- **E1.** "Sage 50 freezes when the Migrate button calls the SDK. Where do you look first?"
- **E2.** "How would you show smooth progress for a long SDK operation in a WPF window?"
- **E3.** "An exception on one of the SDK's background threads crashes Sage 50. How do you make sure the SDK can
  never do that?"
- **E4.** "The Cancel button needs to stop an upload that is halfway through a large chunk."
- **E5.** "Memory grows every time a migration runs in the same session. How do you investigate?"
- **E6.** "What would be different if the host were a MAUI app instead of WPF?"

## F. Accounting data

- **F1.** "Why must money be `decimal`? What would you check when amounts move between two systems?"
- **F2.** "What can go wrong with dates when moving accounting data: time zones, financial years, VAT periods?"
- **F3.** "Customer names contain £, é and ß, and older data was saved with a Windows code page. What might break?"
- **F4.** "How would you migrate graphical assets such as logos, scanned receipts and custom invoice layouts?
  What would you validate?"

## G. Azure

- **G1.** "Should the SDK upload straight to Blob Storage, or through our API?"
- **G2.** "Where do SAS tokens fit into that, and how long should they last?"
- **G3.** "Where does migration state live: on the PC, in the cloud, or both? Which one wins when they disagree?"
- **G4.** "Once the upload finishes, restoring the company takes twenty minutes on the server. How would you build
  that part of the platform?"
- **G5.** "How would the team know, from the office, that migrations are failing on customers' machines?"

## H. You (competency questions)

Answer these from your own experience; there are no model answers. The value each question tends to probe is in
brackets. Prepare them with [story-bank.md](story-bank.md).

- **H1.** "Tell me about something you built that runs inside another application. What was hardest about that?" *(Trust)*
- **H2.** "Tell me about a migration you worked on, such as moving a product to a new framework. How did you avoid
  breaking things for users?" *(Trust)*
- **H3.** "Tell me about a reusable component or library other developers used. How did you get them to adopt it?" *(Human)*
- **H4.** "Tell me about a production problem you couldn't reproduce on your own machine."
- **H5.** "Tell me about a disagreement in a code review. How did it end?" *(Human)*
- **H6.** "Tell me about a time you took a risk or proposed something new." *(Bold)*
- **H7.** "You haven't used MAUI in production. How would you get productive?" *(Bold)*
- **H8.** "Tell me about something complicated that became simpler because of you." *(Simplify)*
- **H9.** "Why Sage? What do you know about our products?"
- **H10.** "What would you want to understand in your first two weeks?"

---

## Likely live-coding tasks

These combine the technical test candidates report with this project. Practise them without AI tools and
without IntelliSense at least once.

| Task | Practise with |
|---|---|
| Group transactions by account, total them, and find duplicates (money as `decimal`) | [01-01](../exercises/01-csharp/01-01-project-totals-wrong/) |
| A retry helper that retries only what should be retried, with backoff and cancellation | [08-01](../exercises/08-reliability/08-01-transient-server-errors/), [13-02](../exercises/13-testing/13-02-testing-retries-and-cancellation/) |
| Read a large file in chunks, hash it and report progress, without loading it all into memory | [07-01](../exercises/07-file-transfer/07-01-large-file-upload/) |
| Fix a UI freeze caused by blocking on async code | [02-04](../exercises/02-debugging/02-04-desktop-app-hangs-on-connect/) |
| Run many uploads with a concurrency limit | [03-01](../exercises/03-async/03-01-five-hundred-files/) |
| Model a migration's states and transitions | [10-01](../exercises/10-migration/10-01-migration-workflow-states/) |
| Codewars-style string and collection problems | A few C# katas around 6–5 kyu on codewars.com |
