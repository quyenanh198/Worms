# Worms Clone (Online) — Kế hoạch kỹ thuật

> Trạng thái: **bản nháp v4, chờ duyệt**. Chưa có dòng code nào.
> Các mục đánh dấu ❓ là quyết định còn mở. Mục đánh dấu ✅ là đã chốt.

**Thay đổi so với v3:**
- **Build** chạy hoàn toàn trên GitHub-hosted runners (repo public nên miễn phí): Ubuntu, Windows, macOS, dùng GameCI. Mac mini không còn làm máy build.
- **Server** là một container chạy trên Mac mini (OrbStack), deploy qua `macmini-hub` giống Gunny. Chạy tại `chat.lazybutts.com/worms/` cho cả dev lẫn production. Bỏ VPS và Cloudflare Pages.
- **Tài khoản** dùng chung với app Chat (cookie `lb_session`, xác thực qua `/api/me`), xem §3.15.
- **Không có ngân sách:** chỉ dùng asset CC0. Nhân vật sâu được dựng và diễn hoạt bằng code (§3.10). Chưa có app iOS native vì cần tài khoản Apple Developer trả phí, người dùng iPhone chơi qua web (§3.14.1).
- D3, D4, D6, D7 đã chốt.

---

## 1. Mục tiêu và phạm vi

### 1.1 MVP
- **Online:** 2–4 người, mỗi người chơi trên máy riêng, mỗi người điều khiển 1 đội 4 con sâu. Tạo phòng bằng mã phòng hoặc ghép trận nhanh (quick match).
- **Nền tảng:** tất cả dùng chung một project Unity. **Web ngang hàng native** (§3.14).
  - **Web** tại `chat.lazybutts.com/worms/`, chạy được trên desktop và mobile browser. Đây là đường vào duy nhất cho iPhone.
  - **Android:** file APK cài tay (sideload). Không cần Play Store.
  - **Windows, macOS, Linux:** app chưa ký (unsigned).
  - **iOS native:** hoãn cho đến khi có tài khoản Apple Developer (99 USD/năm).
- **Tài khoản:** đăng nhập bằng tài khoản Chat (§3.15). Mời bạn chơi bằng cách gửi link phòng trong Chat.
- **Đồ họa 3D, gameplay 2.5D:** nhân vật, địa hình và hiệu ứng là 3D. Mọi chuyển động và va chạm diễn ra trên mặt phẳng X-Y (giống *Worms Revolution*).
- **Địa hình phá hủy được**, có gió, nước, lượt 45 giây, HP 100.
- **8 vũ khí** (§3.8). Có animation cầm, bắn, ném, trúng đạn, văng, lăn lộn, chết, chết đuối.
- **Âm thanh:** hiệu ứng (SFX) theo sự kiện, nhạc nền, chỉnh âm lượng.

### 1.2 Ngoài phạm vi MVP
Xếp hạng và lịch sử trận, app iOS native, Ninja Rope, Jetpack, trình sửa bản đồ, chat voice, replay, client-side prediction (§3.4.4), console. (Bot AI ban đầu nằm ở đây; đã làm ở P9, xem bảng giai đoạn.)

### 1.3 Quyết định
| # | Nội dung | Trạng thái |
|---|---|---|
| D1 | Gameplay 2.5D: đồ họa 3D, gameplay trên mặt phẳng 2D | ✅ |
| D2 | Engine: **Unity 6 LTS, URP, C#** | ✅ |
| D3 | Tài khoản dùng chung với app Chat (`chat.lazybutts.com`), §3.15 | ✅ |
| D4 | Không có ngân sách: chỉ dùng asset **CC0**. Nhân vật sâu được dựng và diễn hoạt bằng code. Không dùng Asset Store, vì EULA của Asset Store cấm đưa asset vào repo public | ✅ |
| D6 | 8 vũ khí như §3.8. Sẽ mở rộng sau | ✅ |
| D7 | Mỗi phase là 1 PR | ✅ |
| D8 | Web ngang hàng native, theo định nghĩa ở §3.14 | ✅ |
| D9 | Build trên GitHub-hosted runners. Server là container trên Mac mini (OrbStack, `macmini-hub`), dùng cho cả dev và production | ✅ |
| D10 | iOS native: hoãn, người dùng iPhone chơi qua web | ✅ (xem lại khi có ngân sách) |

**Pháp lý:** "Worms" và tên các vũ khí đặc trưng (Holy Hand Grenade, Super Sheep…) là tài sản của Team17. Nếu phát hành, phải đổi tên game và không dùng asset gốc.

---

## 2. Tech stack

### 2.1 Stack
| Lớp | Công nghệ | Vai trò |
|---|---|---|
| Engine client | **Unity 6 LTS** (bản LTS mới nhất tại thời điểm làm P0), **URP**, rendering path **Forward** | Forward+ không chạy được trên WebGL2, nên dùng Forward để giữ đồng nhất với web (§3.14) |
| Ngôn ngữ | **C#**, dùng ở cả client và server | Code mô phỏng viết một lần, chạy ở cả hai nơi |
| Code mô phỏng dùng chung | Local UPM package `com.worms.sim`, `com.worms.protocol`. Target `netstandard2.1`, C# 9 | Unity dùng qua `Packages/manifest.json` (`file:`). .NET dùng qua `.csproj`. Asmdef đặt `noEngineReferences: true` nên **compiler chặn** mọi tham chiếu tới `UnityEngine` |
| Game server | **.NET 10 (LTS)**, ASP.NET Core Kestrel, WebSocket | Nhẹ. Chạy trong container linux-arm64 trên Mac mini (OrbStack) |
| Transport | **Chỉ WebSocket** cho mọi client | Trình duyệt không dùng được UDP. Game theo lượt nên TCP đủ tốt, và chỉ phải bảo trì một đường truyền |
| WebSocket phía client | NativeWebSocket (mã nguồn mở, chạy cả WebGL lẫn native) | — |
| Serialize | Codec nhị phân tự viết trong `com.worms.protocol` (BinaryWriter/Reader) | Không phụ thuộc thư viện, chạy an toàn với IL2CPP và WebGL |
| Render | Shader URP viết tay bằng HLSL, Particle System (Shuriken), URP Decal (chế độ Screen Space), Post-processing Volume. Camera tự viết | Tất cả đều chạy được trên WebGL2 (§3.14). Không dùng Shader Graph (file graph khó viết và review bằng tay) và Cinemachine (§3.16) |
| Tính toán nặng | C# thường, chia chunk 64×64 | Web chạy đơn luồng. Chỉ dùng Burst nếu đo thấy chậm (§3.16) |
| Tải asset | Một bản build duy nhất. Chỉ thêm Addressables nếu bản Web vượt 30 MB | — |
| Test | xUnit cho sim, protocol và server (**Claude tự chạy được** trong container). Unity Test Framework cho EditMode và PlayMode (chạy trên CI qua GameCI) | — |
| CI/CD | GitHub Actions trên **GitHub-hosted runners** (miễn phí vì repo public), dùng **GameCI** (`game-ci/unity-test-runner`, `game-ci/unity-builder`) | §3.13 |
| Hạ tầng | Container `ghcr.io/quyenanh198/worms` (linux/arm64) chạy trên Mac mini bằng OrbStack, khai báo trong `macmini-hub`. Đi qua Caddy và cloudflared (Cloudflare Tunnel) sẵn có. **Cùng container phục vụ cả bản web** | Giống Gunny |
| Tài khoản | App Chat: server game gọi `http://chat:8082/api/me` bằng cookie `lb_session` | §3.15 |
| Lưu trữ | Không có ở MVP (danh tính lấy từ Chat). Lịch sử trận để sau (SQLite hoặc Postgres, giống Gunny) | — |
| Asset | Chỉ CC0: Poly Haven và ambientCG (texture PBR, HDRI), Quaternius và Kenney (model môi trường, vũ khí), Kenney Audio và Freesound CC0 (âm thanh). Giọng nói tạo bằng TTS-Studio trên Mac mini hoặc tự thu | §3.10, §3.11 |

### 2.2 Vì sao chọn Unity (tóm tắt)
- Đồ họa đẹp và hiệu năng mobile đã được kiểm chứng nhiều.
- Build được mọi nền tảng trên GitHub Actions bằng GameCI. Mỗi nền tảng dùng IL2CPP trên runner cùng hệ điều hành (§3.13).
- Server .NET dùng chung code C# mô phỏng với client.
- **Đánh đổi:** Claude không chạy được Unity Editor trong container cloud. Test và build Unity chạy trên CI. Để giảm việc phải thao tác trong Editor, scene và prefab được dựng bằng code càng nhiều càng tốt (§5.4).

---

## 3. Architecture

### 3.1 Tổng quan hệ thống
```
 ┌──────── Unity Client (Web / Windows / macOS / Linux / Android) ──────────┐
 │ Input (phím, chuột, cảm ứng) ──► Command ─────────────────┐              │
 │                                                           │ WebSocket    │
 │ Render URP ◄── Interpolation ◄── Snapshot + Events ◄──────┼───┐          │
 │ Audio/VFX  ◄──────── Event scheduler ◄────────────────────┘   │          │
 └───────────────────────────────────────────────────────────────┼──────────┘
      wss://chat.lazybutts.com/worms/ws  (cookie lb_session)     │
 ┌─ Mac mini · OrbStack · macmini-hub ───────────────────────────┼──────────┐
 │ cloudflared ─► Caddy ── handle_path /worms/* ─────────────────┘          │
 │                  │                                                       │
 │  ┌───────────── container worms (.NET 10, Kestrel) ───────────────────┐  │
 │  │ /            bản build Unity Web (tĩnh, Brotli, .unityweb)         │  │
 │  │ /ws          Lobby ─► MatchRoom (mỗi trận 1 vòng lặp)              │  │
 │  │                validate ─► Sim.Step() 60Hz ─► Snapshot 20Hz + Event│  │
 │  │ /healthz                                                           │  │
 │  └──────── GET /api/me (Cookie) ──► container chat:8082 ──────────────┘  │
 └──────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Cấu trúc repo
```
shared/
  com.worms.sim/            # UPM package, C# THUẦN (asmdef noEngineReferences)
    Runtime/ Rng.cs Constants.cs Terrain.cs Body.cs Worm.cs Weapons.cs
             Behaviors/{Ballistic,Hitscan,Melee,Placed,Airstrike}.cs
             Explosion.cs Turn.cs World.cs     # World.Step(commands) -> events
  com.worms.protocol/       # Command, Event, Snapshot, codec nhị phân, PROTOCOL_VERSION
  Sim.csproj Protocol.csproj  # netstandard2.1, compile từ Runtime/** của từng package
  Sim.Tests/ Protocol.Tests/  # xUnit
server/
  Server/                   # Program.cs, ChatAuth, Lobby, MatchRoom, validate, rate limit, static web
  Server.Tests/             # xUnit: test tích hợp với client WebSocket giả và Chat giả
Dockerfile                  # .NET publish linux-arm64 + chép bản build Web vào wwwroot
client/                     # Unity project
  Packages/manifest.json    # "com.worms.sim": "file:../../shared/com.worms.sim"
  Assets/Game/
    Net/        # kết nối, buffer nội suy, lập lịch Event
    Render/     # TerrainMesher, WormView, WeaponView, Vfx, CameraRig
    Anim/       # animation state machine của sâu
    Audio/      # map Event -> âm thanh
    Input/      # Keyboard, Touch -> Command
    UI/         # lobby, HUD, menu vũ khí, cài đặt
    Quality/    # chọn tier đồ họa, benchmark lần chạy đầu
    Sandbox/    # chạy sim offline để test hình ảnh
  Assets/Editor/Build.cs    # điểm vào build ở batchmode cho từng target
  Assets/Tests/             # EditMode, PlayMode
assets-src/                 # file nguồn CC0 và file tự tạo (.wav, script sinh âm thanh). Không dùng Git LFS (§3.16)
.github/workflows/
  ci.yml                    # PR và main: dotnet test + Unity EditMode
  build.yml                 # main: Web + 4 target native, image ghcr.io/quyenanh198/worms. Tag v*: GitHub Release
tools/
  gen_meta.py               # sinh file .meta với GUID cố định
  unity-check/              # compile-check code Unity không cần license
```

### 3.3 Bất biến
1. **Server authoritative.** Chỉ server chạy `World.Step()` có hiệu lực. Client **không bao giờ** tự sửa game state, chỉ gửi Command.
2. `com.worms.sim` không tham chiếu `UnityEngine` (compiler chặn), không dùng `DateTime`, `UnityEngine.Random` hay `System.Random` không có seed. Mọi thứ ngẫu nhiên đi qua `world.Rng`.
3. **Địa hình chỉ đổi qua carve op** (`{x, y, r}`, số nguyên) nằm trong một danh sách có thứ tự. Bitmap không bao giờ được gửi qua mạng.
4. Âm thanh và VFX **chỉ** được kích hoạt bởi Event.
5. Mọi hằng số gameplay nằm trong `Constants.cs`.
6. **Không dùng tính năng chỉ chạy được trên native** (§3.14). Tính năng nào chưa có trong danh sách cho phép thì phải được xác minh chạy trên Web trước khi dùng.
7. **Danh tính người chơi chỉ lấy từ Chat `/api/me`.** Không bao giờ tin user id hay tên do client gửi lên.
8. **Repo public nên chỉ chứa asset CC0 hoặc tự tạo.** Mỗi asset được ghi nguồn và license trong `assets-src/CREDITS.md`.

### 3.4 Luồng dữ liệu online
Trong từng lớp, dữ liệu vẫn chảy một chiều: renderer chỉ đọc, sim chỉ nhận Command. Toàn hệ thống là **một vòng khép kín qua mạng** cộng với 3 luồng phụ.

#### 3.4.1 Vòng chính
1. Client tạo `Command` có `seq` và gửi lên server qua WebSocket.
2. Server kiểm tra hợp lệ: đúng người đang có lượt, đúng phase, giá trị được clamp, không vượt rate limit 60 message/giây. Sau đó đưa vào hàng đợi.
3. Server chạy `World.Step()` 60 lần/giây. Mỗi bước sinh state mới và danh sách `Event[]`, mỗi event gắn số `tick`.
4. Server gửi **Snapshot 20 lần/giây** (sâu, đạn, gió, phase, đồng hồ) và **gửi Event ngay** khi phát sinh.
5. Client **render trễ 100 ms** để nội suy mượt giữa 2 snapshot.
6. Event được phát **đúng tick của nó** theo cùng độ trễ, để âm thanh khớp hình.

#### 3.4.2 Luồng phụ
- **Lobby:** đăng nhập (§3.15), tạo phòng, nhận mã phòng, vào bằng mã hoặc quick match, bấm sẵn sàng, bắt đầu trận. Link mời có dạng `chat.lazybutts.com/worms/?room=ABCD` để dán vào Chat.
- **Resync:** khi vào lại trận, client nhận `FullState` gồm seed, toàn bộ carve op và snapshot hiện tại, rồi dựng lại địa hình.
- **Thời gian:** đồng hồ lượt tính theo tick của server (`TurnEndsAtTick`). Client chỉ hiển thị.

#### 3.4.3 Vì sao server authoritative
Số thực dấu phẩy động (float) có thể cho kết quả khác nhau giữa IL2CPP (client) và .NET (server), và giữa các CPU khác nhau. Server authoritative không cần mọi máy tính ra kết quả giống hệt nhau, và chống được gian lận.

#### 3.4.4 Độ trễ
- **Ngắm và nạp lực chạy hoàn toàn trên client.** Chỉ khi bắn mới gửi một lệnh `Fire{angle, power, fuse, target}`. Góc ngắm được gửi thêm 10 lần/giây để đối thủ thấy tâm ngắm, nhưng chỉ để hiển thị.
- **Đi và nhảy** trễ bằng RTT (khoảng 50–150 ms). Nếu playtest thấy khó chịu, bật prediction cho con sâu đang điều khiển. Client đã có sẵn cùng code sim nên làm được. Đây là việc của P9.

### 3.5 Giao thức (`com.worms.protocol`)
Mỗi message có dạng `[PROTOCOL_VERSION:u16][MsgType:u8][payload]`, số nguyên little-endian. Hiện tại `PROTOCOL_VERSION = 3`.

| Hướng | Message | Nội dung |
|---|---|---|
| S→C | `Hello` | user id, tên hiển thị (lấy từ Chat) |
| S→C | `Error` | mã lỗi: `unauthorized`, `bad_version`, `rate_limited`, `room_not_found`, `room_full`, `room_busy`, `not_host`, `not_ready`, `bad_message` |
| S→C | `Lobby` | mã phòng (rỗng nghĩa là không ở trong phòng), phòng nhanh hay riêng, chủ phòng, trạng thái (Lobby, Playing, Finished), danh sách người chơi (tên, đội, sẵn sàng, còn kết nối) |
| S→C | `MatchStart` | seed, số sâu mỗi đội, đội của bạn, tên các đội, **toàn bộ carve op**, snapshot hiện tại. Gửi khi trận bắt đầu và khi vào lại trận |
| S→C | `Snapshot` | 20 lần/giây: tick, phase, đội và sâu đang có lượt, gió, đồng hồ, vũ khí, từng sâu (vị trí, vận tốc, HP, sát thương chờ trừ, trạng thái, hướng, góc ngắm), từng viên đạn |
| S→C | `Events` | các `SimEvent` gắn tick (Turn, Fire, Explode, Hit, Bounce, Land, Splash, Death, GameOver) |
| C→S | `CreateRoom`, `JoinRoom(code)`, `QuickMatch`, `SetReady(bool)`, `StartMatch`, `LeaveRoom`, `Rematch` | lobby |
| C→S | `Input` | kind (Move, Jump, Backflip, Aim, Select, Fire), dir, angle, power, fuse, weapon, target. **Đội do server gán theo người gửi**, không lấy từ client |

- Client dựng lại địa hình từ seed và carve op. Mỗi event `Explode` được khoét bằng đúng hàm `CarveOp.FromExplosion` như server, đúng lúc event đó tới hạn trên dòng thời gian render.
- Server giới hạn 40 message/giây (cho phép dồn tối đa 80), message tối đa 8 KB.

### 3.6 Mô phỏng vật lý (tự viết, trong `com.worms.sim`)

**Tự viết là đủ.** Worms Armageddon cũng dùng vật lý riêng trên địa hình bitmap, không dùng engine rigid-body. **Không dùng vật lý của Unity (PhysX) cho gameplay**, vì:
- server .NET không có PhysX,
- PhysX không va chạm được với mask địa hình bị khoét liên tục.

Mảnh vỡ, khói và vỏ đạn là particle trên client, không đồng bộ qua mạng.

#### 3.6.1 Đơn vị và hằng số khởi điểm (sẽ tinh chỉnh ở P1)
1 đơn vị (u) = 1 ô của mask. Bản đồ 2048×1024. Mô phỏng chạy 60 tick/giây, `dt = 1/60`. Khi hiển thị trong Unity, 1 u = 0.05 m.

| Hằng số | Giá trị | Ghi chú |
|---|---|---|
| `G` (trọng lực) | 600 u/s² | hướng xuống |
| `WORM_R` | 8 u | sâu cao khoảng 16 u |
| `WALK_SPEED` | 60 u/s | |
| `MAX_CLIMB` | 4 u / bước 1 u | |
| `JUMP` | v = (±150, −200) | cao khoảng 33 u |
| `BACKFLIP` | v = (∓40, −330) | cao khoảng 90 u |
| `V_SAFE` | 270 u/s | tương đương rơi khoảng 60 u. Sát thương = `ceil((v−V_SAFE)/8)` |
| `WIND_MAX` | ±100 u/s² | |
| `E_WORM`, `MU_WORM` | 0.3, 0.25 | hệ số nảy và ma sát của sâu |
| `KB` | 8 u/s mỗi 1 HP sát thương, thành phần hướng lên tối thiểu 0.6 | Tăng so với bản đầu (6 và 0.3) vì sâu bay quá thấp |

#### 3.6.2 Tích phân và va chạm
- Semi-implicit Euler: `v += (G + wind·windFactor)·dt; p += v·dt`.
- **Sub-step:** chia mỗi bước thành các bước nhỏ ≤ 1 u để không xuyên tường.
- **Pháp tuyến va chạm:** `n = normalize(Σ(p − cell))` với các ô đặc nằm trong bán kính.
- **Phản hồi va chạm:** `vn = (v·n)n`, `vt = v − vn`, vận tốc mới `v' = −e·vn + (1−μ)·vt`. Sau đó đẩy vật ra khỏi địa hình theo `n`.
- **Tiếp đất:** sâu đang nhảy hoặc rơi dừng ngay khi chạm mặt đủ phẳng (pháp tuyến hướng lên, `n.y < −0.6`). Sâu đang lăn dừng khi chạm mặt đủ phẳng với tốc độ < `LandSpeed` (40 u/s). Nếu kẹt trong khe hoặc trên dốc đứng mà vẫn chậm liên tục 45 tick thì cũng tính là đã đứng.

#### 3.6.3 Từng hành động
| Hành động | Cách tính |
|---|---|
| **Đi bộ** | Kinematic, không dùng lực. Dịch x theo `WALK_SPEED·dt`, rồi tìm mặt đất trong khoảng ±`MAX_CLIMB`: cao hơn thì bị chặn, hụt chân thì chuyển sang `airborne` |
| **Nhảy / lộn ngược** | Gán vận tốc theo `JUMP` hoặc `BACKFLIP`. **Không điều khiển được khi đang trên không** |
| **Rơi và tiếp đất** | Nếu `|vn| > V_SAFE` thì chịu sát thương rơi, phát event `Land`, và **mất lượt** nếu là sâu đang điều khiển |
| **Bắn đạn đạn đạo** | `v0 = power·maxSpeed·(cos a, −sin a)`. Đạn xuất hiện ở nòng súng, cách tâm sâu `WORM_R + 4`. Nổ khi chạm (`impact`) hoặc khi hết ngòi (`timer`) |
| **Hitscan** | Dò tia từng 1 u. Dừng tại sâu đầu tiên hoặc tại địa hình, gây vụ nổ nhỏ ở điểm trúng |
| **Cận chiến** | Tìm sâu trong hình quạt 90° (±45° quanh hướng ngắm), tầm 20 u tính từ mép thân. Gây sát thương và đặt vận tốc văng theo góc ngắm |
| **Bị trúng nổ** | Với sâu cách tâm `d < r`: `dmg = round(maxDamage·(1−d/r))`, `v += dir·KB·dmg`, trong đó `dir` được nâng thành phần hướng lên ít nhất 0.6. Sâu chuyển sang `tumbling`, nảy và trượt cho đến khi nghỉ thì chuyển sang `getup` |
| **Lăn lộn (hình ảnh)** | Client tự tính góc xoay: trên không thì `ω₀ = k·|v|·sign(vx)` và giảm dần; trên đất thì `ω = |vt|/WORM_R`. Không đồng bộ |
| **Lựu đạn** | Vật thể tròn `r=3`, `e=0.5`, `μ=0.1`, `windFactor=0`, ngòi `fuse·60` tick |

### 3.7 Lượt chơi, sát thương và cái chết
```
Lobby ─► TurnStart ─► Aiming ──bắn──► Retreat(3s) ─► Flying ─► Settling ─► EndOfTurn ─► CheckWin
          (random gió,  (45s)          (sâu chạy     (chờ đạn   (chờ mọi    (sâu HP≤0    │
           chọn sâu)     │              trong lúc     còn bay)   thứ nghỉ)   tự nổ)       ├─► TurnStart
                         │              đạn bay)                                           └─► GameOver
                         └── hết giờ / rơi đau / bị thương trong lượt mình ─► Settling
```
- Thời gian chạy lùi (Retreat) bắt đầu **ngay khi bắn**, lúc đạn vẫn đang bay, giống Worms thật. Nhờ vậy đặt Dynamite xong còn kịp chạy. Hết 3 giây mà đạn vẫn bay thì chuyển sang Flying.
- Shotgun có 2 phát: giữa hai phát vẫn ở Aiming và được ngắm lại. Uzi bắn loạt 10 viên tự động. Trong lúc tấn công chưa xong thì không đổi được vũ khí.
- **Sát thương hiển thị ngay** bằng số bay lên. **Thanh HP chỉ trừ ở EndOfTurn.**
- **Chết:** ở EndOfTurn, sâu HP ≤ 0 lần lượt tự nổ (r 20, dmg 10), có thể gây dây chuyền nên quay lại Settling. Để lại bia mộ, bia mộ cũng bị văng được.
- **Chết đuối** hoặc **rơi ra ngoài bản đồ:** chết ngay.
- **Mất kết nối:** lượt của người đó bị bỏ qua. Không vào lại trong 60 giây thì bị xử thua, các con sâu ở lại làm mục tiêu.

### 3.8 Vũ khí v1 (8 loại) ❓D6
Mỗi vũ khí là **một dòng dữ liệu** cộng với một trong 5 behavior dùng chung.

| Vũ khí | Behavior | Thông số | Cách cầm | Khi bắn / ném | Giật (hình ảnh) |
|---|---|---|---|---|---|
| Bazooka | ballistic, impact | gió ×1, r 50, dmg 50 | vác trên vai, thân trên xoay theo góc ngắm | khói ở nòng và đuôi tên lửa | lùi người 0.1 s, rung camera nhẹ |
| Grenade | ballistic, timer | gió ×0, nảy 0.5, ngòi 1–5 s, r 50, dmg 50 | **cầm quả lựu đạn, không cầm súng** | vung tay ném, sau đó tay không | không |
| Cluster Bomb | ballistic, timer | như Grenade, bung 5 mảnh (r 20, dmg 15) | cầm trên tay | ném | không |
| Shotgun | hitscan ×2 | r 10, dmg 25 | cầm 2 tay | lửa đầu nòng, lên đạn (pump) | giật mạnh, rung camera |
| Uzi | hitscan ×10 | lệch ±5°, dmg 5 mỗi viên | cầm 1 tay | rung tay, vỏ đạn văng ra | rung liên tục |
| Dynamite | placed, timer | ngòi 5 s, r 75, dmg 75 | cầm thỏi, ngòi đang cháy | cúi thả | không |
| Baseball Bat | melee | dmg 30, văng 700 u/s | cầm gậy | vung gậy | không |
| Air Strike | airstrike | 5 tên lửa cách 30 u, r 30, dmg 30 | cầm bộ đàm | bấm nút, máy bay bay qua | không |

- **Giật khi bắn chỉ là hình ảnh**, không đẩy sâu về mặt vật lý.
- **Đổi vũ khí:** menu dạng lưới (phím `Tab`, hoặc phím `1`–`8`). Sâu cất vũ khí cũ rồi rút vũ khí mới.
- **Số lượng mỗi trận, mỗi đội:** Bazooka, Lựu đạn, Shotgun không giới hạn. Bom chùm 2, Uzi 3, Gậy 2, Dynamite 1, Không kích 1. Hết thì nút bị mờ; đầu lượt nếu vũ khí đang chọn đã hết thì tự về Bazooka.

### 3.9 Bản đồ và đồ họa 3D (Unity URP)
- **Lớp gameplay:** mask 2D 2048×1024, sinh ngẫu nhiên từ `seed + theme` (phase sau có bản đồ vẽ tay từ PNG).
- **Khối đất đùn (extrude):**
  - Chia mask thành các chunk 64×64. Mỗi chunk dựng mesh bằng C# thường: marching squares cho mặt trước, cộng tường bên dày 40 u theo trục Z, cạnh vát, và dải mesh cỏ ở mép trên. Thuật toán nằm trong assembly C# thuần để test được bằng `dotnet test`.
  - Khi có vụ nổ, chỉ dựng lại các chunk bị giao với vụ nổ.
  - Shader Graph triplanar với các lớp đất, đá, cỏ theo theme, có normal map và AO lấy theo độ sâu vào trong khối đất.
  - Miệng hố có decal cháy xém (URP Decal, chế độ Screen Space).
- **Chiều sâu cảnh:**
  - Cảnh nền 3D (đồi, cây, nhà) ở `z < −50`, dùng ánh sáng bake và light probe. Model lấy từ bộ CC0 của Quaternius (Stylized Nature) và Kenney (Nature Kit). Texture PBR và HDRI lấy từ Poly Haven và ambientCG.
  - Skybox gradient cộng mây billboard.
  - Camera phối cảnh (FOV khoảng 35°, camera rig tự viết) nên tự có parallax.
  - Đạo cụ tiền cảnh, DOF ở tier High.
- **Nước:** Shader Graph với sóng dịch đỉnh (vertex), bọt theo độ sâu (depth fade), phản chiếu lấy từ reflection probe. Khúc xạ chỉ bật ở tier High.
- **Ánh sáng:** một đèn directional realtime chiếu sâu và địa hình, phần còn lại dùng ánh sáng bake.
- **Post-processing:**
  - Mọi tier: tonemapping ACES và color grading.
  - Từ Medium: thêm bloom và vignette.
  - High: thêm SSAO và DOF.
- **Camera:** bám sâu đang điều khiển hoặc viên đạn. Kéo để pan, cuộn hoặc pinch để zoom, nghiêng ±10°, rung camera theo sát thương.
- **Theme MVP:** Đồng cỏ và Bãi biển.

### 3.10 Nhân vật và animation
- **Model dựng bằng code (không cần họa sĩ, không tốn tiền):** thân sâu là một ống mesh chạy dọc theo một spline khoảng 8 đốt. Có mắt lồi to (hình cầu, đồng tử tự nhìn về hướng ngắm), miệng, và đuôi thon. Màu theo đội, tô bằng Shader Graph (toon ramp, rim light, subsurface giả).
- **Animation bằng code (procedural):** mỗi state là một hàm điều khiển các đốt của spline.
  - Trườn: sóng sin chạy dọc thân.
  - Nhảy: co giãn (squash and stretch).
  - Ngắm: phần đầu và thân trên uốn theo góc ngắm.
  - Lăn lộn: thân cuộn thành vòng tròn.
  - Chết: phồng dần rồi nổ.
  - Chuyển giữa các state bằng blend trọng số.
  - Không dùng Animator và file `.anim`, nên mọi thứ là code: review được, test được, chạy giống nhau trên web.
- **Tay cầm vũ khí:** sâu không có tay (giống Worms gốc). Vũ khí gắn vào socket ở phần thân trên, cạnh đầu, và xoay theo góc ngắm. Có 2 bàn tay nhỏ dạng hình cầu, xuất hiện khi cầm vũ khí.
- **Model vũ khí:** lấy từ bộ CC0 (Kenney Blaster Kit, Quaternius), hoặc ghép từ hình khối cơ bản.
- **State machine của nhân vật** được điều khiển bởi `worm.State` (từ snapshot) và các Event:

| State | Animation |
|---|---|
| idle | thở, thỉnh thoảng nhìn quanh |
| walk | trườn |
| jump / backflip / fall / land | nhảy, lộn ngược, rơi, tiếp đất |
| aim(weapon) | pose cầm vũ khí, blend theo góc ngắm |
| fire / throw / swing | bắn, ném, vung (theo vũ khí) |
| tumbling | cuộn tròn, xoay theo ω ở §3.6.3 |
| getup | đứng dậy, lắc đầu choáng |
| drowning | vùng vẫy rồi chìm |
| dying | phồng lên rồi nổ tung |
| dead | bia mộ |
| victory | nhảy múa |

- **Khi bị trúng:** số sát thương bay lên, sâu nháy đỏ 0.2 s (qua property của material), kêu đau, chuyển sang `tumbling`.
- **Khi chết:** particle nổ, bia mộ rơi xuống, câu thoại.

### 3.11 Âm thanh
- Dùng `AudioSource` và `AudioClip` cơ bản.
- **Âm lượng theo nhóm** (SFX, nhạc, thoại) được nhân trực tiếp trong code, **không dùng hiệu ứng AudioMixer**, để giữ đồng nhất với web (§3.14).
- Âm thanh chỉnh trái/phải theo vị trí x so với camera. Cách làm (`panStereo` hay `spatialBlend` 3D) sẽ chọn sau khi xác minh chạy trên Web ở P6.
- **Map Event → âm thanh:**
  - `Fire`: tiếng bắn riêng từng vũ khí
  - `Explode`: 3 cỡ nổ theo `r`
  - `Bounce`: âm lượng theo tốc độ va chạm
  - `Hit`: tiếng kêu đau
  - `Land`: tiếng "uỵch"
  - `Splash`: tiếng nước
  - `Death`: tiếng nổ nhỏ + câu thoại
  - `Turn`: chuông báo lượt
  - còn 5 giây: tiếng tích tắc
- Nhạc nền loop theo theme. Trên web và iOS, audio chỉ mở khóa sau lần chạm đầu tiên, nên lobby phải có một thao tác chạm trước khi vào trận.
- **Nguồn: tổng hợp bằng code** (`Core/SfxSynth.cs`). Mọi tiếng động (nổ 3 cỡ, phóng tên lửa, ném, shotgun, uzi, vung gậy, máy bay không kích, nảy, nhảy, tiếp đất, bọt nước, kêu đau, "bye bye", chuông lượt, tích tắc) và nhạc nền loop 16 giây đều được sinh ra lúc game khởi động, từ noise, bộ dao động, envelope và bộ lọc. Repo không có file âm thanh nào nên không có vấn đề license. Sau này có thể thay bằng file CC0 hoặc giọng TTS mà không phải đổi phần còn lại.
- Tiếng nhảy không có event riêng: client tự phát khi thấy sâu chuyển sang Airborne và đang đi lên.
- Nút "Âm thanh: bật/tắt" trên HUD, thanh chỉnh Nhạc và Hiệu ứng ở menu. Cả hai được lưu trong `PlayerPrefs`.

### 3.12 Input đa nền tảng
- **Desktop:** `←/→` đi · `Enter` nhảy · `Backspace` lộn ngược · `↑/↓` ngắm · giữ `Space` để nạp lực, thả để bắn · `Tab` hoặc chuột phải mở menu vũ khí · `1–5` chỉnh ngòi · click để chọn mục tiêu Air Strike.
- **Mobile (native và web):**
  - Góc trái dưới: nút `<` `>` để đi, `Nhảy`, `Lộn`.
  - **Kéo từ con sâu** để ngắm: hướng kéo là góc, độ dài kéo là lực, thả tay là bắn; kéo quá ngắn thì huỷ.
  - Vũ khí không cần nạp lực (shotgun, uzi, gậy, dynamite): nút `Bắn` bên phải. Không kích: chạm vào bản đồ để chọn mục tiêu.
  - Nút vũ khí bên phải. Một ngón ở chỗ trống để kéo camera, hai ngón để pinch zoom.
  - Logic nằm trong `Core/TouchInterpreter.cs` (engine-free, có test).
- **Mức đồ họa:** mặc định là Cao cho desktop, Vừa cho app mobile, Thấp cho trình duyệt mobile. Trận đầu tiên đo FPS trong 3 giây rồi hạ mức nếu máy yếu, và lưu lại lựa chọn. Người chơi đổi được ở menu (Tự động, Thấp, Vừa, Cao).
- Dùng `UnityEngine.Input` (Input Manager cũ, chạy giống nhau trên mọi target kể cả Web). `ProjectSetup` tự đặt Active Input Handling. Cả hai kiểu điều khiển đều chuyển thành cùng một kiểu `Command`.

### 3.13 Build, CI và deploy

#### 3.13.1 Target (GitHub-hosted runners + GameCI)
| Target | Runner | Backend | Output | Phát hành |
|---|---|---|---|---|
| Web | `ubuntu-latest` | IL2CPP → WebAssembly, WebGL2 | thư mục tĩnh, nén Brotli với decompression fallback (file `.unityweb`) | Đóng gói vào image server (§3.13.2) |
| Android | `ubuntu-latest` | IL2CPP, ARM64 | `.apk` | GitHub Release, và link tải tại `chat.lazybutts.com/worms/download`. Cài tay (sideload), không qua Play Store |
| Linux | `ubuntu-latest` | IL2CPP | x86_64 | GitHub Release |
| Windows | `windows-latest` | **IL2CPP** | `.exe` (zip) | GitHub Release. Chưa ký nên Windows SmartScreen sẽ cảnh báo, người dùng bấm "Run anyway" |
| macOS | `macos-latest` | **IL2CPP** | `.app` Universal (zip) | GitHub Release. Chưa ký nên người dùng phải chuột phải → Open lần đầu |
| iOS | — | — | — | Hoãn (D10) |
| Server image | `ubuntu-latest` | .NET 10, `dotnet publish -r linux-arm64` (cross-compile, không cần QEMU) | `ghcr.io/quyenanh198/worms:latest` | Deploy lên Mac mini |

- **License Unity Personal cho CI:** lưu file `.ulf` (lấy sau khi kích hoạt Unity Hub trên một máy bất kỳ), `UNITY_EMAIL` và `UNITY_PASSWORD` vào GitHub Secrets. Secret không bị lộ cho PR từ fork.
- **Cache:** thư mục `Library/` được cache bằng `actions/cache` theo từng target, để build lần sau nhanh hơn.
- **Workflow:**
  - `ci.yml` chạy mỗi PR và khi merge vào main: `dotnet test`, Unity EditMode (`game-ci/unity-test-runner`).
  - `build.yml` chạy khi merge vào main: build Web và 4 target native, rồi build Docker image (arm64 + amd64) và push lên ghcr. Khi có tag `v*` thì tạo GitHub Release.
  - **Chưa có secret Unity thì các job Unity được bỏ qua** (không báo lỗi), và image dùng trang tạm thay cho bản Web.

#### 3.13.2 Server trên Mac mini (OrbStack + `macmini-hub`)
Làm theo đúng mẫu của Gunny, vì Gunny cũng là game online có WebSocket và đăng nhập bằng Chat:

- **Image:**
  - Stage 1: .NET SDK publish cho linux-arm64.
  - Stage 2: `mcr.microsoft.com/dotnet/aspnet` (arm64). Chép thêm bản build Web vào `wwwroot/`.
  - Kestrel phục vụ bản Web (file `.unityweb` là byte thô, loader của Unity tự giải nén nên không cần header `Content-Encoding` và không phụ thuộc Cloudflare), phục vụ `/ws`, `/healthz`, và `/download`.
- **Các thay đổi trong repo `macmini-hub`** (Claude soạn PR riêng khi tới P8):
  - Thêm service `worms` vào `docker-compose.yml`:
    - image `ghcr.io/quyenanh198/worms:latest`, `restart: unless-stopped`, `mem_limit`
    - healthcheck `/healthz`
    - biến môi trường `CHAT_API_URL=http://chat:8082`, `BASE_PATH=/worms`, `ALLOWED_ORIGINS=https://chat.lazybutts.com`
  - **Không dùng sablier**, cùng lý do với Gunny: phiên WebSocket không gia hạn sablier, nên container có thể bị tắt giữa trận.
  - Trong `Caddyfile`, thêm vào block `@chat`:
    ```
    redir /worms /worms/
    handle_path /worms/* {
        reverse_proxy worms:8080
    }
    ```
    Bản web phải nằm cùng host với Chat thì mới dùng chung được cookie `lb_session`, vì cookie này không đặt `Domain`.
  - Deploy bằng `scripts/deploy.sh worms`. Lệnh này pull image `:latest` và giữ lại bản `:previous` để rollback. **Không dùng `--build`**, vì image cần bản build Unity Web do CI tạo ra.
- **Dev và production dùng chung một container.** Khi cần thử bản mới mà không ảnh hưởng người đang chơi, chạy thêm container `worms-dev` (image tag `:pr-N`) tại `/worms-dev/*`.
- **Rủi ro chấp nhận được:** mất điện hoặc mất mạng ở nhà thì game sập. Điều này giống mọi app khác trên hub.

#### 3.13.3 Ngân sách hiệu năng
| Thiết bị | Tier mặc định | FPS mục tiêu |
|---|---|---|
| Desktop native và desktop browser (GPU rời, hoặc Apple Silicon) | High | 60 |
| iPhone 13 trở lên, Android tầm trung 2022 trở lên (native) | Medium | 60 |
| Như trên, nhưng chạy trên mobile browser | Medium hoặc Low | ≥ 30, mục tiêu 60 |

- Dưới 200 draw call (dùng SRP Batcher và GPU instancing).
- Dựng lại mesh địa hình < 4 ms mỗi vụ nổ, **đo trên Web** (job chạy đơn luồng).
- Web: tải lần đầu < 30 MB sau nén Brotli. Bộ nhớ < 1 GB trên iOS Safari.
- Server: < 1 ms cho mỗi tick của mỗi phòng.

### 3.14 Web ngang hàng native (D8)

**Định nghĩa "ngang hàng":**
- Cùng gameplay, cùng nội dung, cùng art, cùng tính năng.
- **Chất lượng hình ảnh phụ thuộc thiết bị, không phụ thuộc nền tảng.** Chrome trên PC mạnh chạy tier High giống hệt bản native Windows. Một chiếc điện thoại chạy cùng tier dù dùng app hay trình duyệt, chỉ khác FPS.

**Cái giá phải trả:**
- Cả bản native cũng **không dùng tính năng chỉ có trên native**.
- Mobile browser vẫn chậm hơn app native trên cùng máy. Đây là giới hạn của nền tảng, không có cách khắc phục hoàn toàn.

| Được dùng | Cấm dùng (không chạy trên WebGL2) | Thay bằng |
|---|---|---|
| URP Forward, Shader Graph, SRP Batcher | Forward+, Deferred | Forward, tối đa 8 đèn mỗi object |
| Particle System (Shuriken) | VFX Graph (cần compute shader) | Shuriken |
| C# thường trên main thread | Thread C# tự tạo | Chia việc ra nhiều frame |
| `AudioSource` cơ bản | Hiệu ứng AudioMixer | Nhân âm lượng trong code |
| WebSocket | UDP, socket thô | WebSocket |
| URP Decal (Screen Space) | Decal kiểu DBuffer | Screen Space |
| WebGL2 | WebGPU (Unity 6 vẫn đang thử nghiệm) | Xem xét lại sau MVP |

**Tier đồ họa** (giống nhau trên mọi nền tảng):

| Tier | Render scale | MSAA | Shadow | Post | Particle | Nước |
|---|---|---|---|---|---|---|
| Low | 0.7 | tắt | bóng blob | tonemap, color grading | ×0.5 | sóng + bọt |
| Medium | 0.85 | 2× | 1 cascade | + bloom, vignette | ×1 | sóng + bọt |
| High | 1.0 | 4× | 2 cascade, soft | + SSAO, DOF | ×1.5 | + khúc xạ |

Lần chạy đầu tiên chạy benchmark 3 giây để chọn tier. Người chơi đổi được trong cài đặt.

#### 3.14.1 Thay thế trình duyệt trên điện thoại
| Cách | Hiệu năng | Chi phí | Kết luận |
|---|---|---|---|
| **App Android (APK cài tay)** | Native, tốt nhất | 0 | ✅ Người dùng Android dùng cách này |
| App iOS native | Native | 99 USD/năm (Apple Developer). Cài miễn phí bằng tài khoản Apple thường thì app hết hạn sau 7 ngày và chỉ cài được qua cáp | ⏸ Hoãn (D10) |
| PWA ("Thêm vào màn hình chính") | **Giống trình duyệt**: vẫn chạy bằng engine của Safari hoặc Chrome | 0 | Chỉ cải thiện trải nghiệm (toàn màn hình, có icon), không nhanh hơn. Vẫn làm, vì gần như không tốn công |
| Cloud streaming (Mac mini render rồi stream video) | Mượt trên máy yếu | 0, nhưng Mac mini chỉ gánh được vài luồng; trễ khi điều khiển; rất phức tạp | ❌ Không làm |
| App Clip (iOS) hoặc Instant App (Android) | Native | App Clip cũng cần Apple Developer; Unity không hỗ trợ chính thức | ❌ Không làm |

**Kết luận:**
- Android: dùng APK. Khi mở trang web trên Android, hiện banner "Cài app để chơi mượt hơn" với link tải `/worms/download`.
- iPhone: chơi trên web, mặc định tier Low hoặc Medium. **Vì vậy web trên mobile vẫn là đường vào chính cho iPhone**, và toàn bộ quy tắc ở §3.14 vẫn giữ nguyên.
- Khi có 99 USD/năm thì bật target iOS lên: code không phải đổi, chỉ thêm job build trên `macos-latest` và ký app.

### 3.15 Tài khoản (dùng chung với Chat, D3)
Làm theo cách Gunny, Garden và Farm đang dùng: game hỏi Chat "cookie `lb_session` này là ai". Không cần sửa code của Chat.

- **Web:** game nằm tại `chat.lazybutts.com/worms/`, cùng host với Chat, nên trình duyệt tự gửi cookie `lb_session` khi mở kết nối WebSocket tới `/worms/ws`.
- **Native (Android, desktop):**
  1. Màn hình đăng nhập gửi `POST https://chat.lazybutts.com/api/auth/login {username, password}`.
  2. Lấy giá trị `lb_session` từ header `Set-Cookie` và lưu lại. JWT này có hạn 30 ngày.
  3. Khi mở WebSocket, gửi kèm header `Cookie: lb_session=…` (NativeWebSocket hỗ trợ header tùy chỉnh trên native).
  4. Chưa có tài khoản thì mở trình duyệt tới trang đăng ký của Chat (Chat yêu cầu mã mời).
  5. Nhận 401 thì quay lại màn hình đăng nhập.
- **Server:**
  - Khi có kết nối WebSocket, server gọi `GET http://chat:8082/api/me` kèm cookie. Nhận 200 thì được `{id, username, display_name, avatar_at}`; nhận 401 thì đóng kết nối.
  - Kết quả được cache theo từng kết nối, không gọi Chat mỗi message.
  - Avatar lấy qua `/api/users/:id/avatar` (server làm proxy).
- **Chống CSRF qua WebSocket:** nếu request có header `Origin`, nó phải nằm trong `ALLOWED_ORIGINS`. Request không có `Origin` là client native, được phép.
- **Lưu token trên native:** MVP lưu trong `PlayerPrefs`. Đây là rủi ro: người khác có quyền vào máy có thể đọc được token. Sau này chuyển sang Keystore (Android) và Keychain (macOS). Người dùng đăng xuất thì xóa token.
- **Chat sập thì không đăng nhập được.** Trận đang chơi vẫn tiếp tục, vì danh tính đã được cache theo kết nối.
- **Chế độ khách khi phát triển:** nếu không đặt `CHAT_API_URL`, server cho vào bằng `?name=…` (id âm, không trùng id của Chat). Production luôn đặt `CHAT_API_URL=http://chat:8082`.

### 3.16 Ghi chú khi triển khai
Các quyết định đưa ra lúc làm P0, khác với bản kế hoạch trước:
- **Không dùng package từ Unity registry** (Input System, Cinemachine, Burst, Addressables). Container của Claude không tải được `packages.unity.com` nên không compile-check được code dùng các package đó. Các package cũng không cần cho phạm vi hiện tại. Chỉ dùng package đi kèm editor (URP, Test Framework).
- **Không dùng Git LFS.** Gói miễn phí của GitHub chỉ có 1 GB băng thông LFS mỗi tháng, CI checkout vài lần là hết. Asset phải được nén và giữ nhỏ.
- **Dùng IMGUI cho UI** ở các phase đầu (lobby, HUD). Không cần package, chạy trên mọi target. Có thể thay bằng UI đẹp hơn sau khi gameplay xong.
- **P0 và P1 dùng Built-in Render Pipeline**, chuyển sang URP ở P2 khi bắt đầu làm hình ảnh.

---

## 4. Implementation plan

Mỗi phase là **1 PR** (D7) và phải qua bước verify trước khi sang phase sau.

- **Việc Claude tự verify được** trong container: toàn bộ `dotnet test` (sim, protocol, server).
- **Việc CI verify:** test và build Unity (GameCI).
- **Việc cần bạn kiểm tra bằng mắt:** checklist hình ảnh, âm thanh, cảm giác chơi.

| Phase | Nội dung | Verify |
|---|---|---|
| **P0** Khung dự án | Repo layout §3.2, 2 package dùng chung, server có `/healthz` và WS `hello`, Unity project (scene tạo bằng code), `Build.cs`, 2 workflow, Dockerfile | `dotnet test` xanh · code Unity compile được (`tools/unity-check`) · image chạy được, `/healthz` trả `ok` · *Khi có secret:* `build.yml` ra đủ 5 target, bản Web mở được trên Chrome và Safari iOS, client nhận `hello` |
| **P1** Sim | `Rng`, `Terrain`, `Body`, `Worm`, Ballistic, `Explosion`, `Turn`, `World` | xUnit: cùng seed ra cùng mask · carve đúng bán kính · không xuyên tường ở `maxSpeed` · sâu đứng yên trên mặt phẳng · không leo dốc > `MAX_CLIMB` · rơi cao thì mất HP · trúng nổ thì văng lên rồi nghỉ · state machine lượt đúng §3.7 · dây chuyền chết dừng được |
| **P2** Render offline | Chuyển sang URP. Sandbox: sim chạy local, TerrainMesher, sâu tạm là capsule, camera rig, bazooka, 3 tier, shader địa hình và nước bản đầu, texture CC0 | EditMode test: dựng lại đúng các chunk bị giao · **đo trên Web**: dựng lại < 4 ms và FPS đạt §3.13.3 trên iPhone Safari và Chrome desktop · bạn xem screenshot của 3 tier |
| **P3** Online core và tài khoản | Server: ChatAuth, Lobby, MatchRoom, validate, rate limit, FullState, Snapshot, Event. Client: màn hình đăng nhập (native), Net, nội suy, lập lịch Event, UI lobby, link mời | xUnit tích hợp dùng Chat giả: cookie hợp lệ thì vào được, 401 thì bị đóng kết nối, `Origin` lạ thì bị từ chối · 2 client giả chơi hết 1 trận · Command sai lượt bị từ chối · vào lại trận thì dựng lại địa hình giống hệt · Thủ công: 1 web + 1 Android chơi 1v1 bằng tài khoản Chat thật, giả lập 150 ms trễ và 2% mất gói |
| **P4** Vũ khí | 8 vũ khí, 5 behavior, menu vũ khí, giới hạn số lượng, model vũ khí CC0 | Mỗi vũ khí có ít nhất 1 test xUnit (grenade không chịu gió, nổ đúng tick; bat văng đúng góc; cluster bung đúng 5 mảnh…) |
| **P5** Nhân vật và VFX | Sâu dựng bằng code (mesh spline, mắt, shader toon), animation procedural cho mọi state §3.10, socket vũ khí, VFX nổ, khói, số sát thương, decal, rung camera | EditMode test: mesh sinh đúng số đỉnh, blend giữa các state liên tục (không giật) · PlayMode test: nhận Event thì đổi state đúng · checklist nhìn trên web và native · FPS vẫn đạt |
| **P6** Âm thanh | SFX CC0 và tự sinh, giọng nói từ TTS-Studio, map Event → SFX, pan, nhạc nền, âm lượng theo nhóm, mở khóa audio, `CREDITS.md` | Checklist: mọi Event có tiếng · lệch giữa âm thanh và hình < 50 ms · có tiếng trên iOS Safari và Android Chrome · mỗi file có dòng nguồn trong `CREDITS.md` |
| **P7** Đa nền tảng | Điều khiển cảm ứng, benchmark chọn tier, PWA manifest, banner tải APK, trang `/download`, GitHub Release khi có tag | Chơi được trên: iPhone (Safari), Android (APK + Chrome), Windows, macOS, Linux · FPS theo §3.13.3 |
| **P8** Hardening và deploy | Reconnect 60 s, rate limit, log, `/healthz`. **PR vào `macmini-hub`**: service `worms`, route Caddy `/worms/*`. Deploy bằng `scripts/deploy.sh worms` | Rút mạng 10 giây rồi vào lại, trận tiếp tục · load test 100 phòng giả lập trong container với `mem_limit` · chạy được qua `https://chat.lazybutts.com/worms/` · rollback bằng `deploy.sh worms --rollback` |
| **P9** Bot và font tiếng Việt | **Bot AI** (`BotAi` trong sim, `BotDriver` trên server): chủ phòng riêng bấm "+ Thêm máy" (tối đa 4 đội) hoặc "Chơi với máy" ở menu. Bot thử hàng trăm phát trên bản sao địa hình bằng đúng `Physics.Step` (có gió) cho bazooka, lựu đạn, shotgun, gậy; chọn phát hại địch nhất mà không hại đội mình, rồi lệch nhẹ có chủ đích. Nghĩ trên thread pool, bắn theo nhịp người (~1,5 s nghĩ, 0,7 s ngắm). **Font Be Vietnam Pro** (OFL) cho mọi chữ IMGUI: font mặc định của Unity trên Web thiếu chữ có hai dấu (ắ, ầ, ờ, ủ) | Test sim: bắn trúng địch trên nền phẳng, tính gió ±80, quay mặt khi địch ở sau, tránh mục tiêu cạnh đồng đội, dùng gậy khi sát, cùng seed ra cùng quyết định · Test server: một người đấu máy — máy tự bắn và trả lượt, chỉ chủ phòng thêm máy, tối đa 4, bỏ máy thì giải phóng chỗ và tên |
| **P10+** Sau MVP | Prediction, lịch sử trận và xếp hạng (DB), thêm vũ khí, bản đồ vẽ tay, sudden death, iOS native (khi có ngân sách), WebGPU, thông báo mời qua Chat | Mỗi mục có tiêu chí riêng |

**MVP xong** khi P0–P8 hoàn tất: 4 người dùng tài khoản Chat, trên ít nhất 3 nền tảng (trong đó có 1 iPhone qua web và 1 Android qua APK), chơi trọn 1 trận qua `chat.lazybutts.com/worms/` mà không có lỗi và không lệch trạng thái.

---

## 5. Hand-off

### 5.1 Việc bạn cần làm (trước P0)
1. **Unity license cho CI:** cài Unity Hub trên Mac mini (hoặc máy bất kỳ), đăng nhập, kích hoạt license Personal. Sau đó vào repo Worms → Settings → Secrets → Actions, thêm 3 secret:
   - `UNITY_LICENSE`: nội dung file `/Library/Application Support/Unity/Unity_lic.ulf`
   - `UNITY_EMAIL`
   - `UNITY_PASSWORD`
2. **(Không bắt buộc) Keystore Android:** để APK mới cài đè lên bản cũ mà không phải gỡ app, tạo một keystore một lần:
   ```bash
   keytool -genkeypair -v -keystore worms.keystore -alias worms -keyalg RSA -keysize 2048 -validity 10000
   base64 -w0 worms.keystore   # macOS: base64 -i worms.keystore
   ```
   Thêm 4 secret: `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME` (`worms`), `ANDROID_KEYALIAS_PASS`. **Giữ file keystore cẩn thận**: mất file thì người dùng phải gỡ app cũ mới cài được bản mới.
3. **ghcr:** sau lần push image đầu tiên, vào package `worms` trên GitHub và đặt visibility là public, để Mac mini pull được không cần đăng nhập. Hub đang dùng cách này cho các app khác.
4. **(Không bắt buộc) Chạy Unity Editor trên Mac mini** để xem thử cảnh và tinh chỉnh bằng mắt khi PR ghi "cần bạn làm" (§5.4). Mac mini không còn làm máy build.
5. **Khi tới P8:** review và merge PR vào `macmini-hub`, rồi chạy `scripts/deploy.sh worms` trên Mac mini.

### 5.2 Lệnh
```bash
dotnet test shared/ server/                  # sim, protocol, server (chạy được trên Linux, macOS, CI)
dotnet run --project server/Server           # server local, http://localhost:8080 (web) + ws://localhost:8080/ws
CHAT_API_URL=http://localhost:8082 dotnet run --project server/Server   # chạy cùng Chat local
docker build -t worms:dev . && docker run -p 8080:8080 worms:dev        # thử image trên OrbStack
# Unity (local, không bắt buộc; CI chạy bằng GameCI):
Unity -batchmode -quit -projectPath client -runTests -testPlatform EditMode
Unity -batchmode -quit -projectPath client -executeMethod Build.Web     # hoặc Build.Android, Build.Windows, ...
```

### 5.3 Checklist thêm vũ khí
1. Thêm dòng vào `Weapons.cs` (chọn behavior có sẵn). Chỉ khi hành vi thật sự mới thì mới thêm file vào `Behaviors/`.
2. Viết test xUnit cho hành vi riêng của vũ khí đó.
3. Thêm model vũ khí (CC0 hoặc ghép từ hình khối) và khai báo socket trong `WeaponView`.
4. Thêm hàm pose và hàm động tác bắn hoặc ném vào animation procedural.
5. Thêm âm thanh và icon trong menu vũ khí. Ghi nguồn vào `CREDITS.md`.
6. Chạy bản build web để xác nhận không dùng tính năng bị cấm ở §3.14.

### 5.4 Làm việc khi không có Unity Editor (dành cho Claude)
- Scene tối thiểu: `Boot.unity` rỗng, do `ProjectSetup` tạo lúc build. `GameRoot` tự khởi động bằng `RuntimeInitializeOnLoadMethod` và dựng mọi thứ khác từ code.
- Trước khi push, compile-check bằng `tools/unity-check` (Game, GameWeb, Editor) và chạy `tools/gen_meta.py`.
- Prefab và ScriptableObject được sinh bằng script trong `Assets/Editor/Generators/` và chạy ở batchmode. Không sửa tay file YAML.
- Nhân vật và animation dựng bằng code (§3.10), nên không cần Editor để làm animation.
- Việc chỉ làm được trong Editor (chỉnh ánh sáng, màu sắc bằng mắt) sẽ được ghi trong PR là **"cần bạn làm"**, kèm hướng dẫn từng bước.

### 5.5 Checklist đổi giao thức
Sửa `com.worms.protocol` trước. Server và client phải cập nhật trong **cùng một PR**. Tăng `PROTOCOL_VERSION`, server từ chối client cũ. Người dùng Android phải cài APK mới.

### 5.6 Rủi ro
| Rủi ro | Giảm thiểu |
|---|---|
| **Không có ngân sách asset:** chất lượng hình ảnh không bằng game thương mại | Chọn phong cách stylized hợp với asset CC0. Nhân vật dựng bằng code. Dồn công vào ánh sáng, shader và post-processing, vì các thứ này là code |
| **Web ngang hàng:** iOS Safari hạn chế bộ nhớ và hiệu năng, mà iPhone chỉ vào được qua web | Có danh sách tính năng cấm (§3.14) và tier đồ họa. Đo trên iPhone Safari **ngay từ P2** |
| Claude không chạy được Unity Editor | GameCI trên CI, dựng scene bằng code (§5.4). Việc cần làm bằng mắt được ghi rõ trong từng PR |
| Build Unity trên GitHub-hosted runner chậm (mỗi target 15–40 phút) | Cache `Library/`. PR chỉ build Web, build đủ target khi merge vào main hoặc khi có tag |
| License Unity Personal trên CI hết hạn hoặc bị đổi điều khoản | Cập nhật lại secret. Code sim, protocol và server không phụ thuộc Unity |
| Brotli đi qua Cloudflare bị sai header | Đã tránh: dùng decompression fallback, server không gửi `Content-Encoding` |
| Token `lb_session` lưu trong `PlayerPrefs` trên native | Chấp nhận ở MVP; chuyển sang Keystore và Keychain ở P9 |
| Server phụ thuộc Chat và Mac mini ở nhà | Giống các app khác trên hub. Trận đang chơi không phụ thuộc Chat (danh tính đã cache) |
| App desktop và APK chưa ký nên hệ điều hành cảnh báo | Có hướng dẫn cài trên trang `/download` |
| Đi bộ bị trễ do RTT | Ngắm và nạp lực chạy local; prediction ở P9 |
| Quy mô | Làm tuần tự theo phase, mỗi phase đều test hoặc chơi được. Ước lượng thô: nhiều tháng cho 1 dev |
