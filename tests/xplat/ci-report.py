#!/usr/bin/env python3
"""Turns CI results into workflow annotations, which the public API serves without signing in (job logs need a sign-in).

  ci-report.py failures <folder>...   every .trx below the folders: counts, and the failed tests with their first message line
  ci-report.py text <title>           standard input as an error annotation
  ci-report.py stacks <title>         standard input is a dotnet-stack report: only the threads in the app's own code

Reading them back: GET the job's check_run_url + /annotations.
"""
import pathlib
import sys
import xml.etree.ElementTree as ElementTree

# GitHub keeps ten annotations of a kind per step and shortens long ones.
MAX_ANNOTATIONS = 9
ANNOTATION_LENGTH = 3500
MESSAGE_LINE_LENGTH = 240
APP_FRAMES = ("GitExtensions!", "GitCommands!", "GitExtensions.Xplat", "GitUI.")
FRAMES_PER_THREAD = 30


def escape(text):
    return text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def annotate(kind, title, text):
    pieces = [text[i : i + ANNOTATION_LENGTH] for i in range(0, len(text), ANNOTATION_LENGTH)][:MAX_ANNOTATIONS]
    for number, piece in enumerate(pieces, 1):
        suffix = f" ({number}/{len(pieces)})" if len(pieces) > 1 else ""
        title_text = escape(title + suffix).replace(":", "%3A").replace(",", "%2C")
        print(f"::{kind} title={title_text}::{escape(piece)}")


def local_name(element):
    return element.tag.rsplit("}", 1)[-1]


def read_trx(path):
    """Returns the outcome counts and the failed tests of one result file."""
    counts = {}
    failed = []
    for result in ElementTree.parse(path).getroot().iter():
        if local_name(result) != "UnitTestResult":
            continue

        outcome = result.get("outcome", "Unknown")
        counts[outcome] = counts.get(outcome, 0) + 1
        if outcome == "Failed":
            message = next((e.text or "" for e in result.iter() if local_name(e) == "Message"), "")
            first_line = message.strip().splitlines()[0] if message.strip() else ""
            failed.append(f"{result.get('testName')}: {first_line[:MESSAGE_LINE_LENGTH]}")

    return counts, failed


def failures(folders):
    summary = []
    failed_lines = []
    for folder in folders:
        for path in sorted(pathlib.Path(folder).rglob("*.trx")):
            try:
                counts, failed = read_trx(path)
            except ElementTree.ParseError as error:
                summary.append(f"{path.name}: unreadable, {error}")
                continue

            summary.append(f"{path.name}: {counts}")
            failed_lines += [f"[{path.stem}] {line}" for line in failed]

    annotate("notice", "Test results", "\n".join(summary) or "No .trx file found: no suite got as far as running its tests.")
    if failed_lines:
        annotate("error", f"{len(failed_lines)} tests failed", "\n".join(failed_lines))


def app_threads(report):
    """Keeps the threads of a dotnet-stack report that run the app's own code, which is where a hang shows."""
    threads = []
    current = []
    for line in report.splitlines():
        if line.startswith("Thread") and current:
            threads.append(current)
            current = []

        current.append(line)

    threads.append(current)
    kept = [thread[:FRAMES_PER_THREAD] for thread in threads if any(frame in line for line in thread for frame in APP_FRAMES)]
    return "\n".join("\n".join(thread) for thread in kept) or report


def main(arguments):
    match arguments:
        case ["failures", *folders] if folders:
            failures(folders)
        case ["text", title]:
            annotate("error", title, sys.stdin.read())
        case ["stacks", title]:
            annotate("error", title, app_threads(sys.stdin.read()))
        case _:
            print(__doc__, file=sys.stderr)
            return 2

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
