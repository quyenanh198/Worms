# Worms

Game bắn pháo theo lượt kiểu *Worms*, chơi online. Đồ họa 3D, gameplay trên mặt phẳng 2D. Client Unity 6 (Web, Android, Windows, macOS, Linux), server .NET 10 chạy trên Mac mini tại `chat.lazybutts.com/worms/`.

Kế hoạch đầy đủ: [docs/PLAN.md](docs/PLAN.md). Deploy: [docs/DEPLOY.md](docs/DEPLOY.md).

## Cấu trúc

| Thư mục | Nội dung |
|---|---|
| `shared/com.worms.sim` | Mô phỏng game, C# thuần, dùng chung cho client và server |
| `shared/com.worms.protocol` | Message mạng và codec nhị phân |
| `server/` | Game server (ASP.NET Core, WebSocket), đồng thời phục vụ bản Web |
| `client/` | Project Unity 6000.3 |
| `tools/` | `gen_meta.py`, `unity-check/` (compile-check code Unity không cần license) |

## Lệnh

```bash
dotnet test Worms.slnx                    # sim, protocol, server
dotnet run --project server/Server        # http://localhost:8080, WebSocket tại /ws (chế độ khách vì chưa đặt CHAT_API_URL)
docker build -t worms:dev .               # image server (+ trang tạm nếu chưa có bản Web)
python3 tools/gen_meta.py                 # sinh .meta cho file mới trong client/Assets và shared/
```

Bản build desktop có thể chạy thẳng vào trận offline bằng tham số `-sandbox`, và chỉ định server bằng `-server ws://host:8080/ws`.

Build Unity chạy trên GitHub Actions (GameCI) sau khi thêm secret `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
