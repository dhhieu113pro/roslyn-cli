#!/usr/bin/env python3
"""Validate the repository's minimal Codex skill contract."""

from pathlib import Path
import re
import sys


def validate(skill_dir: Path) -> list[str]:
    errors: list[str] = []
    skill_file = skill_dir / "SKILL.md"
    agent_file = skill_dir / "agents" / "openai.yaml"

    if not skill_file.is_file():
        return [f"{skill_dir}: missing SKILL.md"]
    if not agent_file.is_file():
        errors.append(f"{skill_dir}: missing agents/openai.yaml")

    text = skill_file.read_text(encoding="utf-8")
    match = re.match(r"^---\n(.*?)\n---\n", text, re.DOTALL)
    if not match:
        errors.append(f"{skill_file}: invalid YAML frontmatter delimiters")
        return errors

    frontmatter = match.group(1)
    name_match = re.search(r"^name:\s*(.+)$", frontmatter, re.MULTILINE)
    description_match = re.search(r"^description:\s*(.+)$", frontmatter, re.MULTILINE)
    if not name_match:
        errors.append(f"{skill_file}: missing name")
    elif name_match.group(1).strip() != skill_dir.name:
        errors.append(f"{skill_file}: name must match its directory")
    elif not re.fullmatch(r"[a-z0-9-]{1,63}", skill_dir.name):
        errors.append(f"{skill_file}: invalid skill name")
    if not description_match or not description_match.group(1).strip():
        errors.append(f"{skill_file}: missing description")

    return errors


def main() -> int:
    repository = Path(__file__).resolve().parent.parent
    repository_skills = repository / ".agents" / "skills"
    plugin_skills = repository / "plugins" / "roslyn-cli" / "skills"
    skill_dirs = sorted(
        path
        for skills_dir in (repository_skills, plugin_skills)
        for path in skills_dir.iterdir()
        if path.is_dir()
    )
    errors = [error for skill_dir in skill_dirs for error in validate(skill_dir)]

    repo_skill = repository_skills / "roslyn-investigate"
    plugin_skill = plugin_skills / "roslyn-investigate"
    for relative_path in (Path("SKILL.md"), Path("agents/openai.yaml")):
        if (repo_skill / relative_path).read_bytes() != (plugin_skill / relative_path).read_bytes():
            errors.append(f"roslyn-investigate copies differ at {relative_path}")
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print(f"Validated {len(skill_dirs)} skill package(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
