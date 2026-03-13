import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

SPECS_DIR = Path("openapi_specs")
OUTPUT_DIR = Path("src/OsduCsharpClient")

KIOTA = shutil.which("kiota") or os.path.expanduser("~/.dotnet/tools/kiota")


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

    specs = list(SPECS_DIR.glob("*.json"))
    print(f"Found {len(specs)} OpenAPI specs.")

    for spec_path in specs:
        raw_name = spec_path.stem  # e.g. "CRS_Catalog"
        service_name = raw_name.lower().replace(" ", "_").replace("-", "_")  # e.g. "crs_catalog"
        class_name = to_pascal_case(service_name) + "Client"  # e.g. "CrsCatalogClient"
        namespace = f"OsduCsharpClient.{to_pascal_case(service_name)}"  # e.g. "OsduCsharpClient.CrsCatalog"
        output_path = OUTPUT_DIR / to_pascal_case(service_name)  # e.g. src/OsduCsharpClient/CrsCatalog

        print(f"Generating client for {service_name}...")

        # OSDU specs sometimes miss the 'version' field required by Kiota
        with open(spec_path) as f:
            spec_data = json.load(f)

        normalize_wildcard_properties(spec_data)

        needs_version_patch = "info" in spec_data and "version" not in spec_data["info"]
        if needs_version_patch:
            spec_data["info"]["version"] = "1.0.0"
            print(f"  - Patching missing version for {service_name}")

        # Always write a temp file so in-memory normalizations take effect
        temp_spec_path = spec_path.with_suffix(".temp.json")
        with open(temp_spec_path, "w") as f:
            json.dump(spec_data, f)

        if output_path.exists():
            shutil.rmtree(output_path)
        output_path.mkdir(parents=True)

        cmd = [
            KIOTA, "generate",
            "--openapi", str(temp_spec_path),
            "--language", "CSharp",
            "--class-name", class_name,
            "--namespace-name", namespace,
            "--output", str(output_path),
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
