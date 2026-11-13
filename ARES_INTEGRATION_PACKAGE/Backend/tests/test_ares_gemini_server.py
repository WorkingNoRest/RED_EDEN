import asyncio
import base64
import json
import os
import unittest
from types import SimpleNamespace
from unittest.mock import patch
from urllib.parse import parse_qs, urlsplit

from fastapi import HTTPException
from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect
from websockets.exceptions import ConnectionClosedError
from websockets.frames import Close

from Source import ares_gemini_server as server


class SessionAuthenticationTests(unittest.TestCase):
    def setUp(self):
        self.environment = patch.dict(os.environ, {"ARES_SESSION_SIGNING_KEY": "test-key-" + "x" * 48})
        self.environment.start()
        self.addCleanup(self.environment.stop)

    def test_issued_token_is_valid_and_short_lived(self):
        session = server._issue_session_token()
        claims = server.json.loads(server._b64url_decode(session["access_token"].split(".")[1]))

        self.assertEqual(session["expires_in"], server.SESSION_TTL_SECONDS)
        self.assertEqual(server._validate_session_token("Bearer " + session["access_token"]), claims["sub"])

    def test_tampered_token_is_rejected(self):
        parts = server._issue_session_token()["access_token"].split(".")
        parts[1] = server._b64url_encode(b'{"iss":"attacker"}')

        with self.assertRaises(HTTPException) as error:
            server._validate_session_token("Bearer " + ".".join(parts))

        self.assertEqual(error.exception.status_code, 401)

    def test_expired_token_is_rejected(self):
        with patch.object(server.time, "time", return_value=1000):
            token = server._issue_session_token()["access_token"]

        with patch.object(server.time, "time", return_value=1000 + server.SESSION_TTL_SECONDS):
            with self.assertRaises(HTTPException) as error:
                server._validate_session_token("Bearer " + token)

        self.assertEqual(error.exception.status_code, 401)

    def test_session_signing_key_is_required(self):
        with patch.dict(os.environ, {"ARES_SESSION_SIGNING_KEY": ""}):
            with self.assertRaises(HTTPException) as error:
                server._issue_session_token()

        self.assertEqual(error.exception.status_code, 503)

    def test_live_endpoint_rejects_missing_session_before_upstream(self):
        with TestClient(server.app) as client:
            try:
                with client.websocket_connect("/api/ares/live"):
                    self.fail("Unauthenticated WebSocket unexpectedly connected.")
            except WebSocketDisconnect as error:
                self.assertEqual(error.code, 1008)


class LiveProtocolTests(unittest.TestCase):
    def test_upstream_error_sanitizer_redacts_key_and_session_token(self):
        api_key = "AIza" + "SyFakeTestCredentialValue000000000"
        token = "headersegment.payloadsegment.signaturesegment"

        sanitized = server._sanitize_upstream_message(f"{api_key} Bearer {token}")

        self.assertNotIn(api_key, sanitized)
        self.assertNotIn(token, sanitized)

    def test_upstream_uri_uses_encoded_developer_api_key_query(self):
        api_key = "test-key+/=&"
        uri = server._build_upstream_ws_uri(api_key)
        parsed_uri = urlsplit(uri)

        self.assertEqual(parsed_uri.scheme, "wss")
        self.assertEqual(parsed_uri.netloc, "generativelanguage.googleapis.com")
        self.assertEqual(
            parsed_uri.path,
            "/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent",
        )
        self.assertEqual(parse_qs(parsed_uri.query), {"key": [api_key]})

    def test_setup_frame_parser_accepts_text_and_utf8_binary_json(self):
        response = '{"setupComplete":{}}'
        for frame, expected_type, expected_opcode in (
            (response, "str", "TEXT"),
            (response.encode("utf-8"), "bytes", "BINARY"),
        ):
            with self.subTest(frame_type=expected_type):
                payload, details = server._parse_upstream_setup_frame(frame)

                self.assertEqual(details["classification"], "setup_complete")
                self.assertEqual(details["frame_type"], expected_type)
                self.assertEqual(details["frame_opcode"], expected_opcode)
                self.assertEqual(details["frame_length"], len(response.encode("utf-8")))
                self.assertTrue(details["utf8_valid"])
                self.assertEqual(details["json_keys"], ["setupComplete"])
                self.assertIn("setupComplete", payload)

    def test_setup_frame_parser_classifies_error_malformed_empty_and_unexpected_frames(self):
        api_key = "test-gemini-secret"
        token = "headersegment.payloadsegment.signaturesegment"
        error_frame = json.dumps(
            {
                "error": {
                    "code": 400,
                    "status": "INVALID_ARGUMENT",
                    "message": f"key={api_key} Bearer {token}",
                }
            }
        )
        cases = (
            (error_frame, "error_envelope"),
            ("{invalid-json", "malformed_json"),
            ("", "empty_frame"),
            (b"\xff", "invalid_utf8"),
            (None, "unexpected_frame_type"),
            ('{"serverContent":{}}', "unexpected_json_envelope"),
            ("[]", "unexpected_json_value"),
        )
        for frame, expected_classification in cases:
            with self.subTest(classification=expected_classification):
                _, details = server._parse_upstream_setup_frame(frame)
                self.assertEqual(details["classification"], expected_classification)

        _, error_details = server._parse_upstream_setup_frame(error_frame)
        self.assertEqual(error_details["error_code"], 400)
        self.assertEqual(error_details["error_status"], "INVALID_ARGUMENT")
        self.assertNotIn(api_key, error_details["error_message"])
        self.assertNotIn(token, error_details["error_message"])
        _, invalid_utf8_details = server._parse_upstream_setup_frame(b"\xff")
        self.assertFalse(invalid_utf8_details["utf8_valid"])
        unicode_error = json.dumps(
            {"error": {"code": 400, "status": "INVALID_ARGUMENT", "message": "خطأ"}},
            ensure_ascii=False,
        ).encode("utf-8")
        _, unicode_error_details = server._parse_upstream_setup_frame(unicode_error)
        self.assertEqual(unicode_error_details["classification"], "error_envelope")
        self.assertTrue(unicode_error_details["utf8_valid"])
        self.assertEqual(unicode_error_details["error_message"], "خطأ")

    def test_live_setup_is_audio_only_and_uses_allowlisted_read_only_tools(self):
        setup = server._build_live_setup("Egyptian Arabic")["setup"]

        self.assertEqual(setup["model"], "models/gemini-3.8-live")
        self.assertEqual(setup["generationConfig"]["responseModalities"], ["AUDIO"])
        self.assertTrue(setup["realtimeInputConfig"]["automaticActivityDetection"]["disabled"])
        self.assertEqual(
            {tool["name"] for tool in setup["tools"][0]["functionDeclarations"]},
            server.ALLOWED_LIVE_TOOLS,
        )
        self.assertEqual(setup["inputAudioTranscription"], {})
        self.assertEqual(setup["outputAudioTranscription"], {})

    def test_invalid_language_and_control_payloads_are_rejected(self):
        with self.assertRaises(ValueError):
            server._build_live_setup("x" * 33)
        with self.assertRaises(ValueError):
            server._parse_client_message("{" + " " * server.MAX_CONTROL_MESSAGE_BYTES + "}")
        with self.assertRaises(ValueError):
            server._parse_client_message('{"not_type":"ping"}')

    def test_only_declared_read_only_function_calls_are_forwarded(self):
        source = json.dumps(
            {"toolCall": {"functionCalls": [{"id": "call-1", "name": "get_inventory", "args": {}}]}}
        )
        pending, calls = server._parse_function_calls(source)

        self.assertEqual(pending, {"call-1": "get_inventory"})
        self.assertEqual(calls[0]["name"], "get_inventory")
        with self.assertRaises(ValueError):
            server._parse_function_calls(
                json.dumps({"toolCall": {"functionCalls": [{"id": "call-2", "name": "delete_save"}]}})
            )

    def test_live_connection_attempts_are_rate_limited(self):
        address = "test-live-rate-limit"
        self.assertTrue(
            all(server._allow_live_attempt(address) for _ in range(server.MAX_LIVE_ATTEMPTS_PER_MINUTE))
        )
        self.assertFalse(server._allow_live_attempt(address))

    def test_live_connection_slots_are_limited_per_ip(self):
        address = "test-live-concurrency"
        self.assertTrue(server._acquire_live_slot(address))
        self.assertTrue(server._acquire_live_slot(address))
        self.assertFalse(server._acquire_live_slot(address))
        server._release_live_slot(address)
        server._release_live_slot(address)


class FakeWebSocket:
    def __init__(self, messages):
        self.messages = iter(messages)
        self.sent_text = []
        self.sent_bytes = []

    async def receive(self):
        return next(self.messages)

    async def send_text(self, text):
        self.sent_text.append(text)

    async def send_bytes(self, data):
        self.sent_bytes.append(data)


class FakeUpstream:
    def __init__(self):
        self.sent = []
        self.close_code = None

    async def send(self, message):
        self.sent.append(json.loads(message))


class FakeSetupFailureUpstream(FakeUpstream):
    def __init__(self, response):
        super().__init__()
        self.response = response

    async def recv(self):
        return self.response

    async def close(self):
        pass


class FakeSetupSuccessUpstream(FakeSetupFailureUpstream):
    def __aiter__(self):
        return self

    async def __anext__(self):
        raise StopAsyncIteration


class FakeHandshakeError(OSError):
    def __init__(self, status_code, body):
        super().__init__("handshake rejected")
        self.status_code = status_code
        self.response = SimpleNamespace(status_code=status_code, body=body)


class FakeUpstreamMessages:
    def __init__(self, messages):
        self.messages = iter(messages)

    def __aiter__(self):
        return self

    async def __anext__(self):
        try:
            return next(self.messages)
        except StopIteration:
            raise StopAsyncIteration


class EndpointWebSocket:
    def __init__(self, authorization, address):
        self.headers = {"authorization": authorization}
        self.client = SimpleNamespace(host=address)
        self.query_params = {"language": "English"}
        self.sent_text = []
        self.accepted = False
        self.closed = False

    async def receive(self):
        return {"type": "websocket.disconnect"}

    async def accept(self):
        self.accepted = True

    async def send_text(self, text):
        self.sent_text.append(json.loads(text))

    async def close(self, code=1000, reason=None):
        self.closed = True


class LiveRelayTests(unittest.IsolatedAsyncioTestCase):
    async def test_setup_complete_is_required_before_ready(self):
        with patch.dict(
            os.environ,
            {
                "ARES_SESSION_SIGNING_KEY": "test-key-" + "x" * 48,
                "GEMINI_API_KEY": "test-gemini-key",
            },
        ):
            token = server._issue_session_token()["access_token"]
            websocket = EndpointWebSocket("Bearer " + token, "test-upstream-setup-success")
            upstream = FakeSetupSuccessUpstream('{"setupComplete":{}}')

            async def connect(*args, **kwargs):
                return upstream

            with patch.object(server.websockets, "connect", new=connect):
                with self.assertLogs("ares", level="INFO") as captured:
                    await server.live_session(websocket)

        ready_events = [event for event in websocket.sent_text if event.get("type") == "ready"]
        self.assertEqual(len(ready_events), 1)
        self.assertEqual(ready_events[0]["inputSampleRate"], 16_000)
        self.assertEqual(ready_events[0]["outputSampleRate"], 24_000)
        logs = "\n".join(captured.output)
        self.assertIn("setup_accepted=true", logs)
        self.assertIn("frame_type=str", logs)
        self.assertIn("frame_opcode=TEXT", logs)
        self.assertIn("utf8_valid=true", logs)
        self.assertIn('json_keys=["setupComplete"]', logs)

    async def test_utf8_binary_json_setup_complete_is_accepted(self):
        with patch.dict(
            os.environ,
            {
                "ARES_SESSION_SIGNING_KEY": "test-key-" + "x" * 48,
                "GEMINI_API_KEY": "test-gemini-key",
            },
        ):
            token = server._issue_session_token()["access_token"]
            websocket = EndpointWebSocket("Bearer " + token, "test-upstream-binary-setup-success")
            upstream = FakeSetupSuccessUpstream(b'{"setupComplete":{}}')

            async def connect(*args, **kwargs):
                return upstream

            with patch.object(server.websockets, "connect", new=connect):
                with self.assertLogs("ares", level="INFO") as captured:
                    await server.live_session(websocket)

        self.assertTrue(any(event.get("type") == "ready" for event in websocket.sent_text))
        logs = "\n".join(captured.output)
        self.assertIn("frame_type=bytes", logs)
        self.assertIn("frame_opcode=BINARY", logs)
        self.assertIn("utf8_valid=true", logs)
        self.assertIn("classification=setup_complete", logs)

    async def test_setup_error_logs_sanitized_protocol_diagnostics(self):
        api_key = "test-gemini-key-that-must-not-be-logged"
        with patch.dict(
            os.environ,
            {
                "ARES_SESSION_SIGNING_KEY": "test-key-" + "x" * 48,
                "GEMINI_API_KEY": api_key,
            },
        ):
            token = server._issue_session_token()["access_token"]
            websocket = EndpointWebSocket("Bearer " + token, "test-upstream-setup-error")
            upstream = FakeSetupFailureUpstream(
                json.dumps(
                    {
                        "error": {
                            "code": 400,
                            "status": "INVALID_ARGUMENT",
                            "message": f"API key={api_key} Bearer {token}",
                        }
                    }
                )
            )
            connection = {}

            async def connect(uri, **kwargs):
                connection["uri"] = uri
                connection["kwargs"] = kwargs
                return upstream

            with patch.object(server.websockets, "connect", new=connect):
                with self.assertLogs("ares", level="WARNING") as captured:
                    await server.live_session(websocket)

        self.assertEqual(parse_qs(urlsplit(connection["uri"]).query), {"key": [api_key]})
        self.assertNotIn("extra_headers", connection["kwargs"])
        self.assertIs(connection["kwargs"]["logger"], server._UPSTREAM_WS_LOGGER)
        self.assertEqual(websocket.sent_text[0]["type"], "error")
        log_output = "\n".join(captured.output)
        self.assertIn("http_status=101", log_output)
        self.assertIn("auth_accepted=accepted", log_output)
        self.assertIn("setup_accepted=false", log_output)
        self.assertIn("error_code=400", log_output)
        self.assertIn("error_status=INVALID_ARGUMENT", log_output)
        self.assertIn("frame_type=str", log_output)
        self.assertIn("frame_opcode=TEXT", log_output)
        self.assertIn("utf8_valid=true", log_output)
        self.assertIn("classification=error_envelope", log_output)
        self.assertIn('json_keys=["error"]', log_output)
        self.assertIn("api_version=v1beta", log_output)
        self.assertIn("model=gemini-3.8-live", log_output)
        self.assertNotIn(api_key, log_output)
        self.assertNotIn(token, log_output)

    async def test_handshake_rejection_logs_status_without_credentials(self):
        api_key = "test-gemini-key-that-must-not-be-logged"
        body = json.dumps(
            {
                "error": {
                    "code": 403,
                    "status": "PERMISSION_DENIED",
                    "message": f"API key={api_key} is not permitted",
                }
            }
        ).encode("utf-8")
        with patch.dict(
            os.environ,
            {
                "ARES_SESSION_SIGNING_KEY": "test-key-" + "x" * 48,
                "GEMINI_API_KEY": api_key,
            },
        ):
            token = server._issue_session_token()["access_token"]
            websocket = EndpointWebSocket("Bearer " + token, "test-upstream-handshake-error")

            async def fail_connect(*args, **kwargs):
                raise FakeHandshakeError(403, body)

            with patch.object(server.websockets, "connect", new=fail_connect):
                with self.assertLogs("ares", level="WARNING") as captured:
                    await server.live_session(websocket)

        log_output = "\n".join(captured.output)
        self.assertIn("http_status=403", log_output)
        self.assertIn("auth_accepted=rejected", log_output)
        self.assertIn("error_code=403", log_output)
        self.assertIn("error_status=PERMISSION_DENIED", log_output)
        self.assertNotIn(api_key, log_output)
        self.assertNotIn(token, log_output)

    async def test_upstream_close_code_is_recorded(self):
        with patch.dict(
            os.environ,
            {
                "ARES_SESSION_SIGNING_KEY": "test-key-" + "x" * 48,
                "GEMINI_API_KEY": "test-gemini-key",
            },
        ):
            token = server._issue_session_token()["access_token"]
            websocket = EndpointWebSocket("Bearer " + token, "test-upstream-close")
            upstream = FakeSetupFailureUpstream("")
            close_error = ConnectionClosedError(
                rcvd=Close(1008, "policy violation"),
                sent=None,
            )

            async def receive_close():
                raise close_error

            upstream.recv = receive_close

            async def connect(*args, **kwargs):
                return upstream

            with patch.object(server.websockets, "connect", new=connect):
                with self.assertLogs("ares", level="WARNING") as captured:
                    await server.live_session(websocket)

        log_output = "\n".join(captured.output)
        self.assertIn("stage=setup", log_output)
        self.assertIn("http_status=101", log_output)
        self.assertIn("close_code=1008", log_output)
        self.assertIn("auth_accepted=accepted", log_output)
        self.assertIn("message=policy violation", log_output)

    async def test_binary_pcm_activity_archive_context_and_tool_responses_are_validated(self):
        audio = b"\x01\x00" * 320
        pending = {"tool-1": "get_inventory"}
        websocket = FakeWebSocket(
            [
                {"type": "websocket.receive", "text": '{"type":"activity_start"}'},
                {"type": "websocket.receive", "bytes": audio},
                {
                    "type": "websocket.receive",
                    "text": json.dumps({"type": "context", "archiveEvidence": "Archive excerpt"}),
                },
                {
                    "type": "websocket.receive",
                    "text": json.dumps(
                        {
                            "type": "tool_response",
                            "responses": [
                                {
                                    "id": "tool-1",
                                    "name": "get_inventory",
                                    "response": {"summary": "empty"},
                                }
                            ],
                        }
                    ),
                },
                {"type": "websocket.disconnect"},
            ]
        )
        upstream = FakeUpstream()
        deadline = asyncio.get_running_loop().time() + 10

        await server._client_to_upstream(websocket, upstream, pending, deadline)

        self.assertEqual(upstream.sent[0], {"realtimeInput": {"activityStart": {}}})
        self.assertEqual(
            base64.b64decode(upstream.sent[1]["realtimeInput"]["audio"]["data"]),
            audio,
        )
        self.assertEqual(
            upstream.sent[1]["realtimeInput"]["audio"]["mimeType"],
            "audio/pcm;rate=16000",
        )
        self.assertIn("Untrusted local NASA archive evidence", upstream.sent[2]["clientContent"]["turns"]["parts"][0]["text"])
        self.assertEqual(upstream.sent[3]["toolResponse"]["functionResponses"][0]["id"], "tool-1")
        self.assertEqual(pending, {})

    async def test_tool_response_cannot_match_an_unissued_call(self):
        websocket = FakeWebSocket(
            [
                {
                    "type": "websocket.receive",
                    "text": json.dumps(
                        {
                            "type": "tool_response",
                            "responses": [
                                {"id": "forged", "name": "get_inventory", "response": {"summary": "x"}}
                            ],
                        }
                    ),
                }
            ]
        )

        with self.assertRaises(ValueError):
            await server._client_to_upstream(
                websocket,
                FakeUpstream(),
                {},
                asyncio.get_running_loop().time() + 10,
            )

    async def test_upstream_events_forward_transcripts_audio_interruptions_and_tools(self):
        pcm = b"\x01\x00\x02\x00"
        source = [
            json.dumps(
                {
                    "serverContent": {
                        "inputTranscription": {"text": "status"},
                        "modelTurn": {
                            "parts": [
                                {
                                    "inlineData": {
                                        "mimeType": "audio/pcm;rate=24000",
                                        "data": base64.b64encode(pcm).decode("ascii"),
                                    }
                                }
                            ]
                        },
                    }
                }
            ),
            json.dumps({"serverContent": {"interrupted": True}}),
            json.dumps(
                {"toolCall": {"functionCalls": [{"id": "tool-2", "name": "get_player_status", "args": {}}]}}
            ),
            json.dumps({"serverContent": {"turnComplete": True}}),
        ]
        websocket = FakeWebSocket([])
        pending = {}

        await server._upstream_to_client(websocket, FakeUpstreamMessages(source), pending)

        events = [json.loads(value) for value in websocket.sent_text]
        self.assertEqual(websocket.sent_bytes, [pcm])
        self.assertIn({"type": "input_transcript", "text": "status", "interim": False}, events)
        self.assertIn({"type": "speaking", "value": True}, events)
        self.assertIn({"type": "interrupted"}, events)
        self.assertIn({"type": "tool_calls", "calls": [{"id": "tool-2", "name": "get_player_status", "args": {}}]}, events)
        self.assertIn({"type": "turn_complete"}, events)
        self.assertEqual(pending, {"tool-2": "get_player_status"})

    async def test_upstream_connection_failure_notifies_client_and_releases_capacity(self):
        with patch.dict(
            os.environ,
            {
                "ARES_SESSION_SIGNING_KEY": "test-key-" + "x" * 48,
                "GEMINI_API_KEY": "test-key",
            },
        ):
            token = server._issue_session_token()["access_token"]
            websocket = EndpointWebSocket("Bearer " + token, "test-upstream-failure")
            initial_count = server._live_active_total

            async def fail_connect(*args, **kwargs):
                raise OSError("connection refused")

            with patch.object(server.websockets, "connect", new=fail_connect):
                await server.live_session(websocket)

        self.assertTrue(websocket.accepted)
        self.assertTrue(websocket.closed)
        self.assertEqual(websocket.sent_text[0]["type"], "error")
        self.assertEqual(server._live_active_total, initial_count)

    async def test_binary_audio_frame_size_and_alignment_are_enforced(self):
        for invalid_audio in (b"\x00", b"\x00\x00" * (server.MAX_AUDIO_CHUNK_BYTES // 2 + 1)):
            websocket = FakeWebSocket(
                [{"type": "websocket.receive", "bytes": invalid_audio}]
            )
            with self.assertRaises(ValueError):
                await server._client_to_upstream(
                    websocket,
                    FakeUpstream(),
                    {},
                    asyncio.get_running_loop().time() + 10,
                )


if __name__ == "__main__":
    unittest.main()
