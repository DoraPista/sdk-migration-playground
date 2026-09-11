# Questions Worth Asking in Any Exercise

A short list to fall back on when an exercise is going too smoothly, too slowly, or in an unexpected
direction. They work across categories; the exercise-specific follow-ups are in each `SOLUTION.md`.

## To open

- "Read the failing test out loud and tell me what it is claiming."
- "Before you change anything — what do you think is happening, and how confident are you?"
- "What would you look at first if this arrived as a support ticket at 4 pm on a Friday?"

## When they are stuck

Use in order; note the level you reached.

1. **Narrow the area.** "Which of these three methods would you rule out first, and why?"
2. **Point at the mechanism.** "Walk me through what happens on the second call."
3. **Name the tool.** "What would let you control time in this test?"

If a level-3 hint does not land, move on. A candidate who spends twenty minutes stuck gives you less
information than one who tried two exercises.

## When they are going too fast

- "What breaks if there are 500 of these instead of 5?"
- "What happens if that request succeeds but the response never comes back?"
- "Which of these would you catch in review, and which would you only find in production?"
- "What is the test you would write that you have not written?"

## When they have fixed it

- "What is still wrong with this code that the tests do not check?"
- "How would you stop this bug coming back next quarter?"
- "If you had two more hours, what would you do with them? If you had two days?"
- "Which part of your change are you least sure about?"

## When they have gone the wrong way

Let it run a little first — the recovery is informative.

- "Talk me through why you chose that over the alternative."
- "What would you expect to see if that were the cause? Is that what you are seeing?"
- "If I told you the fix is in a file you have not opened, where would you look next?"

## To find the ceiling

- "Where does this design stop working — what scale, what network, what customer?"
- "You ship this and a customer hits a problem you cannot reproduce. What did you build to make that
  survivable?"
- "What would you say to the person whose code this is?"
- "Which decision here is the expensive one to reverse?"

## To close the session

- "Which of these problems was most like something you have actually dealt with?"
- "What did you want to look up that you did not?"
- "If you joined the team on Monday and owned this SDK, what would you change first?"

## Things to avoid asking

- Trivia with a single right answer ("what is the default `HttpClient` timeout?"). If it matters,
  let them look it up and watch how they use it.
- Questions that test whether they have read the same blog post as you.
- "Is this thread-safe?" without a scenario — it invites a guess. Ask "two users click Start at the same
  moment; walk me through it" instead.
- Anything that only rewards memorising an API surface. The job needs judgement, not recall.
