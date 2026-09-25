# Worms Clone — Kế hoạch kỹ thuật

> Trạng thái: **bản nháp, chờ duyệt**. Chưa có dòng code nào. Mọi mục đánh dấu ❓ là giả định cần xác nhận trước khi bắt đầu M0.

## 1. Mục tiêu và phạm vi

### 1.1 Mục tiêu (MVP)
Game bắn pháo theo lượt kiểu *Worms*, chạy trên trình duyệt:

- 2 đội, mỗi đội 2–4 con sâu, chơi luân phiên trên **cùng một máy** (hot-seat).
- Địa hình 2D sinh ngẫu nhiên, **phá hủy được** tới từng pixel.
- Sâu di chuyển được: đi bộ trên dốc, nhảy, rơi (có sát thương khi rơi).
- Vũ khí: **Bazooka** (chịu ảnh hưởng của gió) và **Grenade** (nảy, nổ theo ngòi 1–5s).
- Có gió, thời gian mỗi lượt, HP, chết đuối khi rơi xuống nước, màn hình thắng/thua.
- Camera bám theo sâu hoặc đạn, HUD hiện HP, gió, đồng hồ và vũ khí.

### 1.2 Ngoài phạm vi MVP (để sau)
Chơi online, bot AI, Ninja Rope, dịch chuyển tức thời, trình sửa bản đồ, âm thanh, lưu game, giao diện cho điện thoại cảm ứng.

### 1.3 Giả định ❓
| # | Giả định | Nếu sai thì sao |
|---|---|---|
| A1 | Chạy trên trình duyệt desktop, điều khiển bằng bàn phím | Nếu cần mobile: thêm lớp input cảm ứng, core giữ nguyên |
| A2 | Đồ họa placeholder vẽ bằng code (hình khối, màu), **không dùng asset gốc** của Team17 | Nếu có sprite: chỉ đổi lớp `render/` |
| A3 | Chỉ dùng cá nhân / học tập. Nếu publish thì phải **đổi tên**, vì "Worms" là thương hiệu của Team17 | — |
| A4 | Không cần âm thanh ở MVP | Thêm module `audio/` độc lập, không ảnh hưởng core |

## 2. Tech stack

| Thành phần | Lựa chọn | Lý do |
|---|---|---|
| Ngôn ngữ | **TypeScript** (strict) | Logic vật lý và state machine nhiều, type bắt lỗi sớm |
| Build/dev server | **Vite** | Không cần cấu hình, hot reload, build ra file tĩnh |
| Render | **Canvas 2D API** (không dùng engine) | Địa hình phá hủy theo pixel làm bằng mask và ImageData là đủ. Engine như Phaser/Pixi không giúp gì phần khó nhất mà lại thêm phụ thuộc |
| Vật lý | **Tự viết** (không dùng Matter.js/Box2D) | Worms dùng va chạm với bitmap địa hình, không va chạm giữa các đa giác. Engine rigid-body không hợp |
| Test | **Vitest** | Chạy chung cấu hình với Vite, test logic core thuần |
| Random | PRNG có seed (mulberry32, khoảng 10 dòng) | Tái hiện được bản đồ và gió khi test hoặc debug |
| Runtime phụ thuộc | **0** | Chỉ có devDependencies: `typescript`, `vite`, `vitest` |
| Deploy | Thư mục `dist/` tĩnh (GitHub Pages hoặc mở file trực tiếp) | — |

**Phương án bị loại:** Dùng một file HTML và JS thuần (như đề xuất lúc đầu) thì đơn giản hơn, nhưng khó test và khó chia module khi code vượt khoảng 1.5k dòng. Godot hoặc Unity thì quá nặng cho phạm vi này.

## 3. Architecture

### 3.1 Nguyên tắc
1. **Tách core và I/O.** `src/core/` là logic thuần: không đụng DOM, Canvas hay `Date.now()`. Nhờ vậy test được bằng Vitest mà không cần trình duyệt.
2. **Fixed timestep 60 Hz.** `game.step(intent)` luôn tiến 1/60 giây. Vòng lặp render dùng accumulator.
3. **Deterministic.** Cùng seed và cùng chuỗi intent thì ra cùng kết quả. Tính chất này giúp test ổn định và mở đường cho replay hoặc online sau này (nhưng không làm replay/online ở MVP).
4. **Luồng dữ liệu một chiều:** `Input → Intent → Game.step() → GameState → Renderer (chỉ đọc)`.
5. **Mask địa hình là nguồn sự thật duy nhất.** Renderer chỉ sao chép vùng thay đổi (dirty rect) từ mask sang ảnh.

### 3.2 Sơ đồ module

```
src/
├─ main.ts               # khởi tạo, vòng lặp requestAnimationFrame + accumulator
├─ core/                 # LOGIC THUẦN — không import DOM
│  ├─ rng.ts             # mulberry32(seed)
│  ├─ vec.ts             # vector 2D tối giản
│  ├─ terrain.ts         # Terrain: Uint8Array mask, generate(), isSolid(), carveCircle() -> dirtyRect
│  ├─ physics.ts         # bước tích phân, va chạm vật thể tròn với mask, nảy, trạng thái nghỉ
│  ├─ worm.ts            # đi bộ trên dốc, nhảy, sát thương khi rơi, chết đuối
│  ├─ weapons.ts         # bảng dữ liệu vũ khí (không có class hierarchy)
│  ├─ projectile.ts      # đạn: bay, chịu gió, ngòi nổ, va chạm
│  ├─ explosion.ts       # khoét địa hình, sát thương giảm theo khoảng cách, đẩy lùi
│  ├─ turn.ts            # state machine của lượt
│  └─ game.ts            # GameState + step(intent): điều phối các module trên
├─ input/
│  └─ keyboard.ts        # phím -> Intent (mỗi frame một snapshot)
└─ render/
   ├─ terrainLayer.ts    # offscreen canvas + ImageData, cập nhật theo dirty rect
   ├─ camera.ts          # bám mục tiêu, lerp, giới hạn trong bản đồ
   ├─ entities.ts        # vẽ sâu, đạn, tâm ngắm, thanh lực bắn, hiệu ứng nổ
   └─ hud.ts             # HP các đội, gió, đồng hồ, vũ khí, màn hình kết thúc
tests/                   # *.test.ts cho core/
```

### 3.3 Mô hình dữ liệu chính

```ts
type Intent = {            // snapshot input của một frame
  left: boolean; right: boolean; jump: boolean;
  aimUp: boolean; aimDown: boolean;
  fireHeld: boolean;       // giữ để nạp lực, thả để bắn
  selectWeapon?: WeaponId; fuse?: 1|2|3|4|5;
};

interface Worm { id: number; team: number; pos: Vec; vel: Vec; hp: number;
                 facing: -1|1; aim: number /*rad*/; grounded: boolean; alive: boolean; }

interface Projectile { weapon: WeaponId; pos: Vec; vel: Vec; fuseLeft?: number; }

interface WeaponDef { id: WeaponId; windFactor: number; bounce: number /*0..1*/;
                      fuse: 'impact' | 'timer'; radius: number; maxDamage: number;
                      maxSpeed: number; }

interface GameState { terrain: Terrain; worms: Worm[]; projectiles: Projectile[];
                      wind: number; turn: TurnState; rng: Rng; waterLevel: number; }
```

### 3.4 Các thuật toán cốt lõi

- **Sinh địa hình:** cộng vài sóng sin với biên độ và pha ngẫu nhiên (theo seed) để ra đường chân trời, lấp đầy phần bên dưới, rồi khoét thêm vài hang tròn ngẫu nhiên. Mặc định bản đồ 2000×1000, mỗi đơn vị là 1 pixel.
- **Va chạm:** vật thể là hình tròn. Mỗi sub-step tối đa 1 px (chia nhỏ theo vận tốc) để không xuyên tường. Pháp tuyến ước lượng bằng tổng vector từ các pixel đặc quanh điểm va chạm.
- **Đi bộ trên dốc:** mỗi bước dịch x 1 px, tìm mặt đất trong khoảng ±`MAX_CLIMB` (4 px). Dốc hơn thì bị chặn, hụt chân thì chuyển sang trạng thái rơi.
- **Sát thương khi rơi:** khi chạm đất, nếu `|vy| > V_SAFE` thì mất `(|vy| - V_SAFE) * k` HP.
- **Vụ nổ:** `terrain.carveCircle(c, r)`. Mỗi sâu trong bán kính r mất `maxDamage * (1 - d/r)` HP và bị đẩy theo hướng `(pos - c)`.
- **Gió:** mỗi lượt random trong `[-W, W]`. Gia tốc ngang của đạn là `wind * windFactor` (bazooka 1, grenade 0).
- **Điều kiện "đã yên":** không còn đạn, và mọi sâu có `grounded` với `|vel| < ε` trong 0.5 giây liên tục.

### 3.5 State machine của lượt (`turn.ts`)

```
          ┌──────────────────────────────────────────────────┐
          ▼                                                  │
 TurnStart ──► Aiming ──fire──► Flying ──► Settling ──► Retreat(3s) ──► CheckWin
 (chọn sâu,   (đi/nhảy/ngắm,   (đồng hồ    (chờ vật      (đi lại,        │   │
  random gió,  đồng hồ 45s     tạm dừng)   thể dừng,     không bắn)      │   └─► GameOver
  reset giờ)   chạy)                        xử lý chết)                   └─► TurnStart
 Aiming ──hết giờ──► Settling
```
Chọn sâu: xoay vòng theo đội, trong đội xoay vòng các sâu còn sống. Hòa khi tất cả sâu chết cùng lúc.

### 3.6 Điều khiển (mặc định)
`←/→` đi · `Enter` nhảy · `↑/↓` ngắm · `Space` giữ để nạp lực, thả để bắn · `1/2` chọn vũ khí · `F1–F5` chỉnh ngòi grenade.

## 4. Implementation plan

Mỗi milestone là **một commit (hoặc PR) độc lập, chạy được**. Chỉ bắt đầu milestone sau khi milestone trước đã qua bước verify.

| # | Việc | Verify |
|---|---|---|
| **M0** | Khung dự án: Vite + TS strict + Vitest, `index.html`, canvas toàn màn hình, vòng lặp fixed-step rỗng | `npm run build` pass · `npm test` pass (1 test mẫu) · `npm run dev` hiện canvas |
| **M1** | `rng`, `terrain` (generate, isSolid, carveCircle), `terrainLayer` render có dirty rect. Tạm thời: click chuột để khoét lỗ (debug) | Test: cùng seed ra cùng mask · carve xóa đúng pixel trong bán kính và không đụng ngoài · Mắt: click thấy lỗ ngay, không giật |
| **M2** | `physics` và `worm`: trọng lực, đi trên dốc, nhảy, sát thương khi rơi, chết đuối. Điều khiển 1 sâu | Test: sâu đứng yên trên mặt phẳng · không leo được dốc > MAX_CLIMB · rơi cao thì mất HP · xuống dưới `waterLevel` thì chết |
| **M3** | Ngắm, nạp lực, `projectile` bazooka, gió, `explosion` | Test: không có gió thì quỹ đạo đối xứng · sát thương giảm tuyến tính theo d · không xuyên tường 1 px ở tốc độ tối đa · Mắt: bắn vào đất thấy khoét lỗ |
| **M4** | `turn` state machine, 2 đội, đồng hồ lượt, Retreat, thắng/thua | Test: chuỗi trạng thái đúng như §3.5 · bỏ qua sâu đã chết · hết giờ thì sang lượt · phát hiện thắng và hòa |
| **M5** | Grenade (nảy, ngòi), chọn vũ khí, chỉnh ngòi | Test: grenade không chịu gió · nổ đúng `fuse*60` tick · nảy làm giảm năng lượng |
| **M6** | Camera bám theo, HUD đầy đủ, màn hình bắt đầu và kết thúc, cân chỉnh các hằng số | Checklist chơi thử 1 ván 2v2 từ đầu đến cuối, không lỗi console |

**Tiêu chí hoàn thành MVP:** M0–M6 xong, `npm test` và `npm run build` xanh, chơi hết một ván 2v2 mà không có lỗi nào trong console.

## 5. Hand-off

### 5.1 Lệnh
```bash
npm install
npm run dev      # dev server, http://localhost:5173
npm test         # vitest, chỉ test core/
npm run build    # tsc --noEmit && vite build -> dist/
```

### 5.2 Bất biến, không được phá
1. `src/core/**` **không** import từ `render/`, `input/`, DOM hay `window`. (Có thể thêm lệnh kiểm tra bằng grep vào CI sau.)
2. Mọi thứ ngẫu nhiên trong core đi qua `state.rng`, cấm `Math.random()`.
3. Core không dùng thời gian thực. Chỉ đếm tick, 1 tick = 1/60 giây.
4. Renderer chỉ đọc `GameState`, không sửa.
5. Mọi hằng số gameplay (trọng lực, MAX_CLIMB, V_SAFE, thời gian lượt…) nằm trong `core/constants.ts`.

### 5.3 Cách thêm vũ khí mới
1. Thêm entry vào bảng `WEAPONS` trong `core/weapons.ts`.
2. Nếu hành vi khác bazooka/grenade (ví dụ bắn tia tức thời như shotgun), thêm một nhánh trong `projectile.ts` hoặc `game.ts`. **Không** tạo class hierarchy.
3. Thêm phím chọn vũ khí trong `input/keyboard.ts` và icon trong `render/hud.ts`.
4. Viết test cho hành vi riêng của vũ khí.

### 5.4 Rủi ro và chỗ dễ sai
- **Xuyên tường** khi đạn bay nhanh: bắt buộc sub-step theo độ dài vận tốc.
- **Sâu rung lắc** trên dốc: dùng ngưỡng ε và bộ đếm "đã yên", không so sánh với 0 tuyệt đối.
- **Hiệu năng render**: không vẽ lại cả bản đồ 2000×1000 mỗi frame, chỉ cập nhật dirty rect lên offscreen canvas.
- **Nạp lực**: dựa vào sự kiện thả phím (`fireHeld` chuyển từ true sang false) để bắn, không dựa vào keydown lặp.

### 5.5 Câu hỏi mở cần chốt trước M0 ❓
1. Đồng ý stack **TypeScript + Vite + Canvas 2D + Vitest**?
2. Phạm vi MVP ở §1.1 đã đủ chưa, có cần thêm vũ khí nào không?
3. Đồ họa placeholder vẽ bằng code có được không (A2)?
4. Mỗi milestone commit thẳng lên branch `claude/vibrant-hawking-bbohqb`, hay mở PR riêng cho từng milestone?
