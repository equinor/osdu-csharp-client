import json
import os
import re
import shutil
import subprocess
from pathlib import Path

import yaml

SPECS_DIR = Path("openapi_specs")
OUTPUT_DIR = Path("src/OsduCsharpClient")
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
