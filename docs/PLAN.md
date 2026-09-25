# Worms Clone (Online) — Kế hoạch kỹ thuật

> Trạng thái: **bản nháp v2, chờ duyệt**. Chưa có dòng code nào.
> Các mục đánh dấu ❓ là quyết định cần chốt. Tài liệu đã chọn sẵn phương án khuyến nghị cho từng mục, nhưng bạn có thể đổi.

**Thay đổi so với v1:** chuyển từ hot-seat sang online (server authoritative). Đồ họa chuyển sang 3D, gameplay vẫn trên mặt phẳng 2D (kiểu 2.5D). Có bản build cho web, desktop và mobile. Vật lý được mô tả chi tiết cho từng hành động. Bổ sung 8 vũ khí, animation, âm thanh và giao thức mạng.

---

## 1. Mục tiêu và phạm vi

### 1.1 MVP
- **Online:** 2–4 người, mỗi người chơi trên máy riêng, mỗi người điều khiển 1 đội 4 con sâu. Tạo phòng bằng mã phòng hoặc ghép trận nhanh (quick match).
- **Nền tảng:** Web (trình duyệt), Desktop (Windows/macOS/Linux), Mobile (Android/iOS). Dùng chung một codebase client.
- **Đồ họa 3D, gameplay 2.5D:** nhân vật, địa hình và hiệu ứng là 3D. Mọi chuyển động và va chạm diễn ra trên mặt phẳng X-Y (giống *Worms Revolution*).
- **Địa hình phá hủy được**, có gió, nước, lượt 45 giây, HP 100.
- **8 vũ khí** (xem §3.8). Nhân vật cầm vũ khí thật, có animation bắn, ném, trúng đạn, văng, lăn lộn, chết, chết đuối.
- **Âm thanh:** hiệu ứng (SFX) theo sự kiện, nhạc nền, chỉnh âm lượng.

### 1.2 Ngoài phạm vi MVP (để các phase sau)
Tài khoản và xếp hạng, bot AI, Ninja Rope, Jetpack, trình sửa bản đồ, chat voice, replay, client-side prediction (chỉ làm nếu đo thấy trễ khó chịu, xem §3.4.4).

### 1.3 Quyết định cần chốt ❓
| # | Câu hỏi | Khuyến nghị | Phương án khác |
|---|---|---|---|
| D1 | Gameplay 2.5D hay 3D thật? | **2.5D**: đồ họa 3D, gameplay trên mặt phẳng 2D | 3D thật (kiểu *Worms 3D*): địa hình voxel, vật lý 3D, camera xoay tự do. Công sức gấp khoảng 3 lần, khó điều khiển trên mobile, chơi mạng phức tạp hơn nhiều |
| D2 | Engine client | **TypeScript + Babylon.js** | Godot 4 hoặc Unity (xem §2.2) |
| D3 | Tài khoản người dùng trong MVP? | **Không**: chơi với tư cách khách (nickname) | Đăng nhập và lưu lịch sử: cần thêm Postgres và auth |
| D4 | Nguồn asset 3D và âm thanh | **Asset CC0** (Kenney, Quaternius, Freesound CC0) làm placeholder, sau đó thuê artist | Tự làm trên Blender |
| D5 | Vỏ desktop | **Electron** (Chromium đi kèm nên WebGL đồng nhất trên mọi máy) | Tauri: file nhỏ hơn, nhưng WebGL trên WebKitGTK (Linux) yếu |
| D6 | Danh sách vũ khí v1 (§3.8) | 8 vũ khí như bảng | Thêm hoặc bớt |
| D7 | Quy trình code | Mỗi phase là 1 PR | Commit thẳng lên branch |

**Pháp lý:** "Worms" và tên các vũ khí đặc trưng (Holy Hand Grenade, Super Sheep…) là tài sản của Team17. Nếu phát hành, phải đổi tên game và không dùng asset gốc.

---

## 2. Tech stack

### 2.1 Stack khuyến nghị
| Lớp | Công nghệ | Vai trò |
|---|---|---|
| Ngôn ngữ | **TypeScript strict**, dùng ở cả client và server | Chạy **cùng một code mô phỏng** trên server (để quyết định kết quả) và client (để test và dự đoán sau này) |
| Monorepo | npm workspaces | Gồm `packages/sim`, `packages/protocol`, `apps/client`, `apps/server` |
| 3D client | **Babylon.js** (WebGL2, có thể dùng WebGPU) | Có sẵn loader glTF, blend animation, gắn vật vào xương (bone), particle, shadow, GUI, audio. Viết hoàn toàn bằng code, không cần editor nhị phân |
| Bundler client | **Vite** | **Chỉ để build và chạy dev server cho client.** Vite không phải game server |
| Game server | **Node.js + Colyseus** | Quản lý phòng, ghép trận, WebSocket, đồng bộ state có delta encoding (Schema), reconnect, vòng lặp mô phỏng |
| Asset | glTF 2.0 (`.glb`), texture KTX2, nén mesh meshopt, âm thanh `.ogg` và `.m4a` | Tải nhẹ trên mobile. `.m4a` dành cho Safari/iOS |
| Mobile | **Capacitor** (Android/iOS) | Bọc bản build web thành app native |
| Desktop | **Electron** (D5) | Bọc bản build web thành app desktop |
| Test | Vitest (sim, protocol, server), Playwright (2 client thật chơi với nhau) | — |
| CI | GitHub Actions: typecheck, test, build | — |
| Hạ tầng | Docker. Server chạy trên 1 VPS (Fly.io hoặc Hetzner) sau Caddy (TLS, `wss://`). Client tĩnh đặt trên CDN (Cloudflare Pages) | Một process đủ cho MVP. Khi cần scale thì thêm Redis presence của Colyseus |
| Cơ sở dữ liệu | **Không có ở MVP** (D3). Phase sau dùng Postgres | — |

### 2.2 Vì sao không chọn Godot hoặc Unity
- **Godot 4:** công cụ animation và scene tốt, hiệu năng mobile native tốt hơn. Nhưng bản xuất web 3D nặng, và bản C# không xuất được ra web. Server phải là Godot headless, dùng ngôn ngữ khác với phần web. Đây là phương án thay thế hợp lý nếu ưu tiên mobile native hơn web.
- **Unity:** mạnh trên mobile, nhưng bản WebGL nặng, có vấn đề giấy phép, và scene là file YAML khó review hay sinh bằng code.
- **Babylon.js + TS** thắng vì web là nền tảng hạng nhất (vào chơi bằng link), server dùng chung code mô phỏng, và mọi thứ đều là text nên dễ review và test.
- **Đánh đổi:** trên mobile, 3D chạy trong WebView chậm hơn native, nên phải giữ ngân sách hiệu năng chặt (§3.13).

---

## 3. Architecture

### 3.1 Tổng quan hệ thống
```
 ┌───────────── Client (web / Electron / Capacitor) ─────────────┐
 │ Input (phím, chuột, cảm ứng) ──► Command ──────────────┐       │
 │                                                        │ WS    │
 │ Render 3D ◄── Interpolation ◄── State patch + Events ◄─┼───┐   │
 │ Audio/VFX ◄──────── Event scheduler ◄──────────────────┘   │   │
 └────────────────────────────────────────────────────────────┼───┘
                                                              │ wss://
 ┌──────────────────── Game Server (Node + Colyseus) ─────────┼───┐
 │ Lobby / Matchmaker ──► MatchRoom (mỗi trận 1 room)         │   │
 │   validate Command ──► sim.step() 60Hz ──► Schema state ───┘   │
 │                                        └─► events (broadcast)  │
 └────────────────────────────────────────────────────────────────┘
```

### 3.2 Cấu trúc monorepo
```
packages/
  sim/        # MÔ PHỎNG THUẦN, dùng chung server và client. Không import DOM, Babylon hay Node
    rng.ts constants.ts vec.ts
    terrain.ts      # mask Uint8Array, generate(seed, theme), carveCircle(), isSolid()
    body.ts         # tích phân, va chạm hình tròn với mask, nảy, ma sát, trạng thái nghỉ
    worm.ts         # đi, nhảy, lộn ngược, rơi, sát thương rơi, chết đuối
    weapons.ts      # bảng dữ liệu vũ khí
    behaviors/      # ballistic.ts hitscan.ts melee.ts placed.ts airstrike.ts
    explosion.ts    # khoét địa hình, sát thương, đẩy văng
    turn.ts         # state machine của lượt
    game.ts         # World + step(commands) -> events[]
  protocol/   # kiểu Command, Event, Schema state, hằng số mạng
apps/
  server/     # Colyseus: LobbyRoom, MatchRoom, validate, rate limit
  client/     # Babylon.js + Vite
    net/        # kết nối, buffer nội suy, lập lịch sự kiện
    render/     # terrainMesher.ts, wormView.ts, weaponView.ts, vfx.ts, camera.ts
    anim/       # state machine animation của sâu
    audio/      # map Event -> âm thanh
    input/      # keyboard.ts, touch.ts -> Command
    ui/         # lobby, HUD, menu vũ khí, cài đặt
  desktop/    # Electron, load build của client
  mobile/     # Capacitor, dự án Android/iOS
assets/       # file nguồn .blend, .wav (dùng Git LFS)
```

### 3.3 Bất biến
1. **Server authoritative.** Chỉ server chạy `sim.step()` có hiệu lực. Client **không bao giờ** tự sửa game state, chỉ gửi Command.
2. `packages/sim` không import DOM, Babylon, Node API hay `Date.now()`. Mọi thứ ngẫu nhiên đi qua `world.rng` có seed.
3. **Địa hình chỉ đổi qua carve op** (`{x, y, r}`, số nguyên) nằm trong một danh sách có thứ tự. Bitmap không bao giờ được gửi qua mạng.
4. Âm thanh và VFX **chỉ** được kích hoạt bởi Event, không suy ra từ việc so sánh state.
5. Mọi hằng số gameplay nằm trong `sim/constants.ts`.

### 3.4 Luồng dữ liệu online (trả lời câu "một chiều có đủ không")
Một chiều **trong từng lớp** thì vẫn đúng: renderer chỉ đọc, sim chỉ nhận Command. Nhưng chơi online cần một **vòng khép kín qua mạng** và 3 luồng phụ.

#### 3.4.1 Vòng chính
1. Client đọc input, tạo `Command` có `seq`, gửi lên server.
2. Server kiểm tra hợp lệ: đúng người đang có lượt, đúng phase, giá trị được clamp, không vượt rate limit. Sau đó đưa vào hàng đợi.
3. Server gọi `sim.step()` 60 lần/giây. Kết quả là state mới và danh sách `Event[]`, mỗi event gắn số `tick`.
4. Colyseus gửi **patch state (delta) 20 lần/giây** và broadcast event.
5. Client đưa snapshot vào buffer và **render trễ 100 ms** để nội suy mượt giữa 2 snapshot.
6. Event được **phát đúng tick của nó** theo cùng độ trễ, để tiếng nổ khớp với hình tên lửa chạm đất.

#### 3.4.2 Luồng phụ
- **Lobby và ghép trận:** tạo phòng, nhận mã phòng, vào phòng bằng mã hoặc quick match, bấm sẵn sàng, bắt đầu trận.
- **Resync:** khi vào lại trận, client nhận full state gồm seed và toàn bộ carve op, rồi dựng lại địa hình.
- **Đồng bộ thời gian:** đồng hồ lượt đếm theo tick của server (`turnEndsAtTick`). Client chỉ hiển thị.

#### 3.4.3 Vì sao chọn server authoritative thay vì lockstep
Lockstep yêu cầu mọi máy tính ra kết quả số thực giống hệt nhau. Nhưng `Math.sin` và các hàm tương tự có thể khác nhau giữa V8 (Chrome, Android) và JavaScriptCore (Safari, iOS). Server authoritative thì miễn nhiễm với vấn đề này và cũng chống gian lận.

#### 3.4.4 Độ trễ
Game theo lượt nên chịu trễ khá tốt:
- **Ngắm và nạp lực chạy hoàn toàn trên client**, phản hồi tức thì. Chỉ khi bắn mới gửi một lệnh `fire {angle, power, fuse, target}`. Góc ngắm được gửi thêm 10 lần/giây để đối thủ thấy tâm ngắm, nhưng chỉ để hiển thị.
- **Đi và nhảy** sẽ trễ bằng RTT (khoảng 50–150 ms). Nếu playtest thấy khó chịu, bật client-side prediction cho con sâu đang điều khiển. Việc này khả thi vì client có sẵn cùng code `sim`. Đây là phase P9, không làm trước.

### 3.5 Giao thức (`packages/protocol`)
```ts
// Client -> Server
type Command =
  | { t: 'move'; seq: number; dir: -1 | 0 | 1 }          // chỉ gửi khi hướng thay đổi
  | { t: 'jump'; seq: number } | { t: 'backflip'; seq: number }
  | { t: 'aim'; seq: number; angle: number }               // 10 lần/giây, chỉ để hiển thị
  | { t: 'select'; seq: number; weapon: WeaponId }
  | { t: 'fire'; seq: number; angle: number; power: number; fuse?: 1|2|3|4|5; target?: {x:number;y:number} }
  | { t: 'ready' } | { t: 'chat'; text: string };

// Server -> Client: Event gắn tick, phát theo cùng độ trễ nội suy
type Event = { tick: number } & (
  | { e: 'fire'; worm: number; weapon: WeaponId }
  | { e: 'explode'; x: number; y: number; r: number }    // đồng thời được thêm vào terrainOps
  | { e: 'hit'; worm: number; dmg: number; ix: number; iy: number }  // ix, iy là hướng xung lực
  | { e: 'bounce'; id: number; speed: number } | { e: 'land'; worm: number; fallDmg: number }
  | { e: 'splash'; x: number } | { e: 'death'; worm: number; cause: 'hp' | 'water' | 'oob' }
  | { e: 'turn'; worm: number; wind: number } | { e: 'gameOver'; winner: number | null });

// Schema state (Colyseus): players, teams, worms {x,y,vx,vy,hp,state,facing,aim,weapon},
// projectiles, wind, phase, turnEndsAtTick, mapSeed, theme, terrainOps: ArraySchema<{x,y,r}>
```

### 3.6 Mô phỏng vật lý (trả lời câu "tự viết có đủ không")

**Có đủ.** Worms Armageddon cũng không dùng engine rigid-body mà dùng vật lý riêng trên địa hình bitmap. Những gì Worms cần đều làm được bằng mô hình "vật thể tròn + mask":
- bị văng khi trúng đạn,
- nảy khi va vào địa hình,
- lăn và trượt trên dốc rồi dừng lại,
- rơi, chết đuối, dây chuyền nổ.

**Không làm được** (và Worms cũng không cần): xếp chồng vật rắn, vật xoay va chạm chính xác. Mảnh vỡ và khói chỉ là particle trên client, không đồng bộ qua mạng.

#### 3.6.1 Đơn vị và hằng số khởi điểm (sẽ tinh chỉnh ở P1)
1 đơn vị = 1 ô của mask. Bản đồ 2048×1024. Mô phỏng chạy 60 tick/giây, `dt = 1/60`.

| Hằng số | Giá trị | Ghi chú |
|---|---|---|
| `G` (trọng lực) | 600 u/s² | hướng xuống |
| `WORM_R` | 8 u | sâu cao khoảng 16 u |
| `WALK_SPEED` | 60 u/s | Worms đi chậm |
| `MAX_CLIMB` | 4 u / bước 1 u | |
| `JUMP` | v = (±150, −200) | cao khoảng 33 u |
| `BACKFLIP` | v = (∓40, −330) | cao khoảng 90 u, bật ngược hướng đang nhìn |
| `V_SAFE` | 270 u/s | tương đương rơi khoảng 60 u. Sát thương = `ceil((v−V_SAFE)/8)` |
| `WIND_MAX` | ±100 u/s² | ngẫu nhiên mỗi lượt |
| `E_WORM`, `MU_WORM` | 0.3, 0.25 | hệ số nảy và ma sát của sâu |
| `KB` | 6 u/s mỗi 1 HP sát thương | dmg 50 cho vận tốc văng 300 u/s |

#### 3.6.2 Tích phân và va chạm (dùng chung cho sâu, đạn, lựu đạn)
- Semi-implicit Euler: `v += (G + wind·windFactor)·dt; p += v·dt`.
- **Sub-step:** chia mỗi bước thành `n = ceil(|v|·dt)` bước nhỏ, mỗi bước ≤ 1 u, để không xuyên tường.
- **Pháp tuyến va chạm:** `n = normalize(Σ (p − cell))` với các ô đặc nằm trong bán kính.
- **Phản hồi va chạm:** tách vận tốc thành thành phần pháp tuyến và tiếp tuyến: `vn = (v·n)n`, `vt = v − vn`. Vận tốc mới là `v' = −e·vn + (1−μ)·vt`. Sau đó đẩy vật ra khỏi địa hình theo `n`.
- **Nghỉ:** khi `|v| < 5 u/s` và đang chạm đất liên tục 20 tick thì vật chuyển sang trạng thái `resting`.

#### 3.6.3 Từng hành động
| Hành động | Cách tính |
|---|---|
| **Đi bộ** | Kinematic, không dùng lực. Mỗi tick dịch x theo `WALK_SPEED·dt`. Sau đó tìm mặt đất trong khoảng `[y−MAX_CLIMB, y+MAX_CLIMB]`: nếu cao hơn khoảng đó thì bị chặn; nếu hụt chân thì chuyển sang `airborne`, giữ nguyên vận tốc ngang |
| **Nhảy / lộn ngược** | Gán vận tốc theo `JUMP` hoặc `BACKFLIP`, chuyển sang `airborne`. **Không điều khiển được khi đang trên không** (giống Worms) |
| **Rơi và tiếp đất** | Khi chạm đất, nếu `|vn| > V_SAFE` thì chịu sát thương rơi, phát event `land`, và **mất lượt** nếu là sâu đang điều khiển |
| **Bắn đạn đạn đạo** | `v0 = power·maxSpeed·(cos a, −sin a)`. `power ∈ [0,1]` nạp trong 1 giây. Đạn xuất hiện tại nòng súng, cách tâm sâu `WORM_R + 4`. Nổ khi chạm địa hình hoặc sâu (`impact`), hoặc khi hết ngòi (`timer`) |
| **Hitscan** (súng) | Dò tia từng 1 u theo góc ngắm. Dừng tại sâu đầu tiên (kiểm tra va chạm hình tròn) hoặc tại địa hình. Gây vụ nổ nhỏ tại điểm trúng |
| **Cận chiến** | Tìm các sâu trong hình quạt 60°, bán kính 20 u theo hướng ngắm. Gây sát thương và đặt thẳng vận tốc văng theo góc ngắm |
| **Bị trúng nổ** | Với mỗi sâu cách tâm `d < r`: `dmg = round(maxDamage·(1−d/r))`. Cộng thêm vận tốc `v += dir·KB·dmg`, trong đó `dir = normalize(p−c)` và được nâng thành phần hướng lên ít nhất 0.3 để sâu luôn bị hất lên. Sâu chuyển sang `tumbling` (văng và lăn lộn), nảy theo `E_WORM`, trượt theo `MU_WORM`, cho đến khi nghỉ thì chuyển sang `getup`. Phát event `hit` |
| **Lăn lộn (hình ảnh)** | Không nằm trong sim. Client tự tính góc xoay: trên không thì `ω₀ = k·|v|·sign(vx)` và giảm dần; trên mặt đất thì `ω = |vt| / WORM_R` (lăn không trượt). Không cần đồng bộ |
| **Lựu đạn** | Vật thể tròn `r=3`, `e=0.5`, `μ=0.1`, `windFactor=0`, ngòi `fuse·60` tick. Nảy nhiều lần rồi nổ |

### 3.7 Lượt chơi, sát thương và cái chết
```
Lobby ─► TurnStart ─► Aiming ──fire──► Flying ─► Settling ─► Retreat(3s) ─► EndOfTurn ─► CheckWin
          (random gió,  (45s: đi,       (đồng hồ   (chờ mọi                   (sâu HP≤0     │
           chọn sâu      nhảy, ngắm)     dừng)      thứ nghỉ)                  tự nổ lần     ├─► TurnStart
           kế tiếp)      │                                                     lượt)         └─► GameOver
                         └── hết giờ / bị rơi đau / bị thương trong lượt mình ─► Settling
```
- **Sát thương hiển thị ngay** bằng số bay lên trên đầu sâu. **Thanh HP chỉ trừ ở EndOfTurn**, giống Worms.
- **Chết:** ở EndOfTurn, từng sâu có HP ≤ 0 lần lượt tự nổ (r = 20, dmg 10). Vụ nổ này có thể làm chết thêm sâu khác, nên quay lại Settling cho đến khi ổn định. Chỗ sâu chết để lại bia mộ, cũng là một vật thể tròn nên bị văng được.
- **Chết đuối:** rơi xuống dưới `waterLevel` thì chết ngay, có animation chìm và event `splash` + `death`.
- **Rơi ra ngoài bản đồ** (trái, phải): chết ngay.
- **Mất kết nối:** nếu người đang có lượt rớt mạng, lượt bị bỏ qua. Nếu không vào lại trong 60 giây, đội đó bị xử thua nhưng các con sâu vẫn ở lại làm mục tiêu.

### 3.8 Vũ khí v1 (8 loại) ❓D6
Mỗi vũ khí là **một dòng dữ liệu** cộng với một trong 5 **behavior** dùng chung. Không dùng class hierarchy.

| Vũ khí | Behavior | Thông số chính | Cách cầm | Khi bắn / ném | Giật (hình ảnh) |
|---|---|---|---|---|---|
| Bazooka | ballistic, impact | gió ×1, r 50, dmg 50 | vác trên vai, thân trên xoay theo góc ngắm | khói ở nòng và đuôi tên lửa | lùi người 0.1 s, rung camera nhẹ |
| Grenade | ballistic, timer | gió ×0, nảy 0.5, ngòi 1–5 s, r 50, dmg 50 | **cầm quả lựu đạn trên tay, không cầm súng** | vung tay ném, sau đó tay không | không |
| Cluster Bomb | ballistic, timer | như Grenade, khi nổ bung 5 mảnh (r 20, dmg 15) | cầm trên tay | ném | không |
| Shotgun | hitscan ×2 | r 10, dmg 25, được ngắm lại giữa 2 phát | cầm 2 tay | lửa đầu nòng, lên đạn (pump) | giật mạnh, rung camera |
| Uzi | hitscan ×10 | lệch ±5°, dmg 5 mỗi viên, không khoét đất | cầm 1 tay | rung tay, vỏ đạn văng ra | rung liên tục |
| Dynamite | placed, timer | thả dưới chân, ngòi 5 s, r 75, dmg 75 | cầm thỏi có ngòi đang cháy (tia lửa) | cúi thả | không |
| Baseball Bat | melee | dmg 30, văng 700 u/s theo góc ngắm | cầm gậy | vung gậy | không |
| Air Strike | airstrike | chọn điểm, 5 tên lửa cách 30 u, r 30, dmg 30 | cầm bộ đàm | bấm nút, máy bay bay qua | không |

- **Giật khi bắn chỉ là hình ảnh:** animation, rung camera và particle. Không đẩy sâu về mặt vật lý, giống Worms. Nếu sau này muốn giật thật thì thêm trường `recoil` vào dữ liệu vũ khí.
- **Đổi vũ khí:** mở menu dạng lưới, sâu cất vũ khí cũ rồi rút vũ khí mới. Vũ khí có số lượng giới hạn mỗi trận (ví dụ Air Strike 1, Dynamite 1, các loại khác ∞ hoặc 3).
- **Thêm vũ khí sau:** Homing Missile, Mine, Banana Bomb, Sheep, Ninja Rope, Girder, Teleport. Phải đổi tên nếu là tên đặc trưng của Team17.

### 3.9 Bản đồ và cách thể hiện 3D
- **Lớp gameplay:** mask 2D 2048×1024. MVP sinh bản đồ ngẫu nhiên từ `seed + theme`: đường chân trời từ nhiều sóng sin, có đảo nổi và hang. Phase sau hỗ trợ bản đồ vẽ tay từ ảnh PNG đen trắng.
- **Lớp hiển thị (3D):**
  - **Khối đất đùn (extrude):** chia mask thành các chunk 64×64. Mỗi chunk dùng marching squares để sinh mặt trước (tam giác) và **tường bên dày 40 u theo trục Z** dọc theo đường biên. Mép trên có vát cạnh và dải cỏ. Shader triplanar dùng texture đất, đá và cỏ theo theme.
  - **Khi có vụ nổ:** chỉ tạo lại mesh cho các chunk bị giao với vụ nổ (mục tiêu < 4 ms). Thêm decal cháy xém quanh miệng hố.
  - **Chiều sâu cảnh:** mặt phẳng chơi nằm ở `z=0`. Phía sau là các lớp cảnh 3D (đồi, cây, nhà) ở `z < −50`, và skybox. Camera phối cảnh (FOV khoảng 35°) nên các lớp này tự tạo hiệu ứng parallax. Phía trước có vài đạo cụ trang trí không va chạm, làm mờ theo chiều sâu (DOF) trên desktop.
  - **Nước:** mặt nước ở đáy dùng shader có sóng, phản chiếu và bọt. Chế độ nước dâng (sudden death) để phase sau.
  - **Ánh sáng:** một đèn directional. Desktop có shadow map; mobile dùng bóng blob đơn giản.
  - **Camera:** mặc định bám sâu đang điều khiển hoặc viên đạn. Người chơi có thể kéo để pan, cuộn hoặc pinch để zoom, nghiêng tối đa ±10° cho thấy chiều sâu. Góc nhìn không xoay tự do vì gameplay là 2.5D.
- **Theme MVP:** 2 theme (Đồng cỏ, Bãi biển). Mỗi theme gồm bộ texture, bộ cảnh nền và bảng màu.

### 3.10 Nhân vật và animation
- **Model:** sâu glTF có rig. Có socket `hand_R`, `hand_L`, `shoulder` để gắn vũ khí. Có aim offset gồm 3 pose (góc −90°, 0°, +90°) được blend theo góc ngắm.
- **Animation state machine (client)** được suy ra từ `worm.state` (do server gửi) và các Event:

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
| dying | phồng lên rồi nổ tung (ở EndOfTurn) |
| dead | bia mộ |
| victory | nhảy múa |

- **Khi bị trúng:** số sát thương bay lên, sâu nháy đỏ 0.2 s, kêu đau, chuyển sang `tumbling`. Camera rung theo độ lớn sát thương.
- **Khi chết:** xem §3.7. Có particle nổ, bia mộ rơi xuống, và câu thoại "bye bye".

### 3.11 Âm thanh
- Dùng audio của Babylon.js (Web Audio API). Âm thanh được **chỉnh trái/phải (pan) theo vị trí x so với camera** và nhỏ dần theo khoảng cách.
- **Map Event → âm thanh:**
  - `fire`: tiếng bắn riêng từng vũ khí
  - `explode`: 3 cỡ nổ theo `r`
  - `bounce`: âm lượng theo `speed`
  - `hit`: tiếng kêu đau
  - `land`: tiếng "uỵch"
  - `splash`: tiếng nước
  - `death`: tiếng nổ nhỏ + câu thoại
  - `turn`: chuông báo lượt
  - còn 5 giây: tiếng tích tắc
- Nhạc nền loop theo theme. Có thanh chỉnh âm lượng riêng cho SFX, nhạc và thoại.
- Trên iOS/Android, audio chỉ mở khóa sau lần chạm đầu tiên của người dùng, nên màn hình lobby phải có một thao tác chạm trước khi vào trận.
- Nguồn âm thanh: CC0 (Kenney, Freesound CC0) cho MVP. Giọng nói tự thu hoặc thuê.

### 3.12 Input đa nền tảng
- **Desktop:** `←/→` đi · `Enter` nhảy · `Backspace` lộn ngược · `↑/↓` ngắm · giữ `Space` để nạp lực, thả để bắn · `Tab` hoặc chuột phải mở menu vũ khí · `1–5` chỉnh ngòi · click chuột để chọn mục tiêu Air Strike.
- **Mobile:** nút ◀ ▶ và nút nhảy ở góc trái. **Kéo từ con sâu** để ngắm: hướng kéo là góc, độ dài kéo là lực, thả tay là bắn. Nút vũ khí ở góc phải. Pinch để zoom, 2 ngón để pan.
- Cả hai đều chuyển thành cùng một kiểu `Command` (§3.5).

### 3.13 Build, deploy và ngân sách hiệu năng
| Target | Cách build | Ghi chú |
|---|---|---|
| Web | `vite build` ra file tĩnh, đặt trên CDN | tải lần đầu < 15 MB (KTX2, meshopt) |
| Desktop | Electron load bản build web, đóng gói bằng `electron-builder` (Windows `.exe`, macOS `.dmg`, Linux `.AppImage`) | cần chứng chỉ ký code để phát hành |
| Mobile | Capacitor sync bản build web, build bằng Android Studio (`.aab`) và Xcode (`.ipa`) | **iOS bắt buộc build trên máy Mac** |
| Server | Docker image, Caddy lo TLS | 1 VPS 2 vCPU chịu khoảng 50–100 phòng (sẽ đo lại ở P8) |

**Ngân sách hiệu năng:**
- Desktop 60 fps. Mobile tầm trung ≥ 30 fps, mục tiêu 60 fps.
- Dưới 150 draw call.
- Tạo lại mesh địa hình < 4 ms mỗi vụ nổ.
- Server < 1 ms cho mỗi tick của mỗi phòng.

---

## 4. Implementation plan

Mỗi phase là **1 PR** (D7) và phải qua bước verify trước khi sang phase sau.

| Phase | Nội dung | Verify |
|---|---|---|
| **P0** Khung dự án | Monorepo với `sim`, `protocol`, `client` (Babylon + Vite), `server` (Colyseus). CI chạy typecheck, test và build | `npm run build` pass toàn bộ · CI xanh · client kết nối server và nhận được tin nhắn `hello` |
| **P1** Sim offline | `rng`, `terrain`, `body`, `worm`, behavior ballistic, `explosion`, `turn`. Chạy headless | Vitest: cùng seed ra cùng mask · carve đúng bán kính · không xuyên tường ở `maxSpeed` · sâu đứng yên trên mặt phẳng · không leo dốc > `MAX_CLIMB` · rơi cao thì mất HP · trúng nổ thì văng lên và cuối cùng nghỉ · state machine lượt đúng §3.7 · dây chuyền chết dừng được |
| **P2** Render 3D offline | Sandbox chạy `sim` ngay trên client (không cần mạng): terrain mesher theo chunk, sâu tạm là hình capsule, camera, bazooka | Test mesher: số chunk tạo lại đúng bằng số chunk giao vụ nổ · đo < 4 ms trên laptop · mắt thường: khoét lỗ thấy tường 3D |
| **P3** Online core | `MatchRoom` chạy sim trên server, Schema state, Command, validate, buffer nội suy, lập lịch Event, lobby (mã phòng, quick match) | Playwright: 2 trình duyệt chơi hết 1 trận 1v1 · giả lập 150 ms trễ và 2% mất gói vẫn chơi được · gửi Command sai lượt thì bị từ chối (test server) |
| **P4** Vũ khí | Đủ 8 vũ khí, 5 behavior, menu vũ khí, giới hạn số lượng | Mỗi vũ khí có ít nhất 1 test sim (ví dụ grenade không chịu gió, nổ đúng tick; bat văng đúng góc; cluster bung đúng 5 mảnh) |
| **P5** Nhân vật và VFX | Sâu glTF có rig, animation state machine, gắn vũ khí vào socket, aim offset, tumbling, getup, dying, drowning, bia mộ, VFX nổ, khói, số sát thương, rung camera | Checklist nhìn: mỗi state trong §3.10 xuất hiện đúng lúc trong 1 trận thật · fps vẫn trong ngân sách |
| **P6** Âm thanh | Map Event → SFX có pan, nhạc nền, cài đặt âm lượng, mở khóa audio trên mobile | Checklist: mọi Event ở §3.11 có tiếng · âm thanh khớp hình (lệch < 50 ms) · iOS Safari có tiếng |
| **P7** Đa nền tảng | Điều khiển cảm ứng, Capacitor (Android, iOS), Electron, tối ưu hiệu năng mobile | Bản build chạy được trên 1 máy Android tầm trung, 1 iPhone, Windows và macOS · đạt fps theo ngân sách |
| **P8** Hardening và deploy | Reconnect 60 s, xử lý rớt mạng, rate limit, Docker, Caddy, log, health check, deploy server và CDN | Test: rút mạng 10 giây rồi vào lại, trận tiếp tục bình thường · load test 50 phòng giả lập < 50% CPU · truy cập được qua `wss://` |
| **P9+** Sau MVP | Client-side prediction, tài khoản và Postgres, xếp hạng, thêm vũ khí, bản đồ vẽ tay, sudden death, bot AI | Mỗi mục có tiêu chí riêng khi lập kế hoạch |

**MVP xong** khi P0–P8 hoàn tất: 4 người trên 4 nền tảng khác nhau chơi trọn 1 trận qua internet mà không có lỗi và không lệch trạng thái giữa các máy.

---

## 5. Hand-off

### 5.1 Lệnh
```bash
npm install
npm run dev            # chạy song song server (ws://localhost:2567) và client (http://localhost:5173)
npm test               # vitest cho mọi workspace
npm run build          # typecheck và build mọi workspace
npm run e2e            # playwright, 2 client
npm run desktop        # electron dev
npm run mobile:android # cap sync và mở Android Studio
```

### 5.2 Checklist thêm vũ khí
1. Thêm dòng vào `sim/weapons.ts` (chọn behavior có sẵn). Chỉ khi hành vi thật sự mới thì mới thêm file vào `sim/behaviors/`.
2. Thêm test trong `sim` cho hành vi riêng của vũ khí đó.
3. Thêm model `.glb`, khai báo socket và pose cầm trong `client/render/weaponView.ts`.
4. Thêm animation bắn hoặc ném vào `client/anim`.
5. Thêm âm thanh vào `client/audio`.
6. Thêm icon vào menu vũ khí.

### 5.3 Checklist đổi giao thức
Sửa `packages/protocol` trước. Server và client phải được cập nhật trong **cùng một PR**. Tăng `PROTOCOL_VERSION` để server từ chối client cũ.

### 5.4 Rủi ro
| Rủi ro | Giảm thiểu |
|---|---|
| **Asset 3D, animation, âm thanh**: Claude viết được code nhưng **không tạo được model có rig và animation chất lượng** | Dùng CC0 placeholder (D4), code không phụ thuộc vào asset cụ thể. Cần artist trước khi phát hành |
| Hiệu năng 3D trong WebView trên mobile | Ngân sách §3.13, LOD, tắt shadow và DOF trên mobile, đo sớm ngay từ P2 |
| Đi bộ bị trễ do RTT | Ngắm và nạp lực chạy local; prediction ở P9 |
| Tạo lại mesh địa hình gây giật hình | Chia chunk, chỉ tạo lại chunk bị ảnh hưởng, có thể đẩy sang Web Worker |
| Âm thanh trên iOS | Mở khóa bằng thao tác chạm ở lobby, file `.m4a` |
| Quy mô dự án | Làm tuần tự theo phase, mỗi phase phải chơi hoặc test được. Ước lượng thô: nhiều tháng cho 1 dev, chưa tính asset |
