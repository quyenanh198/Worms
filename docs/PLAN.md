# Worms Clone (Online) — Kế hoạch kỹ thuật

> Trạng thái: **bản nháp v3, chờ duyệt**. Chưa có dòng code nào.
> Các mục đánh dấu ❓ là quyết định còn mở. Mục đánh dấu ✅ là đã chốt.

**Thay đổi so với v2:**
- Engine chuyển từ Babylon.js/TypeScript sang **Unity 6 URP + C#**. Server chuyển từ Node/Colyseus sang **.NET**, dùng chung code mô phỏng C# với client.
- **Mac mini M4 Pro** làm máy build, CI (self-hosted runner) và server dev.
- **Web phải ngang hàng native**, xem định nghĩa và quy tắc ở §3.14.
- Bỏ Electron và Capacitor: Unity build native trực tiếp cho desktop và mobile.

---

## 1. Mục tiêu và phạm vi

### 1.1 MVP
- **Online:** 2–4 người, mỗi người chơi trên máy riêng, mỗi người điều khiển 1 đội 4 con sâu. Tạo phòng bằng mã phòng hoặc ghép trận nhanh (quick match).
- **Nền tảng:**
  - Web (desktop và mobile browser)
  - Windows, macOS, Linux
  - Android, iOS
  - Tất cả dùng chung một project Unity. **Web ngang hàng native** (§3.14).
- **Đồ họa 3D, gameplay 2.5D:** nhân vật, địa hình và hiệu ứng là 3D. Mọi chuyển động và va chạm diễn ra trên mặt phẳng X-Y (giống *Worms Revolution*).
- **Địa hình phá hủy được**, có gió, nước, lượt 45 giây, HP 100.
- **8 vũ khí** (§3.8). Có animation cầm, bắn, ném, trúng đạn, văng, lăn lộn, chết, chết đuối.
- **Âm thanh:** hiệu ứng (SFX) theo sự kiện, nhạc nền, chỉnh âm lượng.

### 1.2 Ngoài phạm vi MVP
Tài khoản và xếp hạng, bot AI, Ninja Rope, Jetpack, trình sửa bản đồ, chat voice, replay, client-side prediction (§3.4.4), console.

### 1.3 Quyết định
| # | Nội dung | Trạng thái |
|---|---|---|
| D1 | Gameplay 2.5D: đồ họa 3D, gameplay trên mặt phẳng 2D | ✅ |
| D2 | Engine: **Unity 6 LTS, URP, C#** | ✅ |
| D3 | Không có tài khoản trong MVP: chơi với tư cách khách (nickname) | ❓ khuyến nghị |
| D4 | Asset: **mua trên Unity Asset Store** (VFX, môi trường stylized, âm thanh) + CC0 làm placeholder. **Nhân vật sâu phải thuê làm riêng** vì hiếm có sẵn | ❓ cần có ngân sách |
| D6 | 8 vũ khí như §3.8 | ❓ khuyến nghị |
| D7 | Mỗi phase là 1 PR | ❓ khuyến nghị |
| D8 | Web ngang hàng native, theo định nghĩa ở §3.14 | ✅ |
| D9 | Mac mini dùng để build, chạy CI và làm server dev. Server production đặt trên VPS | ✅ |

**Pháp lý:** "Worms" và tên các vũ khí đặc trưng (Holy Hand Grenade, Super Sheep…) là tài sản của Team17. Nếu phát hành, phải đổi tên game và không dùng asset gốc.

---

## 2. Tech stack

### 2.1 Stack
| Lớp | Công nghệ | Vai trò |
|---|---|---|
| Engine client | **Unity 6 LTS** (bản LTS mới nhất tại thời điểm làm P0), **URP**, rendering path **Forward** | Forward+ không chạy được trên WebGL2, nên dùng Forward để giữ đồng nhất với web (§3.14) |
| Ngôn ngữ | **C#**, dùng ở cả client và server | Code mô phỏng viết một lần, chạy ở cả hai nơi |
| Code mô phỏng dùng chung | Local UPM package `com.worms.sim`, `com.worms.protocol`. Target `netstandard2.1`, C# 9 | Unity dùng qua `Packages/manifest.json` (`file:`). .NET dùng qua `.csproj`. Asmdef đặt `noEngineReferences: true` nên **compiler chặn** mọi tham chiếu tới `UnityEngine` |
| Game server | **.NET 10 (LTS)**, ASP.NET Core Kestrel, WebSocket | Nhẹ, chạy native trên Mac (arm64) và Linux VPS (x64) |
| Transport | **Chỉ WebSocket** cho mọi client | Trình duyệt không dùng được UDP. Game theo lượt nên TCP đủ tốt, và chỉ phải bảo trì một đường truyền |
| WebSocket phía client | NativeWebSocket (mã nguồn mở, chạy cả WebGL lẫn native) | — |
| Serialize | Codec nhị phân tự viết trong `com.worms.protocol` (BinaryWriter/Reader) | Không phụ thuộc thư viện, chạy an toàn với IL2CPP và WebGL |
| Render | Shader Graph, Particle System (Shuriken), URP Decal (chế độ Screen Space), Post-processing Volume, Cinemachine 3 | Tất cả đều chạy được trên WebGL2 (§3.14) |
| Tính toán nặng | Burst + Job System (dựng mesh địa hình) | Trên web Burst vẫn chạy, nhưng job chạy đơn luồng. Ngân sách hiệu năng tính theo trường hợp web |
| Tải asset | Addressables | Web tải phần tối thiểu trước, phần còn lại tải dần |
| Test | xUnit cho sim, protocol và server (chạy trên Linux CI, **Claude tự chạy được**). Unity Test Framework cho EditMode và PlayMode (chạy trên Mac runner) | — |
| CI/CD | GitHub Actions: runner Ubuntu cho `dotnet test`; **self-hosted runner trên Mac mini** cho mọi bản build Unity | §3.13 |
| Hạ tầng | Dev: server chạy trên Mac mini (tester vào qua Cloudflare Tunnel). Prod: VPS Linux + Docker + Caddy (TLS, `wss://`). Web client đặt trên Cloudflare Pages | — |
| Asset nguồn | Blender → `.fbx`, `.wav`. Lưu bằng Git LFS | — |

### 2.2 Vì sao chọn Unity (tóm tắt)
- Đồ họa đẹp và hiệu năng mobile đã được kiểm chứng nhiều.
- Build được mọi nền tảng từ Mac: iOS, macOS, Android, Web, Windows và Linux (Windows và Linux dùng scripting backend Mono, §3.13).
- Kho asset lớn, giảm rủi ro thiếu asset.
- Server .NET dùng chung code C# mô phỏng với client.
- **Đánh đổi:** Claude không chạy được Unity Editor trong container cloud. Mọi test và build Unity chạy trên Mac mini. Để giảm việc phải thao tác trong Editor, scene và prefab được dựng bằng code càng nhiều càng tốt (§5.4).

---

## 3. Architecture

### 3.1 Tổng quan hệ thống
```
 ┌──────── Unity Client (Web / Win / macOS / Linux / Android / iOS) ────────┐
 │ Input (phím, chuột, cảm ứng) ──► Command ─────────────────┐              │
 │                                                           │ WebSocket    │
 │ Render URP ◄── Interpolation ◄── Snapshot + Events ◄──────┼───┐          │
 │ Audio/VFX  ◄──────── Event scheduler ◄────────────────────┘   │          │
 └───────────────────────────────────────────────────────────────┼──────────┘
                                                                 │ wss://
 ┌──────────────── Game Server (.NET 10, Kestrel) ───────────────┼──────────┐
 │ Lobby / Matchmaker ──► MatchRoom (mỗi trận 1 room, 1 vòng lặp)│          │
 │   validate Command ──► Sim.Step() 60Hz ──► Snapshot 20Hz ─────┘          │
 │                        (com.worms.sim) └─► Events (gửi ngay)             │
 └──────────────────────────────────────────────────────────────────────────┘
  Dev: chạy trên Mac mini          Prod: VPS Linux (Docker)
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
  Server/                   # Program.cs, Lobby, MatchRoom, validate, rate limit
  Server.Tests/             # xUnit: test tích hợp với client WebSocket giả
client/                     # Unity project
  Packages/manifest.json    # "com.worms.sim": "file:../../shared/com.worms.sim"
  Assets/Game/
    Net/        # kết nối, buffer nội suy, lập lịch Event
    Render/     # TerrainMesher (Burst), WormView, WeaponView, Vfx, CameraRig
    Anim/       # animation state machine của sâu
    Audio/      # map Event -> âm thanh
    Input/      # Keyboard, Touch -> Command
    UI/         # lobby, HUD, menu vũ khí, cài đặt
    Quality/    # chọn tier đồ họa, benchmark lần chạy đầu
    Sandbox/    # chạy sim offline để test hình ảnh
  Assets/Editor/Build.cs    # điểm vào build ở batchmode cho từng target
  Assets/Tests/             # EditMode, PlayMode
assets-src/                 # .blend, .wav (Git LFS)
.github/workflows/ ci.yml (ubuntu) · build.yml (self-hosted macOS)
```

### 3.3 Bất biến
1. **Server authoritative.** Chỉ server chạy `World.Step()` có hiệu lực. Client **không bao giờ** tự sửa game state, chỉ gửi Command.
2. `com.worms.sim` không tham chiếu `UnityEngine` (compiler chặn), không dùng `DateTime`, `UnityEngine.Random` hay `System.Random` không có seed. Mọi thứ ngẫu nhiên đi qua `world.Rng`.
3. **Địa hình chỉ đổi qua carve op** (`{x, y, r}`, số nguyên) nằm trong một danh sách có thứ tự. Bitmap không bao giờ được gửi qua mạng.
4. Âm thanh và VFX **chỉ** được kích hoạt bởi Event.
5. Mọi hằng số gameplay nằm trong `Constants.cs`.
6. **Không dùng tính năng chỉ chạy được trên native** (§3.14). Tính năng nào chưa có trong danh sách cho phép thì phải được xác minh chạy trên Web trước khi dùng.

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
- **Lobby:** tạo phòng, nhận mã phòng, vào bằng mã hoặc quick match, bấm sẵn sàng, bắt đầu trận.
- **Resync:** khi vào lại trận, client nhận `FullState` gồm seed, toàn bộ carve op và snapshot hiện tại, rồi dựng lại địa hình.
- **Thời gian:** đồng hồ lượt tính theo tick của server (`TurnEndsAtTick`). Client chỉ hiển thị.

#### 3.4.3 Vì sao server authoritative
Số thực dấu phẩy động (float) có thể cho kết quả khác nhau giữa IL2CPP (iOS, Web) và Mono hay .NET. Server authoritative không cần mọi máy tính ra kết quả giống hệt nhau, và chống được gian lận.

#### 3.4.4 Độ trễ
- **Ngắm và nạp lực chạy hoàn toàn trên client.** Chỉ khi bắn mới gửi một lệnh `Fire{angle, power, fuse, target}`. Góc ngắm được gửi thêm 10 lần/giây để đối thủ thấy tâm ngắm, nhưng chỉ để hiển thị.
- **Đi và nhảy** trễ bằng RTT (khoảng 50–150 ms). Nếu playtest thấy khó chịu, bật prediction cho con sâu đang điều khiển. Client đã có sẵn cùng code sim nên làm được. Đây là việc của P9.

### 3.5 Giao thức (`com.worms.protocol`)
```csharp
// Client -> Server
enum CmdType : byte { Move, Jump, Backflip, Aim, Select, Fire, Ready, Chat }
struct Command { CmdType Type; uint Seq; sbyte Dir; float Angle; float Power;
                 byte Fuse; WeaponId Weapon; short TargetX, TargetY; string Text; }

// Server -> Client
enum EvType : byte { Fire, Explode, Hit, Bounce, Land, Splash, Death, Turn, GameOver }
struct GameEvent { uint Tick; EvType Type; int Worm; short X, Y, R; int Dmg;
                   float Ix, Iy; float Speed; DeathCause Cause; float Wind; int Winner; }

// Snapshot 20Hz: Tick, Phase, TurnEndsAtTick, Wind, ActiveWorm,
//   Worms[] {Id, X, Y, Vx, Vy, Hp, State, Facing, Aim, Weapon}, Projectiles[] {Id, Kind, X, Y}
// FullState (khi vào hoặc vào lại): MapSeed, Theme, TerrainOps[] {X, Y, R}, Teams, Snapshot
// Mỗi message có header: [PROTOCOL_VERSION:u16][MsgType:u8][payload]
```

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
| `KB` | 6 u/s mỗi 1 HP sát thương | |

#### 3.6.2 Tích phân và va chạm
- Semi-implicit Euler: `v += (G + wind·windFactor)·dt; p += v·dt`.
- **Sub-step:** chia mỗi bước thành các bước nhỏ ≤ 1 u để không xuyên tường.
- **Pháp tuyến va chạm:** `n = normalize(Σ(p − cell))` với các ô đặc nằm trong bán kính.
- **Phản hồi va chạm:** `vn = (v·n)n`, `vt = v − vn`, vận tốc mới `v' = −e·vn + (1−μ)·vt`. Sau đó đẩy vật ra khỏi địa hình theo `n`.
- **Nghỉ:** khi `|v| < 5 u/s` và đang chạm đất liên tục 20 tick.

#### 3.6.3 Từng hành động
| Hành động | Cách tính |
|---|---|
| **Đi bộ** | Kinematic, không dùng lực. Dịch x theo `WALK_SPEED·dt`, rồi tìm mặt đất trong khoảng ±`MAX_CLIMB`: cao hơn thì bị chặn, hụt chân thì chuyển sang `airborne` |
| **Nhảy / lộn ngược** | Gán vận tốc theo `JUMP` hoặc `BACKFLIP`. **Không điều khiển được khi đang trên không** |
| **Rơi và tiếp đất** | Nếu `|vn| > V_SAFE` thì chịu sát thương rơi, phát event `Land`, và **mất lượt** nếu là sâu đang điều khiển |
| **Bắn đạn đạn đạo** | `v0 = power·maxSpeed·(cos a, −sin a)`. Đạn xuất hiện ở nòng súng, cách tâm sâu `WORM_R + 4`. Nổ khi chạm (`impact`) hoặc khi hết ngòi (`timer`) |
| **Hitscan** | Dò tia từng 1 u. Dừng tại sâu đầu tiên hoặc tại địa hình, gây vụ nổ nhỏ ở điểm trúng |
| **Cận chiến** | Tìm sâu trong hình quạt 60°, bán kính 20 u. Gây sát thương và đặt vận tốc văng theo góc ngắm |
| **Bị trúng nổ** | Với sâu cách tâm `d < r`: `dmg = round(maxDamage·(1−d/r))`, `v += dir·KB·dmg`, trong đó `dir` được nâng thành phần hướng lên ít nhất 0.3. Sâu chuyển sang `tumbling`, nảy và trượt cho đến khi nghỉ thì chuyển sang `getup` |
| **Lăn lộn (hình ảnh)** | Client tự tính góc xoay: trên không thì `ω₀ = k·|v|·sign(vx)` và giảm dần; trên đất thì `ω = |vt|/WORM_R`. Không đồng bộ |
| **Lựu đạn** | Vật thể tròn `r=3`, `e=0.5`, `μ=0.1`, `windFactor=0`, ngòi `fuse·60` tick |

### 3.7 Lượt chơi, sát thương và cái chết
```
Lobby ─► TurnStart ─► Aiming ──fire──► Flying ─► Settling ─► Retreat(3s) ─► EndOfTurn ─► CheckWin
          (random gió,  (45s)            (đồng hồ   (chờ mọi                   (sâu HP≤0     │
           chọn sâu)     │                dừng)      thứ nghỉ)                  tự nổ)        ├─► TurnStart
                         └── hết giờ / rơi đau / bị thương trong lượt mình ─► Settling       └─► GameOver
```
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
- **Đổi vũ khí:** menu dạng lưới. Sâu cất vũ khí cũ rồi rút vũ khí mới. Có giới hạn số lượng mỗi trận.

### 3.9 Bản đồ và đồ họa 3D (Unity URP)
- **Lớp gameplay:** mask 2D 2048×1024, sinh ngẫu nhiên từ `seed + theme` (phase sau có bản đồ vẽ tay từ PNG).
- **Khối đất đùn (extrude):**
  - Chia mask thành các chunk 64×64. Mỗi chunk dựng mesh bằng **Burst job**: marching squares cho mặt trước, cộng tường bên dày 40 u theo trục Z, cạnh vát, và dải mesh cỏ ở mép trên. Ghi mesh bằng `Mesh.MeshDataArray`.
  - Khi có vụ nổ, chỉ dựng lại các chunk bị giao với vụ nổ.
  - Shader Graph triplanar với các lớp đất, đá, cỏ theo theme, có normal map và AO lấy theo độ sâu vào trong khối đất.
  - Miệng hố có decal cháy xém (URP Decal, chế độ Screen Space).
- **Chiều sâu cảnh:**
  - Cảnh nền 3D (đồi, cây, nhà) ở `z < −50`, dùng ánh sáng bake và light probe.
  - Skybox gradient cộng mây billboard.
  - Camera phối cảnh (FOV khoảng 35°, dùng Cinemachine) nên tự có parallax.
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
- **Model:** sâu dùng rig Generic, xuất `.fbx`. Có socket `hand_R`, `hand_L`, `shoulder`. **Aim offset** là blend tree 1D gồm 3 pose (−90°, 0°, +90°) theo góc ngắm, đặt ở layer thân trên với avatar mask.
- **Animator state machine** được điều khiển bởi `worm.State` (từ snapshot) và các Event:

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
- Nguồn: gói âm thanh trên Asset Store hoặc CC0. Giọng nói tự thu hoặc thuê.

### 3.12 Input đa nền tảng
- **Desktop:** `←/→` đi · `Enter` nhảy · `Backspace` lộn ngược · `↑/↓` ngắm · giữ `Space` để nạp lực, thả để bắn · `Tab` hoặc chuột phải mở menu vũ khí · `1–5` chỉnh ngòi · click để chọn mục tiêu Air Strike.
- **Mobile (native và web):** nút ◀ ▶ và nút nhảy bên trái. **Kéo từ con sâu** để ngắm (hướng là góc, độ dài là lực, thả là bắn). Nút vũ khí bên phải. Pinch để zoom, 2 ngón để pan.
- Dùng Unity Input System. Cả hai kiểu điều khiển đều chuyển thành cùng một kiểu `Command`.

### 3.13 Build, CI và deploy

#### 3.13.1 Target
| Target | Backend | Output | Ghi chú |
|---|---|---|---|
| iOS | IL2CPP | Xcode project, sau đó `xcodebuild archive` ra `.ipa` | Cần Apple Developer (99 USD/năm), phát hành thử qua TestFlight |
| Android | IL2CPP, ARM64 | `.aab` và `.apk` | Cần Google Play Console (25 USD), phát hành thử qua Internal testing |
| macOS | IL2CPP | `.app` (Apple Silicon và Intel) | Ký và notarize |
| Windows | **Mono** | `.exe` | Build Windows bằng IL2CPP bắt buộc chạy trên máy Windows. Mono chậm hơn nhưng đủ nhanh cho game này |
| Linux | **Mono** | x86_64 | Cùng lý do với Windows |
| Web | IL2CPP → WebAssembly, WebGL2 | thư mục tĩnh, nén Brotli | Trên Cloudflare Pages cần file `_headers` đặt `Content-Encoding: br` |
| Server | .NET 10 | Mac: `dotnet run` (arm64). Prod: Docker image linux-x64 | — |

#### 3.13.2 Mac mini M4 Pro
- **Self-hosted GitHub Actions runner**, nhãn `[self-hosted, macOS, ARM64, unity]`.
- Cài sẵn: Unity Hub, Unity 6 LTS kèm các module iOS, Android, Web, Windows (Mono), Linux (Mono), Mac (IL2CPP); Xcode; .NET 10 SDK. License Unity Personal kích hoạt trên máy.
- `build.yml` chạy khi có PR hoặc tag:
  1. `Unity -batchmode -runTests` (EditMode và PlayMode)
  2. `Unity -batchmode -executeMethod Build.All`
  3. Upload artifact
  4. Web: tự deploy bản preview
- **Server dev:** chạy như service launchd. Tester vào qua **Cloudflare Tunnel** (dùng được với WebSocket), không cần mở port router.
- **Không** dùng làm server production: IP nhà, tốc độ upload, và khi mất điện hoặc mạng thì server sập.

#### 3.13.3 Ngân sách hiệu năng
| Thiết bị | Tier mặc định | FPS mục tiêu |
|---|---|---|
| Desktop native và desktop browser (GPU rời, hoặc Apple Silicon) | High | 60 |
| iPhone 13 trở lên, Android tầm trung 2022 trở lên (native) | Medium | 60 |
| Như trên, nhưng chạy trên mobile browser | Medium hoặc Low | ≥ 30, mục tiêu 60 |

- Dưới 200 draw call (dùng SRP Batcher và GPU instancing).
- Dựng lại mesh địa hình < 4 ms mỗi vụ nổ, **đo trên Web** (job chạy đơn luồng).
- Web: tải lần đầu < 30 MB sau nén Brotli, phần còn lại tải qua Addressables. Bộ nhớ < 1 GB trên iOS Safari.
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
| Burst, Job (chạy đơn luồng trên web) | Thread C# tự tạo | Job, hoặc chia việc ra nhiều frame |
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

---

## 4. Implementation plan

Mỗi phase là **1 PR** (D7) và phải qua bước verify trước khi sang phase sau.

- **Việc Claude tự verify được** trong container: toàn bộ `dotnet test` (sim, protocol, server).
- **Việc chạy trên Mac mini:** test và build Unity, qua runner.
- **Việc cần bạn kiểm tra bằng mắt:** checklist hình ảnh, âm thanh, cảm giác chơi.

| Phase | Nội dung | Verify |
|---|---|---|
| **P0** Khung dự án | Repo layout §3.2, 2 package dùng chung, server trả lời `hello`, Unity project có 1 scene, `Build.cs`, 2 workflow | CI Ubuntu `dotnet test` xanh · runner Mac build ra đủ 6 target · bản web mở được trên Chrome, Safari macOS và Safari iOS · client (web và native) kết nối server trên Mac, nhận `hello` |
| **P1** Sim | `Rng`, `Terrain`, `Body`, `Worm`, Ballistic, `Explosion`, `Turn`, `World` | xUnit: cùng seed ra cùng mask · carve đúng bán kính · không xuyên tường ở `maxSpeed` · sâu đứng yên trên mặt phẳng · không leo dốc > `MAX_CLIMB` · rơi cao thì mất HP · trúng nổ thì văng lên rồi nghỉ · state machine lượt đúng §3.7 · dây chuyền chết dừng được |
| **P2** Render offline | Sandbox scene: sim chạy local, TerrainMesher (Burst), sâu tạm là capsule, Cinemachine, bazooka, 3 tier, shader địa hình và nước bản đầu | EditMode test: dựng lại đúng các chunk bị giao · **đo trên Web**: dựng lại < 4 ms và FPS đạt §3.13.3 trên iPhone Safari và Chrome desktop · bạn xem screenshot của 3 tier |
| **P3** Online core | Server: Lobby, MatchRoom, validate, rate limit, FullState, Snapshot, Event. Client: Net, nội suy, lập lịch Event, UI lobby | xUnit tích hợp: 2 client giả chơi hết 1 trận · Command sai lượt bị từ chối · vào lại trận thì dựng lại địa hình giống hệt · Thủ công: 1 web + 1 native chơi 1v1, giả lập 150 ms trễ và 2% mất gói bằng Network Link Conditioner trên Mac |
| **P4** Vũ khí | 8 vũ khí, 5 behavior, menu vũ khí, giới hạn số lượng | Mỗi vũ khí có ít nhất 1 test xUnit (grenade không chịu gió, nổ đúng tick; bat văng đúng góc; cluster bung đúng 5 mảnh…) |
| **P5** Nhân vật và VFX | Model sâu (placeholder → asset thật), Animator, aim offset, socket vũ khí, mọi state §3.10, VFX nổ, khói, số sát thương, decal, rung camera | PlayMode test: chuyển state đúng khi nhận Event · checklist nhìn trên web và native: mỗi state xuất hiện đúng lúc · FPS vẫn đạt |
| **P6** Âm thanh | Map Event → SFX, pan theo vị trí, nhạc nền, âm lượng theo nhóm, mở khóa audio | Checklist: mọi Event có tiếng · lệch giữa âm thanh và hình < 50 ms · có tiếng trên iOS Safari và Android Chrome |
| **P7** Đa nền tảng | Điều khiển cảm ứng, benchmark chọn tier, ký app, TestFlight, Play Internal testing, ký và notarize macOS | Bản build cài được và chơi được trên: iPhone (app + Safari), Android (app + Chrome), Windows, macOS, Linux · FPS theo §3.13.3 |
| **P8** Hardening và deploy | Reconnect 60 s, rate limit, log, health check, Docker, deploy lên VPS + Caddy, Cloudflare Pages cho web, Cloudflare Tunnel cho server dev | Rút mạng 10 giây rồi vào lại, trận tiếp tục · load test 100 phòng giả lập < 50% CPU của VPS · vào được qua `wss://` |
| **P9+** Sau MVP | Prediction, tài khoản và Postgres, xếp hạng, thêm vũ khí, bản đồ vẽ tay, sudden death, bot AI, WebGPU | Mỗi mục có tiêu chí riêng |

**MVP xong** khi P0–P8 hoàn tất: 4 người trên 4 nền tảng khác nhau (trong đó có ít nhất 1 người chơi trên web) chơi trọn 1 trận qua internet mà không có lỗi và không lệch trạng thái.

---

## 5. Hand-off

### 5.1 Việc bạn cần làm trên Mac mini (trước P0)
1. Cài Xcode (App Store), rồi chạy `xcode-select --install`.
2. Cài Unity Hub, Unity 6 LTS với các module: iOS, Android (kèm SDK, NDK, OpenJDK), Web, Windows Build Support (Mono), Linux Build Support (Mono). Đăng nhập và kích hoạt license Personal.
3. Cài .NET 10 SDK (`brew install --cask dotnet-sdk`) và Git LFS (`brew install git-lfs && git lfs install`).
4. GitHub repo → Settings → Actions → Runners → *New self-hosted runner* (macOS ARM64). Cài runner dạng service, thêm nhãn `unity`.
5. (Làm ở P7) Đăng ký tài khoản Apple Developer và Google Play Console.
6. (Làm ở P8) Tạo tài khoản Cloudflare và cài `cloudflared` cho Tunnel.

### 5.2 Lệnh
```bash
dotnet test shared/ server/                  # sim, protocol, server (chạy được cả trên Linux)
dotnet run --project server/Server           # server dev, ws://localhost:5080
# Trên Mac (runner cũng chạy các lệnh này):
Unity -batchmode -quit -projectPath client -runTests -testPlatform EditMode
Unity -batchmode -quit -projectPath client -executeMethod Build.All   # hoặc Build.Web, Build.iOS, ...
```

### 5.3 Checklist thêm vũ khí
1. Thêm dòng vào `Weapons.cs` (chọn behavior có sẵn). Chỉ khi hành vi thật sự mới thì mới thêm file vào `Behaviors/`.
2. Viết test xUnit cho hành vi riêng của vũ khí đó.
3. Thêm prefab model, khai báo socket và pose cầm trong `WeaponView`.
4. Thêm clip animation bắn hoặc ném vào Animator.
5. Thêm âm thanh và icon trong menu vũ khí.
6. Chạy bản build web để xác nhận không dùng tính năng bị cấm ở §3.14.

### 5.4 Làm việc khi không có Unity Editor (dành cho Claude)
- Scene tối thiểu: chỉ có `Boot.unity` chứa một `Bootstrap` object. Mọi thứ khác được dựng runtime từ code, prefab hoặc Addressables.
- Prefab và ScriptableObject được sinh bằng script trong `Assets/Editor/Generators/` và chạy ở batchmode. Không sửa tay file YAML.
- Việc chỉ làm được trong Editor (chỉnh ánh sáng, material, animation bằng mắt) sẽ được ghi trong PR là **"cần bạn làm"**, kèm hướng dẫn từng bước.

### 5.5 Checklist đổi giao thức
Sửa `com.worms.protocol` trước. Server và client phải cập nhật trong **cùng một PR**. Tăng `PROTOCOL_VERSION`, server từ chối client cũ.

### 5.6 Rủi ro
| Rủi ro | Giảm thiểu |
|---|---|
| **Asset**: Claude không tạo được model có rig và animation chất lượng | Mua asset trên Asset Store, thuê làm nhân vật sâu (D4). Code không phụ thuộc vào asset cụ thể |
| **Web ngang hàng**: iOS Safari hạn chế bộ nhớ và hiệu năng | Có danh sách tính năng cấm (§3.14) và tier đồ họa. Đo trên iPhone Safari **ngay từ P2** |
| Claude không chạy được Unity Editor | Runner trên Mac, dựng scene bằng code (§5.4). Việc cần làm bằng mắt được ghi rõ trong từng PR |
| Windows và Linux dùng Mono nên chậm hơn IL2CPP | Đủ nhanh cho game này. Nếu thiếu thì thêm một runner Windows sau |
| Thay đổi điều khoản license của Unity | Code sim, protocol và server không phụ thuộc Unity, nên có thể đổi engine client nếu cần |
| Đi bộ bị trễ do RTT | Ngắm và nạp lực chạy local; prediction ở P9 |
| Server dev tại nhà | Chỉ dùng cho dev và test, production đặt trên VPS |
| Quy mô | Làm tuần tự theo phase, mỗi phase đều test hoặc chơi được. Ước lượng thô: nhiều tháng cho 1 dev, chưa tính asset |
