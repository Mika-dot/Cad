"""Dependency-free Markdown structure checks for the scientific comparison.

Runs without downloading third-party packages. Checks structural correctness,
not pixel-perfect GitHub rendering.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parent.parent
DOCUMENTS = (
    ROOT / "MEMORY_ACCURACY_PERFORMANCE_ANALYSIS.md",
    ROOT / "README.md",
)
problems = []


def problem(path, line, message):
    problems.append(f"{path.name}:{line}: {message}")


for path in DOCUMENTS:
    if not path.exists():
        problem(path, 0, "Markdown file is missing")
        continue

    text = path.read_text(encoding="utf-8")
    if text.startswith("\ufeff"):
        problem(path, 1, "unexpected BOM")
    if "\r" in text:
        problem(path, 1, "CR line endings: use LF")
    if chr(92) + chr(96) in text:
        problem(path, 1, "escaped backtick could hide an inline code span")

    lines = text.splitlines()
    fenced = False
    math = False
    math_buffer = []
    math_start = 0

    for n, row in enumerate(lines, start=1):
        line = row.strip()

        if line.startswith(chr(96) * 3):
            if math:
                problem(path, n, "code fence started inside math block")
            fenced = not fenced
            continue
        if fenced:
            continue

        if line == "$$":
            if math:
                if not math_buffer:
                    problem(path, math_start, "empty display-math block")
                if math_buffer and (not math_buffer[0].strip() or not math_buffer[-1].strip()):
                    problem(path, n, "blank line adjacent to math delimiter")
                math_source = "\n".join(math_buffer)
                opens = len(re.findall(r"(?<!\\)\{", math_source))
                closes = len(re.findall(r"(?<!\\)\}", math_source))
                if opens != closes:
                    problem(path, math_start, f"unbalanced LaTeX braces {opens} vs {closes}")
                math_buffer = []
            else:
                math_start = n
                math_buffer = []
            math = not math
            continue

        if math:
            math_buffer.append(row)
            continue

        if line == "$":
            problem(path, n, "standalone $ instead of $$ display-math fence")
            continue

        # Remove simple inline code spans before checking math dollars.
        without_code = re.sub(r"\x60[^\x60]+\x60", "", row)
        dollars = re.findall(r"(?<!\\)\$", without_code)
        if len(dollars) % 2 != 0:
            problem(path, n, "unbalanced inline math dollars")

        # Validate local Markdown links against the actual Git repository.
        for match in re.finditer(r"(?<!!)\[[^\]]+\]\(([^)]+)\)", row):
            target = match.group(1).split("#", 1)[0]
            if not target or "://" in target or target.startswith("#") or target.startswith("mailto:"):
                continue
            link = (path.parent / target).resolve()
            if not link.is_relative_to(ROOT) or not link.exists():
                problem(path, n, f"broken relative link: {target}")

        # Check Markdown table row width consistency.
        if line.startswith("|") and line.endswith("|"):
            parts = row.count("|")
            if parts < 3:
                problem(path, n, "table row has fewer than two columns")
            if n > 1 and lines[n - 2].strip().startswith("|"):
                last = lines[n - 2].count("|")
                if last != parts:
                    problem(path, n, f"table column count changed from {last-1} to {parts-1}")

    if fenced:
        problem(path, len(lines), "unclosed Markdown code fence")
    if math:
        problem(path, math_start, "unclosed display-math block")
    if not text.endswith("\n"):
        problem(path, len(lines), "missing final newline")
    print(f"CHECKED {path.name}: {len(lines)} lines")


if problems:
    for message in problems:
        print("MARKDOWN_ERROR", message, file=sys.stderr)
    sys.exit(1)

print("MARKDOWN_STRUCTURE_PASS")
