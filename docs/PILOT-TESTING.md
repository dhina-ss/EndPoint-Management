# EMS Pilot Test Guide — 3 PCs, one API, NeonDB

Scenario: **EMS.Agent installed on 3 Windows PCs → one EMS.API instance → Neon PostgreSQL.**

Conventions used below:

- `$API` = your API base URL (e.g. `https://ems-api.yourdomain.com` or `http://<server>:5102`)
- **PC-1 / PC-2 / PC-3** = the three test machines
- SQL runs in the **Neon console → SQL Editor**
- Agent logs: **Event Viewer → Windows Logs → Application → source "EMS Endpoint Agent"**
- Agent files: `C:\Program Files\EMS Agent\` (binaries+config), `C:\ProgramData\EMS.Agent\` (identity: `device-id.json`, `device-auth.json`)

Record results in the checklist at the bottom.

---

## Phase 0 — Prerequisites (before touching the PCs)

| # | Step | Expected |
|---|------|----------|
| 0.1 | Migrations applied to Neon: `dotnet ef database update --project EMS.API` (with the Neon connection string active) | `Done.` — Neon shows tables `devices`, `device_authentications`, `device_heartbeats`, `__EFMigrationsHistory` |
| 0.2 | API running; open `$API/health` in a browser | `Healthy` |
| 0.3 | API startup log | `Database connection verified: neondb on tcp://ep-…neon.tech:5432.` |
| 0.4 | Installer built **with the production `BaseUrl` baked into `appsettings.json`** before compiling (`installer\EMS.Agent.iss`) | `EMSAgentSetup-1.0.0.exe` exists; publish-folder `appsettings.json` has `"BaseUrl": "$API"` |
| 0.5 | From **each** of the 3 PCs: `curl.exe $API/health` | `Healthy` from all three (rules out firewall/DNS issues before installing) |
| 0.6 | Get an **observer token** for API queries (GET /api/devices requires device credentials). From your admin console: register a synthetic device and save the returned token: | 200, `"success":true`, token returned |

```bat
curl -X POST $API/api/devices/register -H "Content-Type: application/json" ^
     -d "{\"deviceId\":\"TEST-CONSOLE\",\"deviceName\":\"OBSERVER\",\"serialNumber\":\"TEST\"}"
```

Save the `token` value → referred to as `$OBS_TOKEN`. Query helper used throughout:

```bat
curl -s $API/api/devices -H "X-Device-Id: TEST-CONSOLE" -H "X-Device-Token: %OBS_TOKEN%"
```

> Note: each re-registration of TEST-CONSOLE **rotates** its token — re-save it if you re-register.

---

## Phase 1 — Device registration (PC-1 first, then all three)

| # | Step | Expected |
|---|------|----------|
| 1.1 | On PC-1 run `EMSAgentSetup-1.0.0.exe` as Administrator | Installs to `C:\Program Files\EMS Agent\`; no errors |
| 1.2 | `sc query EMSAgent` | `STATE: RUNNING`; `sc qc EMSAgent` shows `START_TYPE: AUTO_START` |
| 1.3 | Event Viewer on PC-1 | `EMS Agent started…`, `Generated new DeviceId <guid>`, `Device registered successfully`, `Heartbeat worker started` |
| 1.4 | Files exist: `C:\ProgramData\EMS.Agent\device-id.json` and `device-auth.json` | Both present; device-id.json holds a GUID — **write it down per PC** |
| 1.5 | Query devices (observer helper above) | PC-1 appears with correct `deviceName`, manufacturer, model, serial, RAM, storage, OS |
| 1.6 | Neon SQL: `SELECT "DeviceId","DeviceName","SerialNumber","CreatedDate" FROM devices;` | One row per registered device — **data is in Neon**, not local |
| 1.7 | Repeat 1.1–1.6 on PC-2 and PC-3 | 3 distinct rows, 3 **different** DeviceId GUIDs |
| 1.8 | Reboot PC-1 | Service auto-starts; Event Viewer shows `Loaded existing DeviceId` (same GUID — identity survived) |

**Pass = 3 devices, 3 unique GUIDs, hardware details correct, rows visible in Neon.**

---

## Phase 2 — Authentication

| # | Step | Expected |
|---|------|----------|
| 2.1 | `curl -s -o NUL -w "%%{http_code}" $API/api/devices` (no headers) | **401** `{"success":false,"message":"Missing device credentials"}` |
| 2.2 | Same with wrong token: `-H "X-Device-Id: TEST-CONSOLE" -H "X-Device-Token: wrong"` | **401** `Invalid device credentials` |
| 2.3 | Same with valid observer credentials | **200** + device list |
| 2.4 | Heartbeat without credentials: `curl -X POST $API/api/devices/heartbeat -H "Content-Type: application/json" -d "{}"` | **401** |
| 2.5 | Neon SQL: `SELECT d."DeviceName", a."IsActive", a."LastUsedDate", left(a."TokenHash",12) FROM device_authentications a JOIN devices d ON d."Id" = a."DeviceId";` | One credential per device, `IsActive=true`, `LastUsedDate` recent, and the stored value is a **hash**, not a token |
| 2.6 | On PC-1 open `device-auth.json`, then restart the service (`sc stop EMSAgent & sc start EMSAgent`) and re-open it after the next registration | Token value **changes** after re-registration (rotation works); old token no longer authenticates |

**Pass = 401 on missing/wrong credentials, 200 on valid; DB stores hashes only; rotation invalidates old tokens.**

---

## Phase 3 — Heartbeat + LastSeen

| # | Step | Expected |
|---|------|----------|
| 3.1 | Watch Event Viewer on any PC for ~3 minutes | No heartbeat errors (success logs at Debug level are hidden by default — absence of warnings = healthy) |
| 3.2 | Query devices twice, ~2 minutes apart; compare `lastHeartbeatTime` | Advances by ~60 s per beat on **all 3** devices |
| 3.3 | Compare `lastSeen` in the same responses | Advances together with heartbeats (also bumped by 10-minute inventory registrations) |
| 3.4 | Neon SQL history: | ~1 row/minute/device accumulating; IP + username + `agentVersion: 1.0.0` populated |

```sql
SELECT d."DeviceName", count(*) AS beats, max(h."HeartbeatTime") AS latest
FROM device_heartbeats h JOIN devices d ON d."Id" = h."DeviceId"
GROUP BY d."DeviceName";
```

| 3.5 | Leave running 30+ minutes; repeat 3.4 | Counts grow ≈ +30/device; no gaps, no service restarts in Event Viewer |

**Pass = steady 1/min heartbeats from all 3 PCs, LastSeen/LastHeartbeatTime advancing, history rows in Neon.**

---

## Phase 4 — Duplicate DeviceId handling

Two distinct cases — test both:

**4A. Benign duplicate (same machine re-registers) — this is normal operation:**

| # | Step | Expected |
|---|------|----------|
| 4A.1 | On PC-1: `sc stop EMSAgent & sc start EMSAgent`, three times | Each start logs `Loaded existing DeviceId` + `Device registered successfully` |
| 4A.2 | Neon SQL: `SELECT count(*) FROM devices;` | **Still 3 (+1 observer)** — no duplicate rows; unique index on `DeviceId` + idempotent upsert |
| 4A.3 | Check PC-1's `UpdatedDate` in devices | Updated at each re-registration; `CreatedDate` unchanged |

**4B. Cloned identity (two machines share one DeviceId) — simulates disk-image cloning. Test machines only:**

| # | Step | Expected |
|---|------|----------|
| 4B.1 | Stop the service on PC-2. Back up `C:\ProgramData\EMS.Agent\*` on PC-2. Copy `device-id.json` **from PC-1 to PC-2** (delete PC-2's `device-auth.json`). Start PC-2's service | PC-2 now registers under PC-1's DeviceId |
| 4B.2 | Query devices | Still no crash, no duplicate row — the shared row's `deviceName` **flaps** between PC-1 and PC-2 as each re-registers (upsert overwrites) |
| 4B.3 | Event Viewer on the PC that registered **least recently** | `Heartbeat rejected as unauthorized…` warnings — each registration rotates the shared token, cutting the other machine off until its next cycle. System remains stable; no crashes |
| 4B.4 | **Recover**: stop PC-2's service, restore its backed-up `ProgramData` files (or delete both JSON files to mint a fresh identity), start service | PC-2 back as its own device; PC-3 row count correct; flapping stops |

**Pass = no duplicate rows, no crashes; cloned identity degrades visibly (flapping + 401 warnings) and recovers cleanly. Lesson for rollout: never bake `C:\ProgramData\EMS.Agent` into disk images.**

---

## Phase 5 — Offline device detection

| # | Step | Expected |
|---|------|----------|
| 5.1 | Note current `lastHeartbeatTime` for all 3. On **PC-3**: `sc stop EMSAgent` | Service stops; Event Viewer: `EMS Agent stopped.` |
| 5.2 | Wait 5 minutes; query devices | PC-1/PC-2 `lastHeartbeatTime` still advancing; **PC-3 frozen** at stop time |
| 5.3 | Offline query in Neon (threshold = 3 min ≈ 3 missed beats): | Returns **exactly PC-3** |

```sql
SELECT "DeviceName", "LastHeartbeatTime",
       now() - "LastHeartbeatTime" AS silent_for
FROM devices
WHERE "LastHeartbeatTime" < now() - interval '3 minutes'
   OR "LastHeartbeatTime" IS NULL;
```

| 5.4 | Pull PC-2's network cable (or disable Wi-Fi) instead of stopping the service; wait 5 min | Same result via a different failure mode; PC-2's Event Viewer fills with `could not reach the EMS server` warnings but the **service keeps running** |
| 5.5 | Restore network on PC-2, `sc start EMSAgent` on PC-3 | Both resume within ~1 min; offline query returns empty; no manual intervention needed |

**Pass = silent devices identifiable by threshold query; agents self-recover after outages.**
> Note: detection is currently a SQL/dashboard-side query — a dedicated `GET /api/devices/offline` endpoint is planned, not yet built.

---

## Phase 6 — Cleanup (optional)

| # | Step | Expected |
|---|------|----------|
| 6.1 | On one PC: Start Menu → **Uninstall EMS Agent** | Service stopped and deleted (`sc query EMSAgent` → not found); `C:\Program Files\EMS Agent\` removed |
| 6.2 | Check `C:\ProgramData\EMS.Agent\` | **Intentionally kept** — reinstalling preserves the device's server identity. Delete manually for a truly clean slate |
| 6.3 | Remove test rows in Neon: `DELETE FROM devices WHERE "DeviceId" IN ('TEST-CONSOLE');` | Cascade removes its credential + heartbeats |

---

## Results checklist

- [ ] 0. All 3 PCs reach `$API/health` = Healthy; migrations applied to Neon
- [ ] 1. Registration: 3 devices, 3 unique GUIDs, correct hardware inventory, rows in Neon
- [ ] 1b. Identity survives reboot (same DeviceId after restart)
- [ ] 2. Auth: 401 missing credentials / 401 wrong token / 200 valid token
- [ ] 2b. DB stores token hashes only; rotation invalidates old tokens
- [ ] 3. Heartbeats ~60 s from all 3; `lastHeartbeatTime` + `lastSeen` advancing; history in `device_heartbeats`
- [ ] 4a. Service restarts create **no** duplicate device rows
- [ ] 4b. Cloned DeviceId: no crash, visible flapping + 401 warnings, clean recovery
- [ ] 5. Stopped/disconnected device found by 3-minute offline query; auto-recovers
- [ ] 6. Uninstall removes service + files, preserves ProgramData identity
