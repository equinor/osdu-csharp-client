import os
import requests
import re

# URL to the wiki (Raw for easier parsing)
WIKI_URL = "https://community.opengroup.org/groups/osdu/platform/-/wikis/Core-Services-API-Docs/raw"
OUTPUT_DIR = "openapi_specs"

def get_json_url(original_url: str) -> str:
    """
    Convert UI/view URLs to JSON spec URLs.
    Handles Springfox (swagger-ui) and FastAPI (/docs).
    """
    if not original_url:
        return original_url

    if "swagger-ui" in original_url:
        base = original_url.split("swagger-ui")[0]
        return base.rstrip("/") + "/api-docs"

    if original_url.endswith("/docs"):
        base = original_url.split("/docs")[0]
        return base.rstrip("/") + "/openapi.json"

    return original_url

def _extract_first_markdown_url(cell_text: str) -> str | None:
    """
    Extract first URL from a markdown table cell, e.g. [API Doc](https://...).
    Falls back to a raw https://... if present.
    """
    if not cell_text:
        return None

    match = re.search(r"\((https?://[^)\s]+)\)", cell_text)
    if match:
        return match.group(1)

    match = re.search(r"(https?://\S+)", cell_text)
    if match:
        return match.group(1).rstrip(")")
    return None

def extract_service_sources(markdown_text: str) -> dict[str, dict[str, str]]:
    """
    Parse markdown tables and collect URLs from:
    Community Implementation, Azure, AWS, GC columns (when present).
    Returns: { safe_service_name: { 'ci': url, 'azure': url, 'aws': url, 'gc': url } }
    """
    services: dict[str, dict[str, str]] = {}

    lines = markdown_text.split("\n")
    for line in lines:
        stripped = line.strip()
        # table row guard; ignore header separators
        if not (stripped.startswith("|") and "---" not in stripped):
            continue

        cols = [c.strip() for c in stripped.split("|")]

        # Expect at least: | Service | Community Implementation | ...
        if len(cols) < 3:
            continue

        service_name = cols[1]
        if "Services" in service_name:
            continue

        safe_name = re.sub(r"[^a-zA-Z0-9]", "_", service_name).strip("_")

        # Column mapping (best-effort):
        # 2: Community Implementation
        # 3: Azure
        # 4: AWS
        # 5: GC
        col_ci = cols[2] if len(cols) > 2 else ""
        col_azure = cols[3] if len(cols) > 3 else ""
        col_aws = cols[4] if len(cols) > 4 else ""
        col_gc = cols[5] if len(cols) > 5 else ""

        urls: dict[str, str] = {}
        ci_url = _extract_first_markdown_url(col_ci)
        if ci_url:
            urls["ci"] = ci_url

        azure_url = _extract_first_markdown_url(col_azure)
        if azure_url:
            urls["azure"] = azure_url

        aws_url = _extract_first_markdown_url(col_aws)
        if aws_url:
            urls["aws"] = aws_url

        gc_url = _extract_first_markdown_url(col_gc)
        if gc_url:
            urls["gc"] = gc_url

        if urls:
            services[safe_name] = urls

    return services

def _looks_like_json_response(resp: requests.Response) -> bool:
    ctype = (resp.headers.get("content-type") or "").lower()
    if "application/json" in ctype:
        return True
    if "text/html" in ctype:
        return False
    # If content-type is missing/odd, do a light check
    body = (resp.text or "").lstrip()
    return body.startswith("{") or body.startswith("[")

def try_download_spec(url: str, timeout: int = 10) -> tuple[bool, str, bytes | None]:
    """
    Attempt to download a spec URL.
    Returns (ok, reason, content_bytes).
    """
    if not url:
        return False, "missing url", None

    json_url = get_json_url(url)

    try:
        resp = requests.get(json_url, timeout=timeout)
    except Exception as e:
        return False, f"request error: {e}", None

    if resp.status_code != 200:
        return False, f"http {resp.status_code}", None

    if not _looks_like_json_response(resp):
        return False, "non-json response", None

    return True, "ok", resp.content

def download_specs() -> None:
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    print(f"Fetching wiki from {WIKI_URL}...")
    try:
        content = requests.get(WIKI_URL, timeout=20).text
    except Exception as e:
        print(f"Failed to fetch wiki: {e}")
        return

    services = extract_service_sources(content)
    print(f"Found {len(services)} services. Starting download with fallbacks...\n")

    priority = ["ci", "azure", "aws", "gc"]

    for name, sources in services.items():
        filename = f"{name}.json"
        filepath = os.path.join(OUTPUT_DIR, filename)

        print(f"Service: {name}")
        tried_any = False
        saved = False

        for key in priority:
            if key not in sources:
                continue

            tried_any = True
            src_url = sources[key]
            ok, reason, data = try_download_spec(src_url)

            print(f" - Try {key.upper():5s}: {get_json_url(src_url)}")
            if ok and data is not None:
                with open(filepath, "wb") as f:
                    f.write(data)
                print(f"   [OK] Saved as {filename} (from {key.upper()})")
                saved = True
                break
            else:
                print(f"   [FAIL] {reason}")

        if not tried_any:
            print(" - [SKIP] No sources found in row")

        if not saved and tried_any:
            print(" - [ERROR] All sources failed")

        print("-" * 40)

    print(f"\nDone. Check folder `{OUTPUT_DIR}`")

if __name__ == "__main__":
    download_specs()
