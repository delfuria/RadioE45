#!/usr/bin/env python3
"""
Measures real end-to-end latency between AzuraCast's `played_at` (SongHistory
timestamp_start, used in the elapsed calc) and the moment the track actually
switches in the encoded Icecast stream.

Method:
  1. Poll /api/nowplaying/<station> every 1s, track current song + played_at.
  2. Open the Icecast stream with `Icy-MetaData: 1`, parse the interleaved
     ICY metadata blocks, and timestamp every StreamTitle change.
  3. On each stream-side title change, pair it with the API's played_at for
     that song and compute delta = stream_change_wallclock - played_at.
  4. After N samples, print the average -> use that (rounded) as
     PLAYBACK_DELAY_SECONDS in backend/src/Entity/SongHistory.php.

Usage:
  ./measure-latency.py [--api-url http://localhost:8880] [--station shortcode] [--samples 8]

No third-party deps: stdlib only (http.client, urllib, threading).
"""

from __future__ import annotations

import argparse
import http.client
import json
import re
import threading
import time
import urllib.parse
import urllib.request


def fetch_nowplaying(api_url: str, station: str | None) -> dict:
    url = f"{api_url}/api/nowplaying" + (f"/{station}" if station else "")
    with urllib.request.urlopen(url, timeout=10) as resp:
        data = json.loads(resp.read())
    if isinstance(data, list):
        if station:
            for entry in data:
                if entry.get("station", {}).get("shortcode") == station:
                    return entry
            raise SystemExit(f"station '{station}' not found in /api/nowplaying")
        if not data:
            raise SystemExit("no stations found in /api/nowplaying")
        return data[0]
    return data


class ApiPoller(threading.Thread):
    def __init__(self, api_url: str, station: str):
        super().__init__(daemon=True)
        self.api_url = api_url
        self.station = station
        self.lock = threading.Lock()
        self.song_text: str | None = None
        self.played_at: int | None = None
        self.stop_flag = False

    def run(self):
        url = f"{self.api_url}/api/nowplaying/{self.station}"
        while not self.stop_flag:
            try:
                with urllib.request.urlopen(url, timeout=5) as resp:
                    data = json.loads(resp.read())
                np = data.get("now_playing", {})
                text = np.get("song", {}).get("text")
                played_at = np.get("played_at")
                with self.lock:
                    self.song_text = text
                    self.played_at = played_at
            except Exception as exc:  # noqa: BLE001
                print(f"  [api-poll error] {exc}")
            time.sleep(1)

    def snapshot(self) -> tuple[str | None, int | None]:
        with self.lock:
            return self.song_text, self.played_at


def parse_icy_metaint(headers: http.client.HTTPMessage) -> int:
    metaint = headers.get("icy-metaint")
    if not metaint:
        raise SystemExit(
            "stream did not return icy-metaint header — server may not support "
            "ICY metadata, or a proxy stripped it"
        )
    return int(metaint)


STREAM_TITLE_RE = re.compile(r"StreamTitle='(.*?)';")


def stream_metadata_loop(stream_url: str, samples: int, api: ApiPoller):
    parsed = urllib.parse.urlparse(stream_url)
    if parsed.scheme == "https":
        conn = http.client.HTTPSConnection(parsed.hostname, parsed.port or 443, timeout=15)
    else:
        conn = http.client.HTTPConnection(parsed.hostname, parsed.port or 80, timeout=15)
    path = parsed.path or "/"
    if parsed.query:
        path += f"?{parsed.query}"
    conn.request("GET", path, headers={"Icy-MetaData": "1", "User-Agent": "azuracast-latency-probe"})
    resp = conn.getresponse()
    if resp.status != 200:
        raise SystemExit(f"stream returned HTTP {resp.status}")

    metaint = parse_icy_metaint(resp.headers)
    print(f"==> Connected to stream, icy-metaint={metaint}")

    last_title: str | None = None
    deltas: list[float] = []

    while len(deltas) < samples:
        # Discard `metaint` bytes of audio.
        _drain(resp, metaint)

        length_byte = resp.read(1)
        if not length_byte:
            raise SystemExit("stream closed unexpectedly")
        meta_len = length_byte[0] * 16
        if meta_len == 0:
            continue

        meta_block = _drain(resp, meta_len)
        change_time = time.time()

        text = meta_block.decode("utf-8", errors="replace")
        match = STREAM_TITLE_RE.search(text)
        if not match:
            continue
        title = match.group(1)

        if title == last_title:
            continue
        last_title = title

        print(f"\n==> Stream title changed to: {title!r} at {change_time:.3f}")

        # AzuraCast's now-playing cache on this instance refreshes only every ~20s
        # (confirmed via test-fix.sh), so give the API poller enough cycles to
        # catch up to the real title before giving up.
        played_at = None
        for _ in range(25):
            api_title, api_played_at = api.snapshot()
            if api_title == title and api_played_at:
                played_at = api_played_at
                break
            time.sleep(1)

        if played_at is None:
            api_title, played_at = api.snapshot()
            print(f"  [warn] no exact title match from API poll (last seen: {api_title!r}); "
                  f"using latest played_at anyway")

        if not played_at:
            print("  [skip] no played_at available yet, skipping this sample")
            continue

        delta = change_time - played_at
        deltas.append(delta)
        avg = sum(deltas) / len(deltas)
        print(f"  played_at={played_at}  delta={delta:+.2f}s  "
              f"samples={len(deltas)}/{samples}  running_avg={avg:+.2f}s")

    conn.close()
    return deltas


def _drain(resp: http.client.HTTPResponse, n: int) -> bytes:
    buf = b""
    while len(buf) < n:
        chunk = resp.read(n - len(buf))
        if not chunk:
            raise SystemExit("stream closed unexpectedly while reading")
        buf += chunk
    return buf


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--api-url", default="https://radioe45.ddns.net")
    parser.add_argument("--station", default="radioe45", help="station shortcode (auto-detected if omitted)")
    parser.add_argument("--stream-url", default=None, help="override the stream URL (auto-detected via listen_url if omitted)")
    parser.add_argument("--samples", type=int, default=5)
    args = parser.parse_args()

    print(f"==> Fetching station info from {args.api_url}/api/nowplaying ...")
    np = fetch_nowplaying(args.api_url, args.station)
    station = np["station"]["shortcode"]
    stream_url = args.stream_url or np["station"].get("listen_url")
    if not stream_url:
        raise SystemExit("could not determine stream URL; pass --stream-url explicitly")

    print(f"==> Station: {station}")
    print(f"==> Stream URL: {stream_url}")
    print(f"==> Collecting {args.samples} track-transition samples (needs {args.samples} song changes)...\n")

    api = ApiPoller(args.api_url, station)
    api.start()
    time.sleep(1.5)  # let first poll land

    try:
        deltas = stream_metadata_loop(stream_url, args.samples, api)
    finally:
        api.stop_flag = True

    if deltas:
        avg = sum(deltas) / len(deltas)
        print(f"\n==> Done. Average delta over {len(deltas)} samples: {avg:+.2f}s")
        print(f"==> Recommended PlaybackLatencySeconds = {round(avg)}")
        print("==> Set it in RadioE45/ViewModels/OnAirViewModel.cs (SetLocalElapsed anchor shift)")


if __name__ == "__main__":
    main()
