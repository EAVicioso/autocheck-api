"""Keep CI strict while permitting the specific SRS 1.1 QR response correction."""
import json
import subprocess
import sys

oasdiff, baseline, current = sys.argv[1:]
result = subprocess.run(
    [oasdiff, "breaking", baseline, current, "--format", "json", "--fail-on", "ERR"],
    capture_output=True, text=True, encoding="utf-8", check=False,
)
if result.stderr:
    print(result.stderr, file=sys.stderr, end="")
if result.stdout:
    print(result.stdout, end="")
if result.returncode == 0:
    sys.exit(0)
if result.returncode != 1:
    sys.exit(result.returncode)

changes = json.loads(result.stdout)
errors = [change for change in changes if change["level"] >= 3]
approved = (
    len(errors) == 1
    and errors[0]["id"] == "response-required-property-removed"
    and errors[0]["operation"] == "GET"
    and errors[0]["operationId"] == "VerificarCertificado"
    and errors[0]["path"] == "/api/certificados/{id}/verificacion"
    and errors[0]["text"] == "removed the required property `integridad` from the response with the `200` status"
)
if approved:
    print("Approved RF-CER-01 / PA-07a correction: remove the extra public QR field.")
sys.exit(0 if approved else 1)
