"""Minimal stage-1 client. Requires a running backend and connected Rhino plugin."""

import argparse
import json
import urllib.request

BASE_URL = "http://127.0.0.1:8766"


def request(method: str, path: str, body: dict | None = None) -> object:
    data = json.dumps(body).encode() if body is not None else None
    http_request = urllib.request.Request(
        BASE_URL + path,
        data=data,
        method=method,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(http_request, timeout=35) as response:
        return json.loads(response.read())


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["sessions", "scene", "box"])
    parser.add_argument("--session")
    args = parser.parse_args()

    if args.command == "sessions":
        print(json.dumps(request("GET", "/api/sessions"), indent=2))
        return
    if not args.session:
        parser.error("--session is required for scene and box")

    if args.command == "scene":
        command = {"type": "get_scene", "payload": {"max_objects": 500}}
    else:
        command = {
            "type": "execute_batch",
            "payload": {
                "label": "RHINO Minion smoke test",
                "operations": [
                    {
                        "operation_id": "box-1",
                        "tool": "create_box",
                        "arguments": {
                            "origin": [0, 0, 0],
                            "width": 30000,
                            "depth": 20000,
                            "height": 80000,
                            "layer": "AI_Massing",
                            "name": "Tower_Base",
                        },
                    }
                ],
            },
        }
    result = request("POST", f"/api/sessions/{args.session}/execute", command)
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
