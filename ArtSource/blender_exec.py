"""Send a Python file to the running Blender MCP add-on (localhost:9876) and print the reply.

Usage:  python ArtSource/blender_exec.py script.py
The script runs inside Blender; put anything you want back into the `result` dict.
Protocol (Blender Lab MCP add-on): one JSON request {"type": "execute", "code": ..., "strict_json": false} ended by a NUL byte.
"""
import json
import socket
import sys

host, port = "127.0.0.1", 9876
code = open(sys.argv[1], encoding="utf-8").read()
s = socket.create_connection((host, port), timeout=10)
s.settimeout(120)
s.sendall(json.dumps({"type": "execute", "code": code, "strict_json": False}).encode("utf-8") + b"\0")
buf = b""
while not buf.endswith(b"\0"):
    d = s.recv(65536)
    if not d:
        break
    buf += d
resp = json.loads(buf.rstrip(b"\0").decode("utf-8"))
if resp.get("status") == "ok":
    print(json.dumps(resp.get("result"), indent=1)[:6000])
else:
    print(resp.get("message", resp))
sys.exit(0 if resp.get("status") == "ok" else 1)
