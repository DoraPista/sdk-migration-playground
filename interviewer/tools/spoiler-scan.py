"""Scans candidate-facing files for accidental spoilers.

Candidate-facing = everything except interviewer/. Flags comments and text that give the answer
away ("BUG:", "the fix is", "should be awaited"), and solution-ish words in exercise READMEs.

    python interviewer/tools/spoiler-scan.py

Exit code 1 if anything is flagged. Findings are advisory: read each one and decide.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SKIP_DIRS = {"interviewer", "bin", "obj", ".git", ".vs", "packages"}
TEXT_EXT = {".cs", ".md", ".xaml", ".csproj", ".json", ".props", ".ps1", ".editorconfig"}

# (name, regex, applies-to-extensions or None for all)
PATTERNS = [
    ("bug marker", r"(?i)\b(//|<!--|\*)\s*(BUG|FIXME|HACK|BROKEN|WRONG|XXX)\b", None),
    ("names the fix", r"(?i)\b(the fix is|to fix this|the bug is|the problem is that|the solution is|should be awaited|forgot to|missing await|this is wrong because)\b", None),
    ("spoiler comment", r"(?i)//.*\b(race condition here|not thread.safe|deadlock|memory leak|swallow(s|ed)? the exception)\b", {".cs", ".xaml"}),
    ("answer in README", r"(?i)\b(the answer is|solution:|you should use [A-Z]|correct approach is)\b", {".md"}),
]

# Deliberate, reviewed exceptions: (path fragment, pattern name)
ALLOWED = [
    # The code-review exercises quote the author's own (wrong) reasoning on purpose.
    ("15-code-review", "names the fix"),
    # Exercise titles/scenarios legitimately use the words "memory leak" / "deadlock" as symptoms.
    ("README.md", "spoiler comment"),
    # Shared test infrastructure documents its own behaviour; it names no exercise.
    ("shared/TestUtilities/Gym.TestUtilities/ScriptedHttpHandler.cs", "spoiler comment"),
    ("shared/Gym.Shared.Tests/TestUtilitiesTests.cs", "spoiler comment"),
]


def allowed(path, name):
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    return any(frag in rel and name == pat for frag, pat in ALLOWED)


def main():
    findings = []
    for base, dirs, files in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS and not d.startswith(".gym-verify")]
        for name in files:
            path = os.path.join(base, name)
            if os.path.splitext(name)[1].lower() not in TEXT_EXT:
                continue
            try:
                with open(path, encoding="utf-8") as handle:
                    lines = handle.read().split("\n")
            except (UnicodeDecodeError, OSError):
                continue
            for number, line in enumerate(lines, start=1):
                for label, pattern, exts in PATTERNS:
                    if exts and os.path.splitext(name)[1].lower() not in exts:
                        continue
                    if re.search(pattern, line) and not allowed(path, label):
                        rel = os.path.relpath(path, ROOT).replace("\\", "/")
                        findings.append(f"{rel}:{number}  [{label}]  {line.strip()[:120]}")

    for finding in findings:
        print(finding)
    print(f"\n{len(findings)} finding(s) in candidate-facing files.")
    return 1 if findings else 0


sys.exit(main())
