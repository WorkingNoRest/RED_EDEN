# ARES NASA tools

ARES is designed to answer scientific questions using NASA evidence and to avoid inventing unsupported measurements. The existing ARES RAG system already loads curated Mars records from `Assets/RED EDEN/ARES/Resources/MarsKnowledge.json` and any generated shards in `Assets/StreamingAssets/ARES/Knowledge`.

## Recommended flow

- Keep the curated NASA archive as the first source of truth.
- Populate additional shards using the existing `Tools/ARES/build_mars_knowledge.py` script.
- Use Gemini only as a response synthesizer, not as a source of scientific truth.
- Require caution for values like pressure, temperature, and radiation. Distinguish direct mission measurements from general summaries.

## Rules

- If the answer is not verified by the archive or the NASA source, say that the information is unavailable.
- Do not present ISS radiation measurements as Mars measurements.
- Preserve dates, units, and source context when an answer relies on a source.
