# Nguồn asset

Repo này public, nên chỉ chứa asset CC0 hoặc tự tạo (docs/PLAN.md §3.3, bất biến 8).

Game không dùng asset bên ngoài có giấy phép hạn chế. Các hình sau do dự án tự tạo:

Bản gốc của 55 ảnh tạo trong đợt concept Worms được lưu tại [generated/README.md](generated/README.md): 2 ảnh tham chiếu, 48 ảnh trong thư mục Unity và 5 phương án chưa dùng. Các ảnh runtime trong thư mục đó trùng byte với PNG tương ứng dưới `client/Assets/Resources/`. Texture đất cũ vẫn có trong Unity làm phương án dự phòng.

Hai bộ PNG màn chờ ven biển và neon do chủ dự án cung cấp được lưu nguyên bản tại [supplied/README.md](supplied/README.md). Những ảnh dùng cho màn chờ và phụ kiện cửa hàng được sao chép vào `client/Assets/Resources/`.

| Loại | Cách tạo | File |
|---|---|---|
| Địa hình, nước, trời, đồi nền | Shader tự viết, màu từ noise | `client/Assets/Resources/Shaders/*.shader` |
| Con sâu | Mesh dựng từ xương sống bằng code | `client/Assets/Game/Core/WormRig.cs` |
| Vũ khí, đạn, bia mộ | Ghép từ hình khối cơ bản | `client/Assets/Game/Render/WeaponProps.cs` |
| Hiệu ứng | Particle không cần texture | `client/Assets/Game/Render/Vfx.cs`, `Particle.shader` |
| Âm thanh, nhạc nền | Tổng hợp bằng code | `client/Assets/Game/Core/SfxSynth.cs` |

Khi thêm một file asset, ghi một dòng vào bảng dưới đây (nguồn, tác giả, license, đường dẫn).

| File | Nguồn | Tác giả | License |
|---|---|---|---|
| `client/Assets/Resources/Backdrop/cloud-bank.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/cliff-rock-inlay.png` | Tạo bằng OpenAI image generation theo ảnh concept đã chọn, 2026-09-28; cụm đá nằm trong vách đất, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/coastal-citadel.png` | Tạo bằng OpenAI image generation theo ảnh concept đã chọn và asset đảo hiện có, 2026-09-28; hải đăng và phế tích trên vách biển, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/coastal-range-left.png` | Tạo bằng OpenAI image generation theo ảnh concept đã chọn và asset vách biển bên phải, 2026-09-28; dãy núi ven biển phía trái, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/distant-island.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/midground-island.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/foreground-oak.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; cây sồi và hàng rào, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/grass-rock-clump.png` | Tạo bằng OpenAI image generation theo ảnh concept đã chọn và asset cây hiện có, 2026-09-28; cụm cỏ, hoa và đá nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/rocky-grass-bank.png` | Tạo bằng OpenAI image generation theo ảnh concept đã chọn, 2026-09-28; cụm đá lớn và cỏ dùng lại trên các kệ địa hình, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/crate-rocks.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; thùng gỗ, đá và bụi cỏ, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Terrain/painted-soil.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; texture đất vẽ tay | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Terrain/sculpted-soil.png` | Tạo bằng OpenAI image generation theo chất đất của ảnh concept do người dùng chọn, 2026-09-28; texture đất có khối đá lớn | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Terrain/stratified-soil.png` | Tạo và chỉnh mép bằng OpenAI image generation theo ảnh concept đã chọn, 2026-09-28; texture vách đất phân lớp | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Terrain/natural-soil.png` | Tạo bằng OpenAI image generation theo vách đất tự nhiên trong ảnh concept đã chọn, 2026-09-28; texture đất và đá bất quy tắc | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-red.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; sâu đỏ nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-red-aim.png` | Chỉnh sprite sâu đỏ bằng OpenAI image generation, 2026-09-28; dáng ngắm tập trung, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-red-hurt.png` | Biến thể biểu cảm trúng đòn bằng OpenAI image generation, 2026-09-28; nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-blue.png` | Biến thể màu xanh từ sprite sâu đỏ bằng OpenAI image generation, 2026-09-27 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-blue-aim.png` | Biến thể màu xanh từ dáng ngắm đỏ bằng OpenAI image generation, 2026-09-28 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-blue-hurt.png` | Biến thể biểu cảm trúng đòn bằng OpenAI image generation, 2026-09-28; nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-yellow.png` | Biến thể màu vàng từ sprite sâu đỏ bằng OpenAI image generation, 2026-09-27 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-yellow-aim.png` | Biến thể màu vàng từ dáng ngắm đỏ bằng OpenAI image generation, 2026-09-28 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-yellow-hurt.png` | Biến thể biểu cảm trúng đòn bằng OpenAI image generation, 2026-09-28; nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-green.png` | Biến thể màu lục từ sprite sâu đỏ bằng OpenAI image generation, 2026-09-27 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-green-aim.png` | Biến thể màu lục từ dáng ngắm đỏ bằng OpenAI image generation, 2026-09-28 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-green-hurt.png` | Biến thể biểu cảm trúng đòn bằng OpenAI image generation, 2026-09-28; nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/weapon-icons.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/napalm-icon.png` | Tạo bằng OpenAI image generation theo atlas icon hiện có và ảnh concept đã chọn, 2026-09-28; biểu tượng Napalm nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/team-portraits.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/foreground-foliage.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; lá cây tiền cảnh nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/VFX/explosion-burst.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; lửa, khói và đất bay nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/VFX/rocket.png` | Tạo bằng OpenAI image generation theo ảnh concept đã chọn, 2026-09-27; tên lửa trắng đỏ nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |

| `client/Assets/Resources/VFX/rock-debris.png` | Tạo bằng OpenAI image generation theo đá bay trong ảnh concept đã chọn, 2026-09-28; mảnh đá vụ nổ, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/VFX/smoke-puff.png` | Tạo và làm sạch bằng OpenAI image generation theo ảnh concept đã chọn, 2026-09-28; cụm khói tên lửa và vụ nổ, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
