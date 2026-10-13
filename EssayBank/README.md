# Ngân hàng đề – E1 (kế hoạch gameplay tự luận v3.2)

## Có gì
- `seed_problems.csv` – 3 bài mẫu từ Phụ lục A (Lý, Hóa, Sinh) theo đúng cột mục 11. Mở bằng Google Sheet (File → Import) để dùng làm khuôn.
- `tools/validate_bank.py` – kiểm lược đồ: cột, id trùng, JSON, tham số khớp đề, công thức đáp số, barem, 3 gợi ý, số bước theo mức (mục 15.1), bảng độ phủ so với mục tiêu 240 đề.
- `tools/generate_variants.py` – sinh biến thể số liệu và đáp số bằng code (nguyên tắc 3: AI không tự tính).
- `tools/bank.py` – thư viện chung. Công thức đáp số chỉ cho số, tên tham số, `+ - * / **`, ngoặc, để dễ viết lại bộ tính bằng C# trong Unity.

## Cách dùng
    python3 tools/validate_bank.py seed_problems.csv
    python3 tools/generate_variants.py seed_problems.csv --n 3 --seed 1

## Quyết định cần bạn duyệt (cổng E1)
1. Mức độ của 3 bài mẫu: ly8-kl-001 và hoa8-tpt-001 xếp `normal`, sinh8-mdqt-001 xếp `hard` (cả ba có 4 bước barem; mục 11 ghi bài Lý là `hard`, mục 15.1 lại xếp cùng dạng vào Bình thường).
2. Barem bài Lý lấy theo Phụ lục A (s2 = thế số, s3 = đổi kg/m³, s4 = đơn vị), không theo ví dụ rút gọn ở mục 11.
3. `finalAnswer` hiện chỉ có một công thức. Bài có nhiều đáp số (Lý: g/cm³ và kg/m³; Sinh: mật độ đầu và mới) đang kiểm đáp số cuối. Nếu muốn code kiểm từng kết quả trung gian, cần mở rộng thành danh sách.
4. Tham số được chọn độc lập cho từng biến. Muốn ràng buộc (ví dụ số mol chẵn) thì thêm bộ tham số theo cặp.
5. Cột `params` của Sinh dùng 1 giá trị mỗi tham số vì số liệu lấy từ Bảng 42.1; cần thêm bộ số khác khi soạn kho đề thật.

## Việc còn lại
- Kho 240 đề (20 đề × 3 môn × 4 mức) do bạn soạn riêng, bám Phụ lục B.
- E2 trở đi cần dự án Unity MathTutorial_G8 (AIService, ProgressSaveSystem, LoginScene), chưa có trong phiên này.
