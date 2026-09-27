# Nguồn asset

Repo này public, nên chỉ chứa asset CC0 hoặc tự tạo (docs/PLAN.md §3.3, bất biến 8).

Game không dùng asset bên ngoài có giấy phép hạn chế. Các hình sau do dự án tự tạo:

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
| `client/Assets/Resources/Terrain/painted-soil.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-27; texture đất vẽ tay | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/weapon-icons.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
| `client/Assets/Resources/UI/team-portraits.png` | Tạo bằng OpenAI image generation từ ảnh concept do người dùng chọn, 2026-09-26 | Worms project | Asset gốc do dự án tạo; không dùng hình bên thứ ba |
