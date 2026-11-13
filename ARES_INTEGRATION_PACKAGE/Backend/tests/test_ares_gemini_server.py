import os
import unittest
from unittest.mock import patch

from fastapi import HTTPException
from fastapi.testclient import TestClient
from pydantic import ValidationError

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
        token = ".".join(parts)

        with self.assertRaises(HTTPException) as error:
            server._validate_session_token("Bearer " + token)

        self.assertEqual(error.exception.status_code, 401)

    def test_expired_token_is_rejected(self):
        with patch.object(server.time, "time", return_value=1000):
            token = server._issue_session_token()["access_token"]

        with patch.object(server.time, "time", return_value=1000 + server.SESSION_TTL_SECONDS):
            with self.assertRaises(HTTPException) as error:
                server._validate_session_token("Bearer " + token)

        self.assertEqual(error.exception.status_code, 401)

    def test_chat_request_rejects_unexpected_and_oversized_fields(self):
        with self.assertRaises(ValidationError):
            server.ChatRequest(prompt="Question", model="attacker-selected-model")

        with self.assertRaises(ValidationError):
            server.ChatRequest(prompt="Q" * 4001)

    def test_session_signing_key_is_required(self):
        with patch.dict(os.environ, {"ARES_SESSION_SIGNING_KEY": ""}):
            with self.assertRaises(HTTPException) as error:
                server._issue_session_token()

        self.assertEqual(error.exception.status_code, 503)

    def test_chat_endpoint_requires_a_session_token(self):
        with TestClient(server.app) as client:
            response = client.post("/api/ares/chat", json={"prompt": "status"})

        self.assertEqual(response.status_code, 401)

    def test_session_endpoint_applies_per_ip_rate_limit(self):
        with TestClient(server.app) as client:
            responses = [client.post("/api/ares/session", json={}) for _ in range(6)]

        self.assertTrue(all(response.status_code == 200 for response in responses[:5]))
        self.assertEqual(responses[5].status_code, 429)

    def test_oversized_request_body_is_rejected(self):
        with TestClient(server.app) as client:
            response = client.post(
                "/api/ares/chat",
                json={"prompt": "Q", "gameContext": "x" * server.MAX_REQUEST_BYTES},
            )

        self.assertEqual(response.status_code, 413)


if __name__ == "__main__":
    unittest.main()
