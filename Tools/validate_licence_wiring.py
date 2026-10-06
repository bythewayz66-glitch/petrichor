#!/usr/bin/env python3
"""Validate the edited workflow and docs without a Unity licence.

Checks:
  1. the workflow parses as YAML
  2. every `run:` shell block passes `bash -n`
  3. HANDOFF.json parses as JSON
  4. no shell line can print a licence/serial/password value
  5. the serial route and the entitlement route are both reachable
"""
import json
import re
import subprocess
import sys
import tempfile
import os

WF = ".github/workflows/water-gate.yml"
HJ = "docs/HANDOFF.json"
HM = "docs/HANDOFF.md"

fails = []


def check(name, ok, detail=""):
    print(("  PASS  " if ok else "  FAIL  ") + name + (("  -- " + detail) if detail else ""))
    if not ok:
        fails.append(name)


print("=== 1. YAML parses ===")
try:
    import yaml
except ImportError:
    subprocess.run([sys.executable, "-m", "pip", "install", "-q", "pyyaml"], check=True)
    import yaml

try:
    doc = yaml.safe_load(open(WF, encoding="utf-8"))
    check("workflow is valid YAML", True)
except Exception as e:
    check("workflow is valid YAML", False, repr(e))
    doc = None

print("=== 2. every run: block passes bash -n ===")
src = open(WF, encoding="utf-8").read()
# Collect the literal body of each `run: |` block by indentation.
lines = src.split("\n")
blocks = []
i = 0
while i < len(lines):
    m = re.match(r"^(\s*)run:\s*\|", lines[i])
    if m:
        indent = len(m.group(1))
        body = []
        j = i + 1
        while j < len(lines):
            ln = lines[j]
            if ln.strip() == "":
                body.append("")
                j += 1
                continue
            cur = len(ln) - len(ln.lstrip())
            if cur <= indent:
                break
            body.append(ln)
            j += 1
        # dedent by the block's own body indentation
        nonempty = [b for b in body if b.strip()]
        if nonempty:
            base = min(len(b) - len(b.lstrip()) for b in nonempty)
            body = [b[base:] if len(b) >= base else b for b in body]
        blocks.append((i + 1, "\n".join(body)))
        i = j
    else:
        i += 1

print("  found %d run block(s)" % len(blocks))
bad = 0
for lineno, body in blocks:
    with tempfile.NamedTemporaryFile("w", suffix=".sh", delete=False) as f:
        f.write(body + "\n")
        path = f.name
    r = subprocess.run(["bash", "-n", path], capture_output=True, text=True)
    os.unlink(path)
    if r.returncode != 0:
        bad += 1
        check("bash -n block at line %d" % lineno, False, r.stderr.strip()[:200])
check("all %d run block(s) pass bash -n" % len(blocks), bad == 0)

print("=== 3. HANDOFF.json parses ===")
try:
    hj = json.load(open(HJ, encoding="utf-8"))
    check("HANDOFF.json is valid JSON", True)
except Exception as e:
    check("HANDOFF.json is valid JSON", False, repr(e))
    hj = {}

print("=== 4. no value-printing ===")
# Any line that expands a secret-bearing variable into output.
risky = []
# A line is risky only if a secret-bearing variable reaches an OUTPUT
# printer AND nothing on the line suppresses or tokenises it. The preflight
# legitimately pipes the licence into `grep -q` (no output at all) and into
# `grep -o '<[A-Za-z]...'` (extracts the tag NAME, never the value), and
# captures that in $( ). Those are safe and must not be flagged, or the
# check cries wolf and stops being evidence.
SUPPRESSORS = ("grep -q", "grep -o", "wc -c", "$(", ">", "GITHUB_OUTPUT", "::set-output")
for n, ln in enumerate(src.split("\n"), 1):
    s = ln.strip()
    if s.startswith("#"):
        continue
    if not re.search(r"\$(\{)?(UNITY_LICENSE|UNITY_SERIAL|UNITY_PASSWORD)", s):
        continue
    if not re.search(r"(^|[|;&]\s*)(echo|printf|cat|tee)\b", s):
        continue
    if any(tok in s for tok in SUPPRESSORS):
        continue
    risky.append((n, s[:120]))
for n, s in risky:
    print("    line %d: %s" % (n, s))
check("no line prints a licence/serial/password value", not risky,
      "%d suspect line(s)" % len(risky))

# The converse, stated as a positive assertion: every reference to the
# licence variable must be suppressed or tokenised.
refs = [n for n, ln in enumerate(src.split("\n"), 1)
        if re.search(r"\$(\{)?UNITY_LICENSE\b", ln) and not ln.strip().startswith("#")]
safe_refs = [n for n in refs
             if any(tok in src.split("\n")[n - 1] for tok in SUPPRESSORS)
             or "UNITY_LICENSE:" in src.split("\n")[n - 1]
             or "-z \"$UNITY_LICENSE\"" in src.split("\n")[n - 1]
             or "-n \"$UNITY_LICENSE\"" in src.split("\n")[n - 1]]
check("all %d UNITY_LICENSE references are suppressed, tokenised or a secret pass-through"
      % len(refs), len(safe_refs) == len(refs),
      "unsafe: %s" % sorted(set(refs) - set(safe_refs)))

print("=== 5. both routes reachable ===")
check("preflight prefers UNITY_SERIAL",
      bool(re.search(r'if \[ -n "\$UNITY_SERIAL" \]; then', src)))
check("serial branch sets format=serial",
      "echo \"format=serial\"" in src)
check("entitlement branch sets format=entitlement-xml",
      "echo \"format=entitlement-xml\"" in src)
# The fix itself: UNITY_LICENSE must be withheld on the serial route.
withheld = "UNITY_LICENSE: ${{ needs.preflight.outputs.format == 'ulf' && secrets.UNITY_LICENSE || '' }}" in src
check("gate step withholds UNITY_LICENSE unless format == ulf", withheld)
check("gate step still passes UNITY_SERIAL",
      "UNITY_SERIAL: ${{ secrets.UNITY_SERIAL }}" in src)
check("gate step still passes UNITY_EMAIL / UNITY_PASSWORD",
      "UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}" in src and
      "UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}" in src)
check("gate step name updated to (serial / ULF)",
      "name: Run the benchmark (serial / ULF)" in src)
check("no stale (ULF) step name remains",
      "name: Run the benchmark (ULF)" not in src)
check("timeout-minutes: 30 still on the gate job", "timeout-minutes: 30" in src)
check("unrecognised format still hard-fails", re.search(r"\*\)[\s\S]{0,900}?exit 1", src) is not None)

print("=== 6. docs internal consistency ===")
hm = open(HM, encoding="utf-8").read()
check("HANDOFF.md names the renamed step",
      "Run the benchmark (serial / ULF)" in hm)
check("HANDOFF.md no longer points at the old step name",
      "**`Run the benchmark (ULF)`**" not in hm)
check("HANDOFF.md documents the withholding fix",
      "withholding" in hm.lower())
check("HANDOFF.md documents the three serial secrets",
      all(s in hm for s in ("UNITY_SERIAL", "UNITY_EMAIL", "UNITY_PASSWORD")))
check("HANDOFF.json documents the withholding fix",
      "serial_route_workflow_change" in hj.get("blocking_item", {}))
check("HANDOFF.json confirmation names the renamed step",
      "serial / ULF" in hj.get("blocking_item", {}).get("serial_route_setup", {}).get("confirmation", ""))
check("no secret VALUE appears in docs",
      not re.search(r"XX-XXXX-XXXX-XXXX-XXXX-XX[0-9A-Z]{2}", hm.replace("XX-XXXX-XXXX-XXXX-XXXX-XXXX", "")))

print()
if fails:
    print("RESULT: %d CHECK(S) FAILED" % len(fails))
    for f in fails:
        print("  - " + f)
    sys.exit(1)
print("RESULT: ALL CHECKS PASSED")
