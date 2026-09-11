# Exercise 09-02 – Review: MigrationManager

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- code review and communication
- API design for a reusable DLL
- spotting risk: correctness, security, threading, maintainability

## Scenario

Our company acquired a competitor. Their product does roughly what we do, and their migration engine
ships as a DLL (`Contoso.Migration`) used by their WPF desktop app and by two partner integrations.

Management wants to know whether we can adopt it as the basis of **our** SDK, which has to serve WPF,
.NET MAUI and partner tools.

`src/` contains the parts of their public API that matter. It compiles, it works, and their desktop
app ships with it.

Their WPF app uses it like this:

```csharp
MigrationConfig.ApiUrl = Settings.Default.ApiUrl;
MigrationConfig.ClientSecret = Settings.Default.Secret;
MigrationConfig.UiDispatcher = Dispatcher.CurrentDispatcher;

MigrationManager.Instance.OnStatus = s => StatusText = s;
MigrationManager.Instance.OnProgress = p => ProgressBar.Value = p;
MigrationManager.Instance.OnError = e => MessageBox.Show(e.Message);
MigrationManager.Instance.Start(@"\\fs01\projects\northwind");
```

## Your Task

Review this API as if it were a pull request you have to approve or reject, for this purpose.

1. What are the problems? Group them and say which ones would block adoption.
2. For the three you consider worst, explain what could happen in production.
3. Sketch what the public API should look like instead. You don't have to write the implementation.

You are not expected to rewrite the code.

## Constraints

- You are not asked to rewrite the class. One or two lines of suggested code per finding is plenty.
- Time-box the reading to ten minutes, then talk.
- The author is a colleague, not a defendant. Phrase the comments as you would to them.

## How to Run

```bash
dotnet build exercises/09-sdk-design/09-02-migration-manager-review/src
```

(You can also just read it. It builds on Windows because it references WPF.)

## What to Produce

A prioritised list: what blocks the merge, what you would raise without blocking, and the one comment you
would leave if you could only leave one.

## When You're Done

You can give a clear recommendation, with reasons a non-technical manager would understand and a
design a developer could act on.
