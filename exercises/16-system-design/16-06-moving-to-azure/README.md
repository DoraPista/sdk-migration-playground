# Exercise 16-06 – Moving the Platform to Azure

Difficulty: Expert Discussion
Estimated Time: 20 minutes (discussion)

## Skills

- mapping a local design onto cloud building blocks
- security models for uploads
- knowing where state lives

## Scenario

Today the mock server in `shared/MockServer` stands in for the platform: it authenticates, takes chunked
uploads, stores files on disk, tracks migration state and runs long operations behind a polling endpoint.

The platform team is moving to Azure. They have asked for your view as the SDK owner.

## Your Task

Work through how each piece maps, and what changes for the desktop SDK.

1. The file store becomes **Azure Blob Storage**. Does the desktop app upload **through** the API or
   **directly** to Blob Storage? Argue both, then choose.
2. If it uploads directly, how does it get permission — a **SAS URL**, a user delegation SAS, or something
   else? What is the lifetime, the scope, and what happens when it expires mid-upload?
3. Where does migration state live, and who owns it?
4. Would you put large migrations behind a **queue**? What does that change for the SDK's progress and
   completion reporting?
5. Where do **managed identities** fit, and where can they not help?
6. What does the SDK's retry logic have to do differently against Azure services than against your own API?

## Constraints

- No Azure subscription is needed and no Azure code is expected. This is a design discussion.
- The desktop app runs on consultant laptops, outside your network.
- Some customers will not allow direct connections to storage endpoints.

## How to Run

Nothing to build. This is a discussion: use a whiteboard, a scratch file or paper. The repository is
here for reference — point at real code when it helps your argument.

## What to Produce

A diagram of the upload path and a short answer to each of the six questions, with the trade-off you are
making in each case.

## When You're Done

You can explain the security difference between a SAS URL and a token from your own API, and say what you
would keep on your own servers no matter what.
