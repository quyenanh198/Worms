# Deploy Worms lên Mac mini

Server Worms chạy thành container `worms` trong repo `macmini-hub`, sau Caddy ở
`https://chat.lazybutts.com/worms/`. Cùng host với Chat nên trình duyệt gửi kèm cookie
`lb_session`; server hỏi `CHAT_API_URL/api/me` để biết người chơi là ai.

## Image

Workflow `build.yml` đẩy `ghcr.io/quyenanh198/worms:latest` (linux/arm64 + amd64) mỗi lần
merge vào `main`. Có bản Web thì image phục vụ luôn bản Web ở `/`; chưa có (thiếu secret
Unity) thì `/` là trang giữ chỗ, `/healthz` và `/ws` vẫn chạy.

Lần đầu: GitHub → Packages → `worms` → Package settings → đổi visibility thành **Public**
(nếu không Mac mini phải `docker login ghcr.io`).

## Biến môi trường

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `PORT` | `8080` | Cổng Kestrel |
| `CHAT_API_URL` | (trống) | Gốc API của Chat, ví dụ `http://chat:8082`. Trống thì chơi bằng tên khách (`?name=`) |
| `ALLOWED_ORIGINS` | (trống) | Origin trình duyệt được mở `/ws`, cách nhau bằng dấu phẩy. Trống thì chặn mọi trình duyệt; client native không gửi Origin nên không bị ảnh hưởng |
| `MAX_CONNECTIONS` | `1000` | Quá số này thì `/ws` bị từ chối |
| `MAX_ROOMS` | `200` | Quá số này thì tạo phòng / quick match trả lỗi `server_full` |
| `STATUS_TOKEN` | (trống = tắt) | Bật `/status?token=…` (số kết nối, phòng, phòng đang chơi, uptime) |
| `SIM_SPEED` | `1` | Chỉ dùng trong test |

## Lần deploy đầu

1. Review và merge PR `claude/vibrant-hawking-bbohqb` trong `macmini-hub` (service `worms`,
   route Caddy `/worms/*`, tile trên hub, `WORMS_STATUS_TOKEN` trong `.env.example`).
2. Trên Mac mini:
   ```sh
   cd ~/macmini-hub && git pull
   echo "WORMS_STATUS_TOKEN=$(openssl rand -hex 16)" >> .env   # tuỳ chọn
   docker compose pull worms
   scripts/deploy.sh worms
   docker compose exec caddy caddy reload --config /etc/caddy/Caddyfile
   ```
3. Kiểm tra:
   ```sh
   curl -fsS https://chat.lazybutts.com/worms/healthz
   curl -fsS "https://chat.lazybutts.com/worms/status?token=$WORMS_STATUS_TOKEN"
   ```
   Mở `https://chat.lazybutts.com/worms/` khi đã đăng nhập Chat: menu phải hiện tên tài khoản.

## Cập nhật

```sh
docker compose pull worms && scripts/deploy.sh worms
```

Các trận đang chơi **mất** khi thay container (trạng thái trận chỉ nằm trong RAM). Deploy lúc
không ai chơi: `/status` cho biết `playing`.

## Rollback

```sh
scripts/deploy.sh worms --rollback
```

Chạy lần nữa để tiến lại. Không có dữ liệu cần lùi theo.

## Giới hạn đã đo

`LoadTests` chạy 30 trận thời gian thực song song trong một tiến trình: mỗi client nhận
179–180/180 tick trong 3 giây. Chưa đo trong container với `mem_limit: 384m`; nếu `docker stats worms`
cho thấy bộ nhớ sát trần thì hạ `MAX_ROOMS`.
