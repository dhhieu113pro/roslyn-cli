#!/usr/bin/env python3
"""Run the packaged CLI against the semantic skill fixture cases."""

from pathlib import Path
import json
import os
import subprocess
import sys


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: verify-skill-cases.py <roslyn-executable>", file=sys.stderr)
        return 2

    repository = Path(__file__).resolve().parent.parent
    executable_path = Path(sys.argv[1]).resolve()
    if os.name == "nt" and not executable_path.exists():
        executable_path = executable_path.with_suffix(".exe")
    executable = str(executable_path)
    fixture = repository / "samples" / "SkillFixture"
    solution = fixture / "SkillFixture.slnx"
    cases = json.loads((fixture / "skill-cases.json").read_text(encoding="utf-8"))

    for case in cases:
        process = subprocess.run(
            [
                executable,
                "symbol",
                "search",
                str(solution),
                case["pattern"],
                "--kind",
                case["kind"],
                "--format",
                "json",
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        if process.returncode != 0:
            print(f"FAILED: {case['prompt']}\n{process.stderr}", file=sys.stderr)
            return 1

        results = json.loads(process.stdout)["results"]
        expected_name = case.get("expectedQualifiedName")
        expected_count = case.get("expectedLocationCount")
        if expected_name and not any(item["qualifiedName"] == expected_name for item in results):
            print(f"FAILED: {case['prompt']} did not find {expected_name}", file=sys.stderr)
            return 1
        if expected_count is not None and len(results) != expected_count:
            print(
                f"FAILED: {case['prompt']} expected {expected_count} results, got {len(results)}",
                file=sys.stderr,
            )
            return 1

        print(f"PASS: {case['prompt']}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
