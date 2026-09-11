# Exercise 01-03 – "Provisioning Failed. Please Contact Support."

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- result / error modelling
- exceptions vs return values
- HTTP status semantics
- API design for callers

## Scenario

Before a migration starts, the setup wizard *provisions* a destination for the customer in the cloud
platform (`POST /provision`, see `shared/MockServer/API.md`).

Support tickets from the last month:

1. "Provisioning failed. Please contact support." shows up for customers who **already have** a destination,
   for example after re-running the wizard. The platform team says the service answered correctly.
2. After a Wi-Fi drop, one customer's migration was created against a destination called `-1`.
3. When the platform returns a 500 the wizard crashes with an unhandled `HttpRequestException`.
4. When the customer's region isn't supported, the wizard says "Please contact support". The platform sends
   back a validation message saying exactly what's wrong.

`ProvisioningClient` returns `null`, throws, or returns magic strings depending on the situation.
`ProvisioningStep` (used by the wizard) interprets them.

## Your Task

Redesign how `ProvisioningClient` reports outcomes, so that callers can't misread them, and
update `ProvisioningStep` to match.

## Constraints

- `ProvisioningStep.RunAsync` and `ProvisioningStepResult` are used by the WPF and MAUI wizards.
  Keep their signatures.
- `ProvisioningClient` is internal to the SDK. Change it however you like.
- Cancellation requested by the user must still surface as cancellation.

## Acceptance Criteria

- An already-provisioned customer continues with their existing destination.
- A network failure never produces a destination ID.
- Transient problems (network, 429, 5xx) are reported as retryable, without crashing.
- Validation problems are reported as not retryable, with the platform's explanation.

## How to Run

```bash
dotnet test exercises/01-csharp/01-03-provisioning-results/tests
```

## When You're Done

All tests pass, and you can explain why you chose your result model over the alternatives.
