#!/usr/bin/env python3
"""Normalize OpenAPI response media types for structured JSON payloads.

This script replaces response content media type `*/*` with `application/json`
for 2xx responses whose schema clearly describes a structured payload
(`object`, `array`, or composed schemas). It intentionally skips plain strings,
HTML, and no-content responses.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


HTTP_METHODS = {"get", "post", "put", "patch", "delete", "head", "options"}
STRUCTURED_TYPES = {"object", "array", "allOf", "anyOf", "oneOf"}


def infer_schema_type(schema: dict[str, Any] | None, components: dict[str, Any]) -> str | None:
    if not isinstance(schema, dict):
        return None

    ref = schema.get("$ref")
    if isinstance(ref, str) and ref.startswith("#/components/schemas/"):
        name = ref.rsplit("/", 1)[-1]
        target = components.get(name)
        if isinstance(target, dict):
            return infer_schema_type(target, components)
        return None

    schema_type = schema.get("type")
    if isinstance(schema_type, str):
        return schema_type

    if "properties" in schema:
        return "object"

    for composed in ("allOf", "anyOf", "oneOf"):
        if composed in schema:
            return composed

    return None


def should_fix_response(response: dict[str, Any], components: dict[str, Any]) -> bool:
    content = response.get("content")
    if not isinstance(content, dict):
        return False

    if "application/json" in content or any(
        isinstance(media_type, str) and media_type.endswith("+json")
        for media_type in content
    ):
        return False

    wildcard = content.get("*/*")
    if not isinstance(wildcard, dict):
        return False

    schema = wildcard.get("schema")
    schema_type = infer_schema_type(schema, components)
    return schema_type in STRUCTURED_TYPES


def fix_spec_file(path: Path, *, write: bool) -> tuple[int, list[str]]:
    with path.open() as f:
        spec = json.load(f)

    components = ((spec.get("components") or {}).get("schemas") or {})
    changed = 0
    touched_ops: list[str] = []

    for route, path_item in (spec.get("paths") or {}).items():
        if not isinstance(path_item, dict):
            continue
        for method, operation in path_item.items():
            if method not in HTTP_METHODS or not isinstance(operation, dict):
                continue
            for status_code, response in (operation.get("responses") or {}).items():
                if not (isinstance(status_code, str) and status_code.startswith("2")):
                    continue
                if not isinstance(response, dict):
                    continue
                if not should_fix_response(response, components):
                    continue

                content = response["content"]
                content["application/json"] = content.pop("*/*")
                changed += 1
                touched_ops.append(f"{method.upper()} {route} [{status_code}]")

    if changed and write:
        with path.open("w") as f:
            json.dump(spec, f, indent=2)
            f.write("\n")

    return changed, touched_ops


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "paths",
        nargs="*",
        type=Path,
        default=[Path("openapi_specs")],
        help="Files or directories to scan (default: openapi_specs)",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Report changes needed without modifying files",
    )
    args = parser.parse_args()

    files: list[Path] = []
    for p in args.paths:
        if p.is_dir():
            files.extend(sorted(p.glob("*.json")))
        elif p.is_file():
            files.append(p)

    total_changes = 0
    for file_path in files:
        changed, touched_ops = fix_spec_file(file_path, write=not args.check)
        total_changes += changed
        if changed:
            mode = "would update" if args.check else "updated"
            print(f"{file_path}: {mode} {changed} response(s)")
            for op in touched_ops:
                print(f"  - {op}")

    if args.check and total_changes:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
