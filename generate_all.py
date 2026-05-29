import json
import os
import re
import shutil
import subprocess
from pathlib import Path

import yaml

SPECS_DIR = Path("openapi_specs")
OUTPUT_DIR = Path("src/OsduCsharpClient/Generated")
SPEC_EXTENSIONS = {".json", ".yaml", ".yml"}

KIOTA = shutil.which("kiota") or os.path.expanduser("~/.dotnet/tools/kiota")


class _NoTimestampLoader(yaml.SafeLoader):
    """SafeLoader that leaves ISO date/datetime values as strings.

    OpenAPI ``example`` fields like ``2021-01-26T02:24:13.843Z`` would
    otherwise become ``datetime`` objects, which then break JSON serialization
    when we hand the spec off to Kiota.
    """


_NoTimestampLoader.yaml_implicit_resolvers = {
    k: [(tag, regexp) for tag, regexp in v if tag != "tag:yaml.org,2002:timestamp"]
    for k, v in yaml.SafeLoader.yaml_implicit_resolvers.items()
}


def _load_spec(spec_path: Path) -> dict:
    text = spec_path.read_text(encoding="utf-8")
    if spec_path.suffix.lower() in {".yaml", ".yml"}:
        return yaml.load(text, Loader=_NoTimestampLoader)
    return json.loads(text)


def to_pascal_case(name: str) -> str:
    return "".join(word.capitalize() for word in re.split(r"[_\-\s]+", name))


# Per-spec set of schema names whose ``data`` property is a generic OSDU
# free-form payload and should be emitted as a Kiota ``UntypedNode`` instead
# of an empty model class.
#
# OSDU ``data`` is polymorphic by ``kind`` (a WellLog, a Wellbore, a
# Trajectory, ... all share the same record envelope), so no single closed
# C# type can represent it. These schemas declare it as a free-form
# ``{"type": "object", "additionalProperties": true}`` (or similar
# map-of-objects in the case of merge-patch), which Kiota turns into an
# empty ``*_data`` class that cannot be used to author payloads. Replacing
# the schema with an empty one makes Kiota generate an ``UntypedNode``
# instead, which round-trips arbitrary JSON.
# See https://github.com/equinor/osdu-csharp-client/issues/38
FREEFORM_DATA_SCHEMAS: dict[str, set[str]] = {
    "wellbore_ddms": {"Record"},
    "dataset":       {"Record"},
    "storage":       {"Record", "RecordMergePatchRequest"},
}


def _is_freeform_data_schema(schema: dict) -> bool:
    """Return True if ``schema`` looks like a free-form OSDU ``data`` payload.

    Accepts the two shapes we've observed upstream:
    * ``{type: object, additionalProperties: true}`` (most ``Record.data``)
    * ``{type: object, additionalProperties: {type: object}}``
      (``RecordMergePatchRequest.data``)

    Anything with typed ``properties``, a ``$ref``, or ``allOf``/``oneOf``/
    ``anyOf`` is rejected — it would mean upstream now describes a structured
    ``data`` and the patch should be re-evaluated rather than silently dropping
    the type information.
    """
    if schema.get("type") != "object":
        return False
    if schema.get("properties"):
        return False
    if any(key in schema for key in ("$ref", "allOf", "oneOf", "anyOf")):
        return False
    additional_properties = schema.get("additionalProperties")
    return additional_properties is True or isinstance(additional_properties, dict)


def untype_freeform_record_data(spec_data: dict, service_name: str) -> list[str]:
    """Untype the ``data`` property on each free-form record schema for the spec.

    Replaces the ``data`` property of every targeted schema with an empty
    schema so Kiota emits a ``UntypedNode`` (free-form JSON) rather than an
    empty ``*_data`` class. Returns the names of schemas that were patched.

    If the upstream ``data`` schema no longer matches the expected free-form
    shape, the patch is skipped and a warning is printed so the change is
    visible — silently overwriting a now-typed schema would be worse than not
    patching at all.
    """
    targets = FREEFORM_DATA_SCHEMAS.get(service_name, set())
    if not targets:
        return []

    schemas = (spec_data.get("components") or {}).get("schemas") or {}
    patched: list[str] = []
    for name in sorted(targets):
        schema = schemas.get(name)
        if not isinstance(schema, dict):
            continue
        properties = schema.get("properties")
        if not isinstance(properties, dict) or "data" not in properties:
            continue
        data_schema = properties["data"]
        if not isinstance(data_schema, dict) or not _is_freeform_data_schema(data_schema):
            shape = (
                f"keys={sorted(data_schema)}"
                if isinstance(data_schema, dict)
                else f"type={type(data_schema).__name__}"
            )
            print(
                f"  ! WARNING: {name}.data in {service_name} no longer looks "
                f"like a free-form schema ({shape}). Leaving it untouched — "
                f"re-evaluate whether this patch is still needed."
            )
            continue
        # An empty schema carries no type information, so Kiota maps the
        # property to UntypedNode. The title/description are dropped
        # intentionally to avoid Kiota inferring a named model from them.
        properties["data"] = {}
        patched.append(name)
    return patched


def normalize_wildcard_properties(obj):
    """
    Recursively replace { "< * >": <schema> } with additionalProperties.

    Some OSDU specs (e.g. Partition) express map/dictionary schemas using
    a literal "< * >" property key as a wildcard placeholder. This is not
    valid OpenAPI and causes Kiota to emit broken C# identifiers. The correct
    OpenAPI representation is additionalProperties.
    """
    if isinstance(obj, dict):
        if "< * >" in obj.get("properties", {}):
            wildcard_schema = obj["properties"].pop("< * >")
            obj.setdefault("additionalProperties", wildcard_schema)
            if not obj["properties"]:
                del obj["properties"]
        for value in obj.values():
            normalize_wildcard_properties(value)
    elif isinstance(obj, list):
        for item in obj:
            normalize_wildcard_properties(item)


def generate_all():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    specs = sorted(
        p for p in SPECS_DIR.iterdir()
        if p.is_file() and p.suffix.lower() in SPEC_EXTENSIONS
    )
    print(f"Found {len(specs)} OpenAPI specs.")

    for spec_path in specs:
        raw_name = spec_path.stem  # e.g. "CRS_Catalog"
        service_name = (
            raw_name.lower().replace(" ", "_").replace("-", "_")
        )  # e.g. "crs_catalog"
        class_name = to_pascal_case(service_name) + "Client"  # e.g. "CrsCatalogClient"
        namespace = f"Equinor.OsduCsharpClient.{to_pascal_case(service_name)}"  # e.g. "Equinor.OsduCsharpClient.CrsCatalog"
        output_path = OUTPUT_DIR / to_pascal_case(
            service_name
        )  # e.g. src/OsduCsharpClient/CrsCatalog

        print(f"Generating client for {service_name} (from {spec_path.name})...")

        spec_data = _load_spec(spec_path)

        normalize_wildcard_properties(spec_data)

        for patched_name in untype_freeform_record_data(spec_data, service_name):
            print(f"  - Untyping {patched_name}.data for {service_name} (free-form JSON)")

        needs_version_patch = "info" in spec_data and "version" not in spec_data["info"]
        if needs_version_patch:
            spec_data["info"]["version"] = "1.0.0"
            print(f"  - Patching missing version for {service_name}")

        # Always write a temp JSON file so in-memory normalizations and YAML
        # conversion take effect (Kiota accepts JSON on all platforms).
        temp_spec_path = spec_path.with_suffix(".temp.json")
        with open(temp_spec_path, "w") as f:
            json.dump(spec_data, f)

        if output_path.exists():
            shutil.rmtree(output_path)
        output_path.mkdir(parents=True)

        cmd = [
            KIOTA,
            "generate",
            "--openapi",
            str(temp_spec_path),
            "--language",
            "CSharp",
            "--class-name",
            class_name,
            "--namespace-name",
            namespace,
            "--output",
            str(output_path),
            "--clean-output",
            "--clear-cache",
            "--exclude-backward-compatible",
        ]

        try:
            result = subprocess.run(cmd, capture_output=True, text=True)
            if result.returncode == 0:
                print(f"  Successfully generated {service_name} → {output_path}")
            else:
                print(f"  Failed to generate {service_name}")
                print(result.stderr or result.stdout)
        except Exception as e:
            print(f"  Error generating {service_name}: {e}")
        finally:
            if temp_spec_path.exists():
                temp_spec_path.unlink()


if __name__ == "__main__":
    generate_all()
