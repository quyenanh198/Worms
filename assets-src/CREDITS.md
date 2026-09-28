# Nguồn asset

Repo này public, nên chỉ chứa asset CC0 hoặc tự tạo (docs/PLAN.md §3.3, bất biến 8).

Game không dùng asset bên ngoài có giấy phép hạn chế. Các hình sau do dự án tự tạo:

Bản gốc của toàn bộ 22 ảnh tạo trong đợt concept Worms được lưu tại [generated/README.md](generated/README.md): 2 ảnh tham chiếu, 15 ảnh đang dùng trong Unity và 5 phương án chưa dùng. Các ảnh runtime trong thư mục đó trùng byte với PNG tương ứng dưới `client/Assets/Resources/`.

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
| `client/Assets/Resources/Backdrop/distant-island.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/midground-island.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/foreground-oak.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; cây sồi và hàng rào, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Backdrop/crate-rocks.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; thùng gỗ, đá và bụi cỏ, nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Terrain/painted-soil.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; texture đất vẽ tay | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-red.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; sâu đỏ nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-blue.png` | Biến thể màu xanh từ sprite sâu đỏ bằng OpenAI image generation, 2026-09-27 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-yellow.png` | Biến thể màu vàng từ sprite sâu đỏ bằng OpenAI image generation, 2026-09-27 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/Characters/worm-green.png` | Biến thể màu lục từ sprite sâu đỏ bằng OpenAI image generation, 2026-09-27 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/weapon-icons.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/team-portraits.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/foreground-foliage.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; lá cây tiền cảnh nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/VFX/explosion-burst.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; lửa, khói và đất bay nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/VFX/rocket.png` | Tạo bằng OpenAI image generation theo ảnh concept đã chọn, 2026-09-27; tên lửa trắng đỏ nền trong suốt | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
