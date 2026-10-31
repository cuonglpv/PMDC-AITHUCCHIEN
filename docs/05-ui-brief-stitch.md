# UI Brief cho Stitch — cần bạn chốt

> Trạng thái: ĐÃ CHỐT (31/10/2026). Cột **Bạn chốt** ghi quyết định cuối cùng.

## 1. Stitch cần input gì (tóm tắt từ tài liệu Stitch)

Nguồn: Stitch docs (*Effective Prompting*, *DESIGN.md overview & specification*) và repo chính thức `google-labs-code/stitch-skills` (`design-md`, `enhance-prompt`).

| Input | Nội dung | Trạng thái |
|---|---|---|
| **DESIGN.md** | Front matter YAML chứa token (`colors`, `typography`, `rounded`, `spacing`, `components`) + 8 mục theo thứ tự: Overview (Brand & Style) · Colors · Typography · Layout · Elevation & Depth · Shapes · Components · Do's and Don'ts. Màu ghi theo dạng *"Tên mô tả (#hex) – vai trò"*. | Mình sẽ viết sau khi bạn chốt mục 2 |
| **Vibe (tính từ)** | Stitch dùng tính từ để suy ra màu, font, hình ảnh. | Cần chốt |
| **Platform / Device type** | Web hoặc App, desktop-first hay mobile-first. | Đề xuất: Web, desktop 1440 px |
| **Prompt từng màn hình** | 1 câu mục đích + vibe → khối `DESIGN SYSTEM (REQUIRED)` → `Page Structure` đánh số từng vùng, dùng đúng từ khoá UI (navigation bar, data table, side panel, stepper…). | Mình viết từ IA ([04-information-architecture.md](04-information-architecture.md)) |
| **Cách lặp** | Đầu tiên prompt tổng quát, sau đó sửa từng màn hình, mỗi lần chỉ thay đổi 1–2 điểm, nêu rõ sửa gì và sửa thế nào. | Quy trình làm việc |
| **Branding có sẵn (tuỳ chọn)** | Stitch có thể rút design system từ URL website hoặc ảnh. | Bạn có thì gửi |

Đã có sẵn từ IA: danh sách màn hình, bố cục 3 vùng của Claim workspace, các trạng thái UI, các pattern hiển thị AI và dữ liệu mẫu (hồ sơ 1.400.000 → 800.000 ₫).

## 2. Các quyết định cần bạn chốt

| # | Câu hỏi | Lựa chọn | Đề xuất của mình | Bạn chốt |
|---|---|---|---|---|
| 1 | Tên sản phẩm & logo | Giữ tên tạm **ClaimLens** / tên khác / có logo riêng | Giữ ClaimLens, logo dạng chữ đơn giản | ClaimLens, logo dạng chữ (wordmark) |
| 2 | Ngôn ngữ giao diện | Tiếng Việt / English / song ngữ | **Tiếng Việt** cho nhãn, giữ mã kỹ thuật (UC, EX-04) | **English** cho toàn bộ nhãn; dữ liệu mẫu (tên, địa chỉ) vẫn tiếng Việt |
| 3 | Theme | Light / Dark / cả hai | **Light** là chính, có thêm dark mode | **Sáng + shell đen**: vùng làm việc nền giấy/trắng, sidebar và Sign in nền đen #090909; token dark mode để sẵn |
| 4 | Vibe (3–5 tính từ) | vd. *calm, trustworthy, clinical-precise, information-dense but airy, modern* | Như ví dụ bên trái | **precise, accountable, editorial, high-contrast, calm** |
| 5 | Màu thương hiệu | Màu bạn thích / URL brand công ty bảo hiểm để Stitch import / để mình chọn | Primary **Deep Trust Blue (#1A56DB)**, AI accent **Insight Violet (#7C3AED)**, nền **Paper White (#F8FAFC)**, trạng thái xanh/hổ phách/đỏ | **Ink #090909 · Paper #F4F3EF · Line #D8D6D1 · Signal Red #EB0029** (chỉ CTA chính, nav đang chọn, focus) · cảnh báo/từ chối **#A40022** + icon · AI **Insight Violet #6D28D9** |
| 6 | Font (phải hỗ trợ dấu tiếng Việt) | Inter / Be Vietnam Pro / Manrope / Roboto | **Be Vietnam Pro** cho UI, **JetBrains Mono** cho số tiền và mã | **Lexend Exa** (tiêu đề IN HOA) + **Be Vietnam Pro** (UI) + **IBM Plex Mono** (nhãn, số tiền, mã). Cả ba có tiếng Việt |
| 7 | Mật độ thông tin | Compact (công cụ chuyên nghiệp) / Comfortable | **Compact** cho bảng và workspace, comfortable cho dashboard | **Compact** (dòng 36 px, chữ 13–14 px) cho bảng và workspace; dashboard và Sign in thoáng kiểu editorial |
| 8 | Hình khối | Góc vuông / bo nhẹ 6–8 px / bo nhiều 12–16 px / pill | Bo nhẹ **8 px** cho control, **12 px** cho card, pill cho status chip | **Gần vuông 2 px** cho control và card; drawer/modal 0 px; status pill bo tròn |
| 9 | Độ nổi (elevation) | Flat + viền mảnh / bóng mềm / bóng đậm | **Flat + viền mảnh**, chỉ drawer/modal có bóng mềm | **Flat + đường kẻ 1 px** (#D8D6D1); chỉ drawer, modal, popover có bóng mềm |
| 10 | Sản phẩm tham khảo về cảm giác | vd. Linear, Stripe Dashboard, Google Material 3, Notion | Stripe Dashboard × Material 3 | **VinGuard × Linear** |
| 11 | Màn hình làm trước | Chọn trong danh sách ưu tiên bên dưới | 6 màn ưu tiên | **6 màn ưu tiên + `SCR-01` Sign in** (7 màn) |
| 12 | Ai chạy Stitch | (a) Bạn dán prompt mình viết vào stitch.withgoogle.com · (b) Kết nối Stitch MCP để mình gọi trực tiếp · (c) Mình dựng luôn HTML/React theo DESIGN.md, không qua Stitch | (a) hoặc (b); thêm (c) nếu cần bản chạy được | **(a)** Bạn dán prompt vào stitch.withgoogle.com (không cần API key), gửi lại ảnh chụp/HTML export để review |
| 13 | Mức accessibility | WCAG AA / AAA | **AA** (tương phản ≥ 4.5:1, không dùng màu làm tín hiệu duy nhất) | **WCAG AA** + đi được toàn bộ bằng bàn phím + tôn trọng `prefers-reduced-motion` |
| 14 | Có thêm yêu cầu hay cấm kỵ nào không | vd. "không gradient", "không emoji", "có minh hoạ"… | Không gradient lớn, không emoji, icon dạng line | Không gradient, không emoji (icon line) · Motion chừng mực: trong app 150–250 ms, reveal/blur chỉ ở Sign in và trạng thái rỗng · Không chữ IN HOA dài: IN HOA chỉ cho tiêu đề ngắn và nhãn mono · Không ảnh minh hoạ/stock: trạng thái rỗng dùng icon + chữ |

### Màn hình ưu tiên (đề xuất cho câu 11)

1. `SCR-31` Claim workspace — Evidence (xem tài liệu cạnh trường dữ liệu AI trích xuất, có độ tin cậy)
2. `SCR-33` Claim workspace — Adjudication (bảng từng khoản chi phí, mã quy tắc, công thức, hạn mức còn lại)
3. `SCR-35` Claim workspace — Decision (bản tóm tắt AI soạn có trích dẫn, chọn đề xuất)
4. `SCR-20` Review queue (danh sách hồ sơ theo độ ưu tiên)
5. `SCR-13` Upload tài liệu và kiểm tra trang bằng AI
6. `SCR-50` Operations dashboard (kèm chất lượng AI)

## 2b. Moodboard tham khảo (bạn gửi)

Nguồn: VinGuard — AI Gateway, `https://c4-app-079.mastercuberskill.workers.dev/landing` (và `/login`). Bạn muốn tham khảo: **bảng màu, cách thiết kế từng màn, transition**. Có thể dùng làm input "import from website" của Stitch.

| Khía cạnh | VinGuard làm gì (đo từ CSS) | Áp vào ClaimLens |
|---|---|---|
| Màu | `--black #090909`, `--red #EB0029` (đỏ tín hiệu), `--red-dark #A40022`, `--paper #F4F3EF` (giấy ấm), `--line #D8D6D1`, `--muted #6D6B68`, trắng `#FFFFFF`. Chủ yếu đen/giấy/trắng, đỏ dùng rất ít. | Đen + giấy ấm + 1 màu nhấn. Vì đỏ cũng là màu "từ chối/chặn" trong xử lý hồ sơ, cần tách đỏ thương hiệu khỏi đỏ cảnh báo (xem câu 5). |
| Font | Display **Michroma** (chữ rộng, IN HOA, letter-spacing âm), body **Instrument Sans**, nhãn **IBM Plex Mono** IN HOA, giãn chữ 1.1 px. | Michroma và Instrument Sans **không có dấu tiếng Việt** (Google Fonts chỉ có latin/latin-ext), nên phải đổi sang font tương đương có tiếng Việt (xem câu 6). IBM Plex Mono có tiếng Việt, giữ được. |
| Hình khối | `border-radius: 0` ở mọi phần tử; lưới chia bằng đường kẻ 1 px; không bóng đổ. | Góc vuông hoặc gần vuông, flat + đường kẻ (câu 8, 9). |
| Bố cục từng màn | Mỗi section là một dải màu riêng (đen, giấy, trắng, đỏ). Tiêu đề lớn IN HOA bên trái, đoạn dẫn bên phải. Item đánh số `01`, `02 / 04` bằng mono đỏ, kicker mono IN HOA phía trên tiêu đề. Lưới 2 cột ngăn bằng đường kẻ. | App làm việc cần mật độ cao: dùng kicker mono + số thứ tự cho tiêu đề panel, stepper, các bước tính toán, các khối KPI. Không dùng tiêu đề hero cỡ lớn trong workspace. |
| Transition | Hover 0.25 s (màu/nền) kèm `translateY(-2px)`. Reveal khi cuộn: opacity 0→1, `translateY(34px)`, `blur(4px)`, 0.85 s `cubic-bezier(0.16,1,0.3,1)`. Chữ trồi lên `rise` 0.95 s. Marquee 32 s. Focus `outline 2px` màu đỏ, offset 4 px. | Giữ easing `cubic-bezier(0.16,1,0.3,1)` làm easing chuẩn. Trong app chỉ dùng 150–250 ms (hover, đổi tab, drawer trượt vào, highlight vùng nguồn); reveal/blur chỉ dùng cho màn Sign in và trạng thái rỗng. Tôn trọng `prefers-reduced-motion`. |

## 3. Sau khi bạn chốt, mình sẽ làm

1. `DESIGN.md` theo đúng spec của Stitch (YAML token + 8 mục + Do's and Don'ts, có token riêng cho các trạng thái AI: suggestion, confidence, signal).
2. `stitch-prompts.md`: 1 prompt tổng quát cho toàn app, sau đó 1 prompt cho mỗi màn hình ưu tiên, kèm prompt chỉnh sửa để lặp.
3. Tuỳ lựa chọn ở câu 12: chạy Stitch qua MCP, hoặc dựng prototype HTML để xem ngay.
