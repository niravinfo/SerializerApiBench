#!/usr/bin/env python3
"""
Generate blog-ready chart images from the k6 metrics summary CSV.

Reads k6_metrics_summary.csv (the output of convert_k6_json_to_csv.py) and writes a
set of .png charts into ./charts.

Design rules:
  * Serializers use one fixed, professional color in every image so they are easy
    to identify at a glance.
  * Charts default to the largest payload size only (--payload-sizes), because
    10-item and 1000-item numbers are so far apart that mixing them on one axis
    makes the chart hard to read. If several sizes are selected, each size gets
    its own panel (never shared bars on one axis).
  * No grid lines — data labels carry the numbers.

Charts:
  1. roundtrip_latency_leaderboard.png   round-trip p95 latency, ranked (panel per size)
  2. latency_breakdown_by_mode.png       p95 latency, panel per operation & size
  3. throughput_by_mode.png              requests/sec, panel per operation & size
  4. wire_size_per_request.png           serialized bytes per request (panel per size)
  5. roundtrip_latency_scaling.png       p95 vs payload size (only when >=2 sizes selected)

Usage:
    python generate_charts.py [path/to/k6_metrics_summary.csv]
    python generate_charts.py --payload-sizes 1000
    python generate_charts.py --payload-sizes 10,100,1000
"""

import argparse
import sys
from pathlib import Path

import matplotlib

matplotlib.use("Agg")

import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_INPUT_CSV = SCRIPT_DIR / "k6_metrics_summary.csv"
OUTPUT_DIR = SCRIPT_DIR / "charts"

# Column names in the summary CSV.
COL_LATENCY_AVG = "http_req_duration.avg"
COL_LATENCY_P95 = "http_req_duration.p(95)"
COL_REQS_RATE = "http_reqs.rate"
COL_REQS_COUNT = "http_reqs.count"
COL_DATA_RECEIVED = "data_received.count"
COL_DATA_SENT = "data_sent.count"

CATEGORY_ORDER = ["serialize-only", "deserialize-only", "roundtrip"]

CATEGORY_LABELS = {
    "serialize-only": "Serialize only",
    "deserialize-only": "Deserialize only",
    "roundtrip": "Round-trip",
}

SERIALIZER_LABELS = {
    "google-protobuf": "Google Protobuf",
    "json": "System.Text.Json",
    "messagepack-lz4": "MessagePack + LZ4",
    "messagepack": "MessagePack",
    "newtonsoft-json": "Newtonsoft.Json",
    "protobuf-net": "protobuf-net",
}

# One fixed color per serializer so every chart stays consistent and instantly
# readable. Saturated, modern shades that stay distinct from each other.
SERIALIZER_COLORS = {
    "json": "#2563EB",             # blue    (System.Text.Json, the default)
    "newtonsoft-json": "#DC2626",  # red
    "messagepack": "#16A34A",      # green
    "messagepack-lz4": "#14B8A6",  # teal
    "google-protobuf": "#7C3AED",  # violet
    "protobuf-net": "#EA580C",     # orange
}

# Used for any serializer that is not in SERIALIZER_COLORS.
FALLBACK_COLORS = ["#6B7280", "#9CA3AF", "#374151"]

DPI = 200


def serializer_label(name: str) -> str:
    return SERIALIZER_LABELS.get(name, name.replace("-", " ").title())


def fmt_ms(value: float) -> str:
    if value >= 100:
        return f"{value:.0f} ms"
    if value >= 10:
        return f"{value:.1f} ms"
    return f"{value:.2f} ms"


def fmt_reqs(value: float) -> str:
    return f"{value:,.0f}"


def fmt_bytes(value: float) -> str:
    if value >= 1024 * 1024:
        return f"{value / (1024 * 1024):.1f} MB"
    if value >= 1024:
        return f"{value / 1024:.0f} KB"
    return f"{value:.0f} B"


def load_data(csv_path: Path) -> pd.DataFrame:
    df = pd.read_csv(csv_path)

    required = {
        "serializer",
        "category",
        "payload_size",
        COL_LATENCY_P95,
        COL_REQS_RATE,
        COL_REQS_COUNT,
        COL_DATA_RECEIVED,
        COL_DATA_SENT,
    }
    missing = required - set(df.columns)
    if missing:
        sys.exit(f"CSV {csv_path} is missing required columns: {sorted(missing)}")

    df["payload_size"] = df["payload_size"].astype(int)
    df["category"] = pd.Categorical(
        df["category"], categories=CATEGORY_ORDER, ordered=True
    )

    # Bytes actually transferred per request (request body / response body).
    df["bytes_per_req_sent"] = df[COL_DATA_SENT] / df[COL_REQS_COUNT]
    df["bytes_per_req_received"] = df[COL_DATA_RECEIVED] / df[COL_REQS_COUNT]
    return df


def order_serializers(df: pd.DataFrame, anchor_size: int) -> list:
    """Fastest-first, based on round-trip p95 latency at the anchor payload size."""
    sub = df[(df["category"] == "roundtrip") & (df["payload_size"] == anchor_size)]
    ordered = list(sub.sort_values(COL_LATENCY_P95)["serializer"].unique())
    for name in sorted(set(df["serializer"]) - set(ordered)):
        ordered.append(name)
    return ordered


def build_colors(serializers: list) -> dict:
    colors = {}
    fallback_index = 0
    for name in serializers:
        if name in SERIALIZER_COLORS:
            colors[name] = SERIALIZER_COLORS[name]
        else:
            colors[name] = FALLBACK_COLORS[fallback_index % len(FALLBACK_COLORS)]
            fallback_index += 1
    return colors


def style_axes(ax):
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)


def _safe_top(value, factor, default=1.0):
    return value * factor if pd.notna(value) else default


def draw_horizontal_bars(ax, series, colors, value_fmt, order="asc", margin=1.35, ratio_ref=None):
    """Horizontal leaderboard bars. `order` = 'asc' (fastest/smallest on top) or 'desc'."""
    series = series.sort_values(ascending=(order == "asc"))
    y = np.arange(len(series))
    values = series.to_numpy(dtype=float)

    ax.barh(
        y,
        values,
        color=[colors[name] for name in series.index],
        edgecolor="white",
        linewidth=0.5,
    )
    ax.set_yticks(y)
    ax.set_yticklabels([serializer_label(name) for name in series.index])
    ax.invert_yaxis()

    for yi, (name, value) in enumerate(series.items()):
        if not np.isfinite(value):
            continue
        label = value_fmt(value)
        if ratio_ref is not None:
            label += f"  ({value / ratio_ref:.1f}x)"
        ax.text(value + value * 0.02, yi, label, va="center", fontsize=9)

    ax.set_xlim(right=_safe_top(series.max(), margin))
    style_axes(ax)
    return series


def plot_latency_leaderboard(df, serializers, colors, sizes, out_dir):
    n = len(sizes)
    fig, axes = plt.subplots(1, n, figsize=(6.6 * n, 4.4), squeeze=False)

    for ax, size in zip(axes.flat, sizes):
        sub = df[(df["category"] == "roundtrip") & (df["payload_size"] == size)]
        ranked = (
            sub.set_index("serializer").reindex(serializers)[COL_LATENCY_P95]
            .astype(float)
        )
        draw_horizontal_bars(
            ax,
            ranked,
            colors,
            fmt_ms,
            order="asc",
            margin=1.45,
            ratio_ref=ranked.min(),
        )
        ax.set_xlabel("p95 latency (ms)")
        if n == 1:
            ax.set_title(
                f"Round-trip p95 latency at {size} items — lower is better",
                fontweight="bold",
            )
        else:
            ax.set_title(f"{size} items", fontweight="bold")

    if n > 1:
        fig.suptitle("Round-trip p95 latency — lower is better", fontweight="bold")
    fig.tight_layout(rect=(0, 0, 1, 0.95) if n > 1 else (0, 0, 1, 1))

    path = out_dir / "roundtrip_latency_leaderboard.png"
    fig.savefig(path, dpi=DPI)
    plt.close(fig)
    return path


def plot_mode_grid(df, serializers, colors, sizes, metric_col, value_fmt, unit, suffix):
    """Grid: rows = payload sizes, columns = operations (serialize/deserialize/round-trip)."""
    rows = len(sizes)
    cols = len(CATEGORY_ORDER)
    fig, axes = plt.subplots(rows, cols, figsize=(4.4 * cols, 3.1 * rows), squeeze=False)

    for r, size in enumerate(sizes):
        for c, category in enumerate(CATEGORY_ORDER):
            ax = axes[r, c]
            sub = df[(df["category"] == category) & (df["payload_size"] == size)]
            series = (
                sub.set_index("serializer").reindex(serializers)[metric_col]
                .astype(float)
            )

            x = np.arange(len(series))
            values = series.to_numpy(dtype=float)
            ax.bar(
                x,
                values,
                color=[colors[name] for name in series.index],
                edgecolor="white",
                linewidth=0.5,
            )
            ax.set_xticks(x)
            if r == rows - 1:
                ax.set_xticklabels(
                    [serializer_label(name) for name in series.index],
                    rotation=30,
                    ha="right",
                    fontsize=7,
                )
            else:
                ax.set_xticklabels([])

            for xi, v in enumerate(values):
                if np.isfinite(v):
                    ax.text(xi, v + v * 0.02, value_fmt(v), ha="center", va="bottom", fontsize=7)
            ax.set_ylim(top=_safe_top(series.max(), 1.18))

            if c == 0:
                ax.set_ylabel(f"{size} items", fontsize=9)
            if r == 0:
                ax.set_title(CATEGORY_LABELS[category], fontsize=10, fontweight="bold")
            style_axes(ax)

    if len(sizes) == 1:
        title = f"{unit} at {sizes[0]} items — {suffix}"
    else:
        title = f"{unit} by payload size and operation — {suffix}"
    fig.suptitle(title, fontweight="bold")
    fig.tight_layout(rect=(0, 0, 1, 0.95))
    return fig


def plot_latency_breakdown(df, serializers, colors, sizes, out_dir):
    fig = plot_mode_grid(
        df, serializers, colors, sizes, COL_LATENCY_P95, fmt_ms,
        "p95 latency (ms)", "lower is better",
    )
    path = out_dir / "latency_breakdown_by_mode.png"
    fig.savefig(path, dpi=DPI)
    plt.close(fig)
    return path


def plot_throughput(df, serializers, colors, sizes, out_dir):
    fig = plot_mode_grid(
        df, serializers, colors, sizes, COL_REQS_RATE, fmt_reqs,
        "Requests/sec", "more is better",
    )
    path = out_dir / "throughput_by_mode.png"
    fig.savefig(path, dpi=DPI)
    plt.close(fig)
    return path


def plot_wire_size(df, serializers, colors, sizes, out_dir):
    sub = df[df["category"] == "roundtrip"]
    n = len(sizes)
    fig, axes = plt.subplots(1, n, figsize=(7 * n, 4.4), squeeze=False)

    for ax, size in zip(axes.flat, sizes):
        data = (
            sub[sub["payload_size"] == size]
            .set_index("serializer").reindex(serializers)["bytes_per_req_received"]
            .astype(float)
        )
        draw_horizontal_bars(ax, data, colors, fmt_bytes, order="desc", margin=1.3)
        ax.set_xlabel("Response body per request")
        if n == 1:
            ax.set_title(
                f"Serialized size on the wire at {size} items (round-trip) — smaller is better",
                fontweight="bold",
            )
        else:
            ax.set_title(f"{size} items", fontweight="bold")

    if n > 1:
        fig.suptitle("Serialized size on the wire (round-trip) — smaller is better", fontweight="bold")
    fig.tight_layout(rect=(0, 0, 1, 0.95) if n > 1 else (0, 0, 1, 1))

    path = out_dir / "wire_size_per_request.png"
    fig.savefig(path, dpi=DPI)
    plt.close(fig)
    return path


def plot_latency_scaling(df, serializers, colors, sizes, out_dir):
    fig, ax = plt.subplots(figsize=(8, 5))

    for name in serializers:
        sub = df[(df["serializer"] == name) & (df["category"] == "roundtrip")]
        sub = sub[sub["payload_size"].isin(sizes)].sort_values("payload_size")
        valid = sub[sub[COL_LATENCY_P95].notna()].sort_values("payload_size")
        if valid.empty:
            continue
        x = valid["payload_size"].astype(int)
        y = valid[COL_LATENCY_P95].astype(float)

        ax.plot(
            x,
            y,
            marker="o",
            markersize=4.5,
            linewidth=1.6,
            color=colors[name],
            label=serializer_label(name),
        )
        ax.annotate(
            fmt_ms(y.iloc[-1]),
            (x.iloc[-1], y.iloc[-1]),
            textcoords="offset points",
            xytext=(6, 0),
            fontsize=8,
            color=colors[name],
            va="center",
        )

    ax.set_xscale("log")
    ax.set_yscale("log")
    ax.set_xticks(sizes)
    ax.xaxis.set_major_formatter(plt.ScalarFormatter())
    ax.set_xlabel("Payload size (items)")
    ax.set_ylabel("p95 latency (ms, log scale)")
    ax.set_title("Round-trip latency vs payload size — lower is better")
    ax.legend(frameon=False, ncol=2, fontsize=9)
    style_axes(ax)

    fig.tight_layout()
    path = out_dir / "roundtrip_latency_scaling.png"
    fig.savefig(path, dpi=DPI)
    plt.close(fig)
    return path


def main():
    parser = argparse.ArgumentParser(
        description="Generate blog-ready charts from the k6 metrics summary CSV."
    )
    parser.add_argument(
        "csv",
        nargs="?",
        default=str(DEFAULT_INPUT_CSV),
        help="Path to k6_metrics_summary.csv",
    )
    parser.add_argument(
        "--outdir",
        default=str(OUTPUT_DIR),
        help="Directory to write chart images to",
    )
    parser.add_argument(
        "--payload-sizes",
        default=None,
        help="Comma-separated payload sizes to chart, e.g. '1000' or '10,100,1000'. "
             "Defaults to the largest size present in the data.",
    )
    args = parser.parse_args()

    csv_path = Path(args.csv)
    out_dir = Path(args.outdir)
    out_dir.mkdir(parents=True, exist_ok=True)

    df = load_data(csv_path)

    available = sorted(set(int(s) for s in df["payload_size"]))
    if args.payload_sizes:
        requested = [int(s.strip()) for s in args.payload_sizes.split(",") if s.strip()]
        sizes = sorted({s for s in requested if s in available})
        if not sizes:
            sys.exit(
                f"None of the requested payload sizes ({args.payload_sizes}) exist "
                f"in the data. Available: {available}"
            )
    else:
        sizes = [available[-1]]

    anchor = sizes[-1]
    serializers = order_serializers(df, anchor)
    colors = build_colors(serializers)

    plt.rcParams.update(
        {
            "figure.facecolor": "white",
            "axes.facecolor": "white",
            "font.size": 10,
            "axes.titlesize": 12,
            "axes.titleweight": "bold",
        }
    )

    generated = [
        plot_latency_leaderboard(df, serializers, colors, sizes, out_dir),
        plot_latency_breakdown(df, serializers, colors, sizes, out_dir),
        plot_throughput(df, serializers, colors, sizes, out_dir),
        plot_wire_size(df, serializers, colors, sizes, out_dir),
    ]
    if len(sizes) >= 2:
        generated.append(plot_latency_scaling(df, serializers, colors, sizes, out_dir))

    sub = (
        df[(df["category"] == "roundtrip") & (df["payload_size"] == anchor)]
        .set_index("serializer")[COL_LATENCY_P95]
    )
    fastest = sub.idxmin()
    slowest = sub.idxmax()

    print(f"Read {len(df)} rows from {csv_path}")
    print(f"Charting payload sizes: {sizes}")
    print("Serializer order (fastest first):", ", ".join(serializer_label(s) for s in serializers))
    print(
        f"Round-trip p95 at {anchor} items: fastest "
        f"{serializer_label(fastest)} ({sub[fastest]:.1f} ms), slowest "
        f"{serializer_label(slowest)} ({sub[slowest]:.1f} ms, "
        f"{sub[slowest] / sub[fastest]:.1f}x slower)"
    )
    print("Generated charts:")
    for path in generated:
        print(f"  {path}")


if __name__ == "__main__":
    main()
