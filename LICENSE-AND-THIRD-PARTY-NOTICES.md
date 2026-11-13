# License and third-party notices

This file is a dependency and distribution notice, not a grant of rights to Unity, Devion, Google, Render, NASA, or other third-party material. The repository does not include a license authorizing redistribution of those parties' products.

## RED EDEN project material

The Full-System Unity package is curated to export RED EDEN project-owned scripts, databases, models, prefabs, and documentation only. No general-purpose license for this material is granted by this notice; obtain permission from the project owner before redistributing it.

## Third-party dependencies

- **Unity and Unity Input System**: obtain and use through Unity's supported editor/package manager under the applicable Unity terms.
- **Devion Games Inventory System and related Devion assets**: required by the integration, but excluded from the repository's current Unity package release. Install from the licensed Unity account. No Devion license or redistribution authorization was found in the repository.
- **Google Gemini API / `gemini-3.8-live`**: accessed by the backend at runtime. Google's API terms, service policies, model availability, and pricing apply. No API key or Google SDK is bundled.
- **Render**: optional hosting provider; its service terms and plan limits apply. No Render credentials are included.
- **Python packages**: FastAPI, Uvicorn, python-dotenv, slowapi, websockets, httpx (test dependency), and their transitive dependencies are installed from their upstream package indexes. Their respective upstream license and notice files apply; packages are not vendored here.
- **NASA Mars knowledge records**: the optional archive data includes source labels and is informational only. NASA does not endorse RED EDEN or ARES. Check each record's source terms before reusing data outside this project.

## Distribution safeguards

- The Full-System export does not include Devion source/binaries, third-party UI/examples, Unity Starter Assets, or a full project scene/player.
- The Full-System export does not include Whisper, Ollama, local model files, model provisioning scripts, or local model configuration assets.
- The previous Core, Devion-Dependencies, and ARES-Optional `.unitypackage` artifacts are superseded and are not part of this release.
- Never publish `.env`, Gemini credentials, signing secrets, or bearer tokens.

If a third-party license later explicitly authorizes distribution, update this notice with the license name, version, scope, and required attribution before adding those files.
