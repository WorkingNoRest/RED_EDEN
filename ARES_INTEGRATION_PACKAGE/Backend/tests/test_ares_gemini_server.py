import asyncio
import base64
import json
import os
import unittest
from types import SimpleNamespace
from unittest.mock import patch

from fastapi import HTTPException
from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect

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

    async def send(self, message):
        self.sent.append(json.loads(message))


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

    async def accept(self):
        self.accepted = True

    async def send_text(self, text):
        self.sent_text.append(json.loads(text))

    async def close(self, code=1000, reason=None):
        self.closed = True


class LiveRelayTests(unittest.IsolatedAsyncioTestCase):
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
