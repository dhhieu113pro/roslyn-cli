#!/usr/bin/env python3
"""Run coverage, skill validation, packaging, and black-box skill cases."""

from pathlib import Path
import os
import subprocess
import sys
import tempfile


def run(arguments: list[str], repository: Path) -> None:
    print(f"\n> {' '.join(arguments)}", flush=True)
    subprocess.run(arguments, cwd=repository, check=True)


def main() -> int:
    repository = Path(__file__).resolve().parent.parent
    package_directory = repository / "artifacts" / "package"

    try:
        run(["dotnet", "test", "RoslynCli.slnx", "--configuration", "Release"], repository)
        run([sys.executable, "scripts/validate-skills.py"], repository)
        run(
            [
                "dotnet",
                "pack",
                "src/RoslynCli/RoslynCli.csproj",
                "--configuration",
                "Release",
                "--no-restore",
                "--output",
                str(package_directory),
            ],
            repository,
        )

        with tempfile.TemporaryDirectory(prefix="roslyn-cli-tool-") as tool_directory:
            run(
                [
                    "dotnet",
                    "tool",
                    "install",
                    "RoslynCli.Tool",
                    "--tool-path",
                    tool_directory,
                    "--add-source",
                    str(package_directory),
                ],
                repository,
            )
            executable = Path(tool_directory) / ("roslyn.exe" if os.name == "nt" else "roslyn")
            run([sys.executable, "scripts/verify-skill-cases.py", str(executable)], repository)
    except subprocess.CalledProcessError as exception:
        return exception.returncode

    print("\nAll code, coverage, package, and skill checks passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
