#!/usr/bin/env python3
"""
Convert multiple k6 final-summary JSON files into one CSV file.

This script looks for all .json files in the same directory as this Python file.
It extracts the top-level "metrics" object from each file and flattens it into
CSV columns.

The first column, "key", is the JSON filename without extension.
"""

import json
import re
from pathlib import Path

import pandas as pd

# Read JSON files from the "results" subdirectory next to this Python file,
# and generate the CSV alongside this Python file.

try:
    # Preferred: resolve paths relative to this Python file.
    SCRIPT_DIR = Path(__file__).resolve().parent
except NameError:
    # Fallback for unusual execution contexts where __file__ is not defined.
    SCRIPT_DIR = Path.cwd()


INPUT_DIR = SCRIPT_DIR / "results"
OUTPUT_CSV = SCRIPT_DIR / "k6_metrics_summary.csv"


def strip_whitespace_from_keys(value):
    """
    Recursively strip whitespace from dictionary keys.

    Some exported JSON summaries may contain keys like:
        "metrics "
        "http_req_duration "
        "p(90) "

    This function normalizes them to:
        "metrics"
        "http_req_duration"
        "p(90)"
    """
    if isinstance(value, dict):
        return {
            str(key).strip(): strip_whitespace_from_keys(val)
            for key, val in value.items()
        }

    if isinstance(value, list):
        return [strip_whitespace_from_keys(item) for item in value]

    return value


def flatten_dict(value, parent_key="", sep="."):
    """
    Flatten a nested dictionary.

    Example:
        {
            "http_req_duration": {
                "avg": 46.59,
                "p(90)": 62.71
            }
        }

    Becomes:
        {
            "http_req_duration.avg": 46.59,
            "http_req_duration.p(90)": 62.71
        }
    """
    flat = {}

    if isinstance(value, dict):
        for key, val in value.items():
            new_key = f"{parent_key}{sep}{key}" if parent_key else str(key)

            if isinstance(val, dict):
                flat.update(flatten_dict(val, new_key, sep=sep))
            elif isinstance(val, list):
                # Preserve lists as JSON strings in the CSV.
                flat[new_key] = json.dumps(val)
            else:
                flat[new_key] = val
    else:
        # If a scalar is passed directly, store it under the parent key.
        flat[parent_key or "value"] = value

    return flat


# Matches k6 result filenames like:
#   google-protobuf_serialize-only_10.json
#   messagepack-lz4_roundtrip_1000.json
#
# Captures the serializer name (which may contain hyphens), the operation
# (serialize-only / deserialize-only / roundtrip), and the numeric payload size.
_FILENAME_PATTERN = re.compile(
    r"^(?P<serializer>.+?)_(?P<category>serialize-only|deserialize-only|roundtrip)_(?P<payload_size>\d+)$"
)


def parse_filename_metadata(filename_stem: str):
    """
    Parse the k6 result filename into descriptive columns.

    The result is used as:
        row.update({
            "serializer": "messagepack-lz4",
            "category": "roundtrip",
            "payload_size": 1000,
        })

    Returns None if the filename does not match the expected pattern.
    """
    match = _FILENAME_PATTERN.match(filename_stem)
    if not match:
        return None

    return {
        "serializer": match.group("serializer"),
        "category": match.group("category"),
        "payload_size": int(match.group("payload_size")),
    }


def load_k6_metrics(json_path: Path):
    """
    Load one k6 JSON summary file and return flattened metrics.

    Returns None if the file does not look like a k6 summary file.
    """
    raw_text = json_path.read_text(encoding="utf-8-sig")
    data = json.loads(raw_text)

    if not isinstance(data, dict):
        return None

    data = strip_whitespace_from_keys(data)

    metrics = data.get("metrics")
    if not isinstance(metrics, dict) or not metrics:
        return None

    return flatten_dict(metrics)


def find_json_files(directory: Path):
    """
    Return all .json files in the given directory, sorted by filename.
    """
    return sorted(
        path
        for path in directory.iterdir()
        if path.is_file() and path.suffix.lower() == ".json"
    )


def main():
    rows = []

    json_files = find_json_files(INPUT_DIR)

    if not json_files:
        print(f"No JSON files found in: {INPUT_DIR}")
        return

    for json_path in json_files:
        try:
            metrics_row = load_k6_metrics(json_path)
        except Exception as exc:
            print(f"Skipping {json_path.name}: {exc}")
            continue

        if metrics_row is None:
            print(f"Skipping {json_path.name}: no top-level 'metrics' object found.")
            continue

        metadata = parse_filename_metadata(json_path.stem)
        if metadata is None:
            print(
                f"Skipping {json_path.name}: filename does not match the expected "
                "pattern '<serializer>_<category>_<payload_size>'."
            )
            continue

        row = {
            "key": json_path.stem,
        }
        row.update(metadata)
        row.update(metrics_row)

        rows.append(row)

    if not rows:
        print("No valid k6 JSON summary files were processed.")
        return

    df = pd.DataFrame.from_dict(rows)

    # Keep the identifier column first, followed by descriptive columns parsed
    # from the filename, then sort the remaining metric columns for stable CSV output.
    descriptive_columns = ["serializer", "category", "payload_size"]
    metric_columns = sorted(
        column
        for column in df.columns
        if column not in {"key", *descriptive_columns}
    )
    df = df.reindex(columns=["key", *descriptive_columns, *metric_columns])

    df.to_csv(
        OUTPUT_CSV,
        index=False,
        encoding="utf-8",
        na_rep="",
    )

    print(f"Created CSV: {OUTPUT_CSV}")
    print(f"Rows written: {len(df)}")
    print(f"Columns written: {len(df.columns)}")


if __name__ == "__main__":
    main()
