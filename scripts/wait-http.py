"""Bounded readiness polling, without hiding a failed final request."""
import sys
import time
import urllib.request

for attempt in range(100):
    try:
        with urllib.request.urlopen(sys.argv[1], timeout=1) as response:
            if response.status == 200:
                sys.exit(0)
    except (OSError, TimeoutError):
        pass
    time.sleep(0.1)
raise SystemExit('HTTP readiness did not succeed within the allotted window')
