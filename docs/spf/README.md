# SPF (publication profiles)

SPF is a **publication profile** of `U.AppliedDiscipline` (not a new root entity).

## Profiles (strictness)

| Profile | Intent | Minimum output |
| --- | --- | --- |
| **SPF-Min** | Smallest usable vertical slice | CHR + CAL + ≥1 `E.TGA` crossing + ≥1 realization case |
| **SPF-Lite** | Practical pack with SoTA echoing | SPF‑Min + SoTA‑Echoing sections (projection over SoTA sources) |
| **SPF-Full** | Strongest publication profile | SPF‑Lite + full conformance + broader crossings/realizations |

## Where artifacts live

- Task statement: `docs/spf/SPF-zadanie.md`
- Decisions: `docs/decisions/DRR-0001-spf-g14.md`
- Discipline packs: `docs/spf/packs/<discipline>/`
- Realizations (cases): `docs/spf/packs/<discipline>/realizations/`

## Terminology (frozen)

- **Profile** = strictness level over the same discipline pack (Min/Lite/Full).
- **SoTA-Pack** = curated source bundle (citations + adopt/adapt/reject decisions).
- **SoTA-Echoing** = projection/face over SoTA data (no duplicated SoTA).
- **ATS** = legacy hook route; keep via compatibility, migrate via `E.TGA` crossings.
