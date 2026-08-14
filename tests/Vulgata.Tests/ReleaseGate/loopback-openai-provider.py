#!/usr/bin/env python3
"""Isolated stdlib OpenAI-compatible Worker/Chat fixture for Story 11.5."""

import argparse
import hashlib
import json
import threading
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def _chat_selection(request):
    payload = _user_payload(request)
    candidates = payload.get("candidates", [])
    handle = candidates[0].get("candidateHandle", "k1") if candidates else "k1"
    return {
        "schemaVersion": "prepared-chat-answer-v1",
        "requestId": payload.get("requestId", "request"),
        "mode": payload.get("mode", "business"),
        "evidenceFingerprint": payload.get("evidenceFingerprint", "0" * 64),
        "answerability": "supported",
        "selections": [{"candidateHandle": handle, "order": 1}],
    }


def _user_payload(request):
    messages = request.get("messages", [])
    user = next(message for message in messages if message.get("role") == "user")
    return json.loads(user["content"])


def _stable_id(prefix, value):
    digest = hashlib.sha256(f"{prefix}\n{value}".encode("utf-8")).digest()
    return str(uuid.UUID(bytes=digest[:16]))


def _pass1(request):
    payload = _user_payload(request)
    source = payload["source"]
    code_unit_id = payload["codeUnitId"]
    anchor_id = _stable_id("story115-anchor", code_unit_id)
    evidence = sorted(
        payload.get("approvedCommunicationEvidence", []),
        key=lambda item: item["evidenceId"],
    )
    sections = [{
        "stableSectionId": _stable_id("story115-section", code_unit_id),
        "order": 1,
        "kind": "TechnicalDetail",
        "heading": "处理逻辑",
        "markdown": "按固定提交的代码切片生成代码逻辑文档。",
        "searchText": "固定提交 代码切片 代码逻辑",
        "sourceAnchorIds": [anchor_id],
        "documentVersionIds": [],
    }]
    sections.extend({
        "stableSectionId": item["evidenceId"],
        "order": index + 2,
        "kind": "Evidence",
        "heading": item["kind"],
        "markdown": item["description"],
        "searchText": item["description"],
        "sourceAnchorIds": [anchor_id],
        "documentVersionIds": [],
    } for index, item in enumerate(evidence))
    return {
        "jobId": payload["jobId"],
        "systemId": payload["systemId"],
        "repositoryId": payload["repositoryId"],
        "scanRunId": payload["scanRunId"],
        "codeUnitId": code_unit_id,
        "profiledRevision": payload["profiledRevision"],
        "repositoryRelativePath": source["repositoryRelativePath"],
        "lineStart": source["lineStart"],
        "lineEnd": source["lineEnd"],
        "sourceHash": source["sourceHash"],
        "fileIdentityHash": source["fileIdentityHash"],
        "content": {
            "schemaIdentity": "vulgata.knowledge-document/1.0",
            "documentType": "CodeLogic",
            "stableKey": f"story115.code-unit.{code_unit_id}",
            "title": f"代码逻辑 {code_unit_id}",
            "repositoryRelativePath": source["repositoryRelativePath"],
            "summary": "说明固定提交中准备代码单元的确定性处理。",
            "keySymbols": [source["symbolName"]],
            "businessTerms": ["账户", "渠道"],
            "technicalTerms": ["Worker Agent"],
            "sections": sections,
        },
        "sourceAnchors": [{
            "id": anchor_id,
            "repositoryRelativePath": source["repositoryRelativePath"],
            "evidenceLocator": source["symbolName"],
            "lineStart": source["lineStart"],
            "lineEnd": source["lineEnd"],
            "sourceHash": source["sourceHash"],
        }],
        "referenceDocumentVersionIds": [],
        "communicationEvidenceIds": [item["evidenceId"] for item in evidence],
    }


def _pass2(request):
    payload = _user_payload(request)
    documents = payload["codeLogicDocumentVersions"]
    document_ids = sorted(item["documentVersionId"] for item in documents)
    anchors = sorted(
        (anchor for item in documents for anchor in item["sourceAnchors"]),
        key=lambda item: item["id"],
    )
    flow_id = payload["flowId"]
    return {
        "jobId": payload["jobId"],
        "systemId": payload["systemId"],
        "repositoryId": payload["repositoryId"],
        "scanRunId": payload["scanRunId"],
        "profiledRevision": payload["profiledRevision"],
        "flowId": flow_id,
        "purpose": "帮助业务人员理解账户与渠道的跨仓库处理流程。",
        "mainSteps": [
            {"order": 1, "description": "读取账户规则。", "traceEvidenceOrders": [1]},
            {"order": 2, "description": "应用渠道约束。", "traceEvidenceOrders": [2]},
        ],
        "outcome": "账户规则与渠道约束形成可追溯的业务结果。",
        "content": {
            "schemaIdentity": "vulgata.knowledge-document/1.0",
            "documentType": "BusinessLogic",
            "stableKey": "story115.account-channel.prepared-flow",
            "title": "账户与渠道处理流程",
            "repositoryRelativePath": "flows/account-channel-prepared-flow",
            "summary": "账户规则与渠道约束形成可追溯的业务结果。",
            "keySymbols": [],
            "businessTerms": ["账户", "渠道"],
            "technicalTerms": [],
            "sections": [{
                "stableSectionId": _stable_id("story115-business-section", flow_id),
                "order": 1,
                "kind": "ProcessStep",
                "heading": "主要步骤",
                "markdown": "先读取账户规则，再应用渠道约束。",
                "searchText": "账户 规则 渠道 约束",
                "sourceAnchorIds": [item["id"] for item in anchors],
                "documentVersionIds": document_ids,
            }],
        },
        "sourceAnchors": anchors,
        "traceEvidenceOrders": [1, 2],
        "generatedFromDocumentVersionIds": document_ids,
    }


def _worker_response(request):
    payload = _user_payload(request)
    if payload["kind"] == "Pass1CodeLogicDocument":
        return _pass1(request)
    return _pass2(request)


def _handler(kind):
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path == "/health":
                self._json(200, {"status": "ok", "kind": kind})
            else:
                self._json(404, {"error": "not_found"})

        def do_POST(self):
            if self.path != "/v1/chat/completions":
                self._json(404, {"error": "not_found"})
                return
            length = int(self.headers.get("Content-Length", "0"))
            request = json.loads(self.rfile.read(length).decode("utf-8"))
            content = (
                _chat_selection(request)
                if kind == "chat"
                else _worker_response(request)
            )
            self._json(200, {
                "id": "story115-loopback",
                "object": "chat.completion",
                "choices": [{"index": 0, "message": {
                    "role": "assistant",
                    "content": json.dumps(content, ensure_ascii=False, separators=(",", ":")),
                }, "finish_reason": "stop"}],
            })

        def log_message(self, *_):
            return

        def _json(self, status, payload):
            body = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host")
    parser.add_argument("--worker-port", type=int)
    parser.add_argument("--chat-port", type=int)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        print(json.dumps(_self_test_payload(), ensure_ascii=True, separators=(",", ":")))
        return
    if args.host is None or args.worker_port is None or args.chat_port is None:
        parser.error("--host, --worker-port, and --chat-port are required unless --self-test is used")
    servers = [
        ThreadingHTTPServer((args.host, args.worker_port), _handler("worker")),
        ThreadingHTTPServer((args.host, args.chat_port), _handler("chat")),
    ]
    for server in servers:
        threading.Thread(target=server.serve_forever, daemon=True).start()
    servers[0].serve_forever()


def _self_test_payload():
    value = "00000000-0000-0000-0000-000000000001"
    source = {
        "repositoryRelativePath": "src/Sample.cs", "lineStart": 1, "lineEnd": 2,
        "sourceHash": "a" * 64, "fileIdentityHash": "b" * 64, "symbolName": "Sample",
        "sourceText": "sample",
    }
    pass1_request = {"messages": [{"role": "user", "content": json.dumps({
        "kind": "Pass1CodeLogicDocument", "jobId": value, "systemId": value,
        "repositoryId": value, "scanRunId": value, "codeUnitId": value,
        "profiledRevision": "c" * 40, "source": source,
        "approvedCommunicationEvidence": [],
    })}]}
    pass1 = _pass1(pass1_request)
    pass2_request = {"messages": [{"role": "user", "content": json.dumps({
        "kind": "Pass2BusinessLogicDocument", "jobId": value, "systemId": value,
        "repositoryId": value, "scanRunId": value, "profiledRevision": "c" * 40,
        "flowId": value, "codeLogicDocumentVersions": [{
            "documentVersionId": value, "sourceAnchors": pass1["sourceAnchors"],
        }],
    })}]}
    chat_request = {"messages": [{"role": "user", "content": json.dumps({
        "requestId": value, "mode": "business", "evidenceFingerprint": "d" * 64,
        "question": "账户与渠道如何协作？",
        "candidates": [{"candidateHandle": "k1"}],
    })}]}
    return {"pass1": pass1, "pass2": _pass2(pass2_request), "chat": _chat_selection(chat_request)}


if __name__ == "__main__":
    main()
