#!/usr/bin/env python3
"""Check `openapi_specs/` against the provenance recorded in spec_sources.yaml.

The specs here are copies of files their services publish. `spec_sources.yaml`
records where each came from and the sha256 of both sides at the last check, so
a known divergence can be told apart from a new one.

    uv run python verify_spec_sources.py            # offline: manifest vs local files
    uv run python verify_spec_sources.py --fetch    # also compare against upstream
    uv run python verify_spec_sources.py --update   # rewrite the manifest from upstream
    uv run python verify_spec_sources.py --refresh  # overwrite the specs from upstream, then re-record

The offline check needs no network and is what `tests/unit/test_spec_sources.py`
enforces. `--fetch` downloads each recorded URL; they serve anonymously, so no
credentials are required.

Never resolve a difference by editing a spec. Refresh it from its recorded
`url`, and put any local correction in a generation-time patcher -- see
docs/development.md.
"""

from __future__ import annotations

import argparse
import hashlib
import sys
import textwrap
import urllib.request
from pathlib import Path
from typing import Any

import yaml

_REPO_ROOT = Path(__file__).resolve().parent
MANIFEST = _REPO_ROOT / "spec_sources.yaml"
SPECS_DIR = _REPO_ROOT / "openapi_specs"
SPEC_EXTENSIONS = {".json", ".yaml", ".yml"}
VALID_STATES = {"identical", "differs"}


def load_manifest() -> dict[str, Any]:
    return yaml.safe_load(MANIFEST.read_text(encoding="utf-8"))


def local_spec_files() -> list[Path]:
    """Every spec on disk, as a path relative to ``SPECS_DIR``.

    Specs live one per service directory under a generic name
    (``crs_catalog/openapi.yaml``), so the directory is what identifies them;
    the bare filename is the same for all of them.
    """
    return sorted(
        p.relative_to(SPECS_DIR)
        for p in SPECS_DIR.rglob("openapi.*")
        if p.is_file() and p.suffix.lower() in SPEC_EXTENSIONS
    )


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def fetch(url: str) -> bytes:
    with urllib.request.urlopen(url, timeout=60) as resp:  # noqa: S310
        return resp.read()


def check_offline(manifest: dict[str, Any]) -> list[str]:
    """Manifest must describe exactly the specs on disk, with current hashes."""
    problems: list[str] = []
    entries = manifest.get("specs") or []

    recorded = [e.get("spec") for e in entries]
    on_disk = [str(p.as_posix()) for p in local_spec_files()]

    for name in sorted(set(on_disk) - set(recorded)):
        problems.append(f"{name}: present in openapi_specs/ but not recorded in spec_sources.yaml")
    for name in sorted(set(recorded) - set(on_disk)):
        problems.append(f"{name}: recorded in spec_sources.yaml but missing from openapi_specs/")

    dupes = {n for n in recorded if recorded.count(n) > 1}
    for name in sorted(dupes):
        problems.append(f"{name}: recorded more than once")

    for e in entries:
        name = e.get("spec", "<unnamed>")
        for field in ("service", "spec", "project", "path", "ref", "url",
                      "state", "upstream_sha256", "local_sha256", "verified"):
            if not e.get(field):
                problems.append(f"{name}: missing field '{field}'")
        if e.get("state") not in VALID_STATES:
            problems.append(f"{name}: state must be one of {sorted(VALID_STATES)}, got {e.get('state')!r}")
        for field in ("upstream_sha256", "local_sha256"):
            v = e.get(field, "")
            if not (isinstance(v, str) and len(v) == 64 and all(c in "0123456789abcdef" for c in v)):
                problems.append(f"{name}: {field} is not a sha256 digest")
        if e.get("state") == "identical" and e.get("upstream_sha256") != e.get("local_sha256"):
            problems.append(f"{name}: state is 'identical' but the two hashes differ")
        if e.get("state") == "differs" and not e.get("note"):
            problems.append(f"{name}: state is 'differs' but no note explains why")
        if e.get("state") == "identical" and e.get("note"):
            problems.append(
                f"{name}: state is 'identical' but still carries a divergence note "
                f"(run --update to clear it)"
            )

        path = SPECS_DIR / name
        if path.is_file() and e.get("local_sha256") != sha256(path.read_bytes()):
            problems.append(
                f"{name}: file has changed since it was recorded "
                f"(run --update to re-record its provenance)"
            )
    return problems


def check_upstream(manifest: dict[str, Any]) -> list[str]:
    """Compare each recorded URL against what it served at the last check."""
    problems: list[str] = []
    for e in manifest.get("specs") or []:
        name = e["spec"]
        try:
            up = fetch(e["url"])
        except Exception as exc:  # noqa: BLE001
            problems.append(f"{name}: could not fetch {e['url']} ({exc})")
            continue
        up_hash = sha256(up)
        local = (SPECS_DIR / name).read_bytes()
        now = "identical" if up == local else "differs"

        if up_hash != e["upstream_sha256"]:
            problems.append(f"{name}: upstream has changed since {e['verified']} — refresh from {e['url']}")
        if now != e["state"]:
            problems.append(f"{name}: recorded state is '{e['state']}' but it is now '{now}'")
    return problems


def update(manifest: dict[str, Any]) -> int:
    """Rewrite the manifest with freshly fetched hashes and states."""
    import datetime

    today = datetime.date.today().isoformat()
    entries = sorted(manifest.get("specs") or [], key=lambda e: e["service"].lower())
    for e in entries:
        up = fetch(e["url"])
        local = (SPECS_DIR / e["spec"]).read_bytes()
        e["upstream_sha256"] = sha256(up)
        e["local_sha256"] = sha256(local)
        e["state"] = "identical" if up == local else "differs"
        e["verified"] = today
        if e["state"] == "identical":
            # A note explains a divergence; once there is none it is stale.
            e.pop("note", None)

    header = MANIFEST.read_text(encoding="utf-8").split("version: 1")[0]
    out = [header + f'version: 1\nverified: "{today}"\n\nspecs:\n']
    blocks = []
    for e in entries:
        lines = [f'  - service: {e["service"]}', f'    spec: {e["spec"]}',
                 f'    project: {e["project"]}', f'    path: {e["path"]}',
                 f'    ref: {e["ref"]}', f'    url: {e["url"]}',
                 f'    state: {e["state"]}',
                 f'    upstream_sha256: {e["upstream_sha256"]}',
                 f'    local_sha256: {e["local_sha256"]}',
                 f'    verified: "{e["verified"]}"']
        if e.get("note"):
            lines.append("    note: >-")
            lines += [f"      {w}" for w in textwrap.wrap(str(e["note"]).strip(), 74)]
        blocks.append("\n".join(lines))
    MANIFEST.write_text(out[0] + "\n\n".join(blocks) + "\n", encoding="utf-8")
    print(f"Updated {MANIFEST.name}: {len(entries)} specs, verified {today}")
    return 0


def refresh(manifest: dict[str, Any]) -> int:
    """Overwrite each local spec with the file its service publishes.

    `--update` only re-records hashes, which turns a real divergence into a
    recorded one. This is the other half: bring the copies back in line, then
    re-record. The manifest is left untouched when nothing moved, so a
    scheduled run that finds no drift produces no diff at all.
    """
    changed: list[str] = []
    for e in manifest.get("specs") or []:
        upstream = fetch(e["url"])
        path = SPECS_DIR / e["spec"]
        if path.read_bytes() == upstream:
            continue
        path.write_bytes(upstream)
        changed.append(e["service"])

    if not changed:
        print("Every spec already matches upstream; nothing refreshed.")
        return 0

    print(f"Refreshed {len(changed)} spec(s) from upstream: {', '.join(changed)}")
    print("Regenerate and run the tests before committing -- an upstream change")
    print("can add, remove or reshape operations on the generated client.")
    return update(manifest)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fetch", action="store_true",
                        help="Also compare each spec against its recorded upstream URL")
    parser.add_argument("--update", action="store_true",
                        help="Rewrite the manifest with freshly fetched hashes and states")
    parser.add_argument("--refresh", action="store_true",
                        help="Overwrite the specs from upstream, then re-record the manifest")
    args = parser.parse_args()

    manifest = load_manifest()
    if args.refresh:
        return refresh(manifest)
    if args.update:
        return update(manifest)

    problems = check_offline(manifest)
    if args.fetch and not problems:
        problems += check_upstream(manifest)

    if problems:
        print(f"{len(problems)} problem(s):")
        for p in problems:
            print(f"  - {p}")
        return 1

    n = len(manifest.get("specs") or [])
    scope = "against upstream" if args.fetch else "against local files"
    print(f"All {n} specs consistent with spec_sources.yaml ({scope}).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
