# Worms

Game bắn pháo theo lượt kiểu *Worms*, chơi online. Đồ họa 3D, gameplay trên mặt phẳng 2D. Client Unity 6 (Web, Android, Windows, macOS, Linux), server .NET 10 chạy trên Mac mini tại `chat.lazybutts.com/worms/`.

Kế hoạch đầy đủ: [docs/PLAN.md](docs/PLAN.md). Deploy: [docs/DEPLOY.md](docs/DEPLOY.md). Bàn giao đồ họa: [docs/GRAPHICS_HANDOFF.md](docs/GRAPHICS_HANDOFF.md).

## Đồ họa

Game đang hướng tới phong cách hoạt hình sắc nét của [ảnh tham chiếu được chọn](docs/visuals/approved-cartoon-reference.png): địa hình có lớp đất và mép cỏ, biển và đảo nhiều lớp, sâu có biểu cảm, hiệu ứng đạn/nổ, HUD và menu cùng bảng màu. Bản chạy hiện tại chưa đạt mức chi tiết và bố cục của ảnh mẫu. Có hai cảnh Beach và Meadow, ba mức chất lượng Low/Medium/High và tùy chọn giảm chuyển động. Nguồn gốc các asset được ghi trong [assets-src/CREDITS.md](assets-src/CREDITS.md).

Bản đồ họa hiện tại đã thêm hình sâu cho bốn đội, thung lũng ở giữa bản đồ, đạo cụ tránh điểm xuất hiện, lá tiền cảnh, hiệu ứng nổ vẽ tay và HUD desktop. Ảnh dưới chụp từ **development preview build Windows thật**, seed `123456`, chế độ bốn đội mỗi đội một sâu, bằng Unity 6000.3.25f1 trên desktop tách biệt để không lấy focus. Vách địa hình đã được làm thoải hơn trong dữ liệu va chạm thật; đất dùng texture đá khối mới với tông nâu trầm hơn, HUD hiển thị sát thương đang chờ xử lý; tên lửa, khói, va chạm và hố nổ đã được chụp trong lượt bắn thực. Đây là tiến độ đang kiểm tra, chưa phải bản đồ họa được nghiệm thu: bậc đất còn đều, sâu nhỏ trong khung hình, trời chiếm nhiều diện tích và chất liệu cảnh vẫn khác concept; các trạng thái khác và hiệu năng trên thiết bị cần đối chiếu thêm.

![Cảnh chiến đấu preview bốn đội với HUD](docs/visuals/concept-rebuild-battle-preview.png)

[Xem chế độ mặc định hai đội, bốn sâu mỗi đội](docs/visuals/concept-rebuild-two-team-preview.png) · [màn hình dọc 390×844](docs/visuals/concept-rebuild-portrait-preview.png) · [tên lửa bay](docs/visuals/concept-rebuild-shot-flight.png) · [va chạm](docs/visuals/concept-rebuild-shot-impact.png) · [hố nổ sau khi khói tan](docs/visuals/concept-rebuild-shot-aftermath.png). Ba ảnh cuối lấy từ cùng một lượt bắn trong mô phỏng trận thật. [Ảnh hiệu ứng kích hoạt riêng](docs/visuals/concept-rebuild-vfx-preview.png) chỉ dùng để kiểm tra asset nổ.

Ảnh trên là **trận đấu**, không phải màn hình đầu khi mở app. Menu dùng cảnh đội sâu riêng; ảnh dưới bao gồm cả chữ và giao diện menu, ở trạng thái máy chủ báo cần cập nhật.

![Menu preview thực tế với bốn nhân vật](docs/visuals/concept-rebuild-menu-preview.png)

Ảnh tham chiếu đã chọn là **concept**, còn các ảnh preview là hình từ build thật. Bản đồ mới có hai bờ và thung lũng ở giữa; vách cao thay đổi cách đi bộ/nhảy. Khi các đội còn gần nhau, camera bao quát toàn trận; trên màn hình dọc, camera theo sâu đang chơi. Xem [GRAPHICS_HANDOFF.md](docs/GRAPHICS_HANDOFF.md) để đối chiếu và biết những phần chưa đạt.

## Cấu trúc

| Thư mục | Nội dung |
|---|---|
| `shared/com.worms.sim` | Mô phỏng game, C# thuần, dùng chung cho client và server |
| `shared/com.worms.protocol` | Message mạng và codec nhị phân |
| `server/` | Game server (ASP.NET Core, WebSocket), đồng thời phục vụ bản Web |
| `client/` | Project Unity 6000.3.25f1 (URP; desktop, WebGL và Android) |
| `tools/` | `gen_meta.py`, `unity-check/` (compile-check code Unity không cần license) |

## Lệnh

```bash
dotnet test Worms.slnx                    # GameCore, server, protocol, sim
dotnet run --project server/Server        # http://localhost:8080, WebSocket tại /ws (chế độ khách vì chưa đặt CHAT_API_URL)
docker build -t worms:dev .               # image server (+ trang tạm nếu chưa có bản Web)
python3 tools/gen_meta.py                 # sinh .meta cho file mới trong client/Assets và shared/
```

Bản build desktop có thể chạy thẳng vào trận offline bằng tham số `-sandbox`, và chỉ định server bằng `-server ws://host:8080/ws`.

Build Unity chạy trên GitHub Actions (GameCI) sau khi thêm secret `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`.

Đợt đồ họa ngày 2026-09-27 đã qua 158 bài test .NET và build Windows/WebGL bằng Unity 6000.3.25f1 trong batch mode, không có lỗi. Giao diện WebGL khi chạy trong trình duyệt, thao tác chạm và FPS/bộ nhớ trên thiết bị thật vẫn cần kiểm tra; xem [ma trận xác minh](docs/GRAPHICS_HANDOFF.md#verification-matrix).

## Asset đồ họa từ concept

[Bộ 49 ảnh PNG gốc đã tạo](assets-src/generated/README.md) được lưu trong repo: `concept/` chứa ảnh chuẩn và bảng định hướng, `runtime/` chứa bản gốc của 42 ảnh trong `client/Assets/Resources/`, `drafts/` chứa các phương án chưa dùng. Ảnh chuẩn trong `assets-src/generated/concept/approved-battle.png` trùng byte với ảnh đối chiếu ở tài liệu. Các bản runtime cũng trùng byte với asset Unity tương ứng; ảnh concept nguyên cảnh và bản nháp không được nạp làm sprite trong game. Texture đất cũ vẫn được giữ làm phương án dự phòng.
