# Nguồn asset

Repo này public, nên chỉ chứa asset CC0 hoặc tự tạo (docs/PLAN.md §3.3, bất biến 8).

Hiện tại game **không dùng asset bên ngoài nào**:

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
