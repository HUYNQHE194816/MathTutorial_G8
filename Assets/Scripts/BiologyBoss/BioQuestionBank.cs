using System.Collections.Generic;

[System.Serializable]
public class BioQuestion
{
    public string text;
    /// <summary>answers[0] là đáp án ĐÚNG, 3 phần tử còn lại là đáp án sai. Game tự xáo trộn khi hiển thị.</summary>
    public string[] answers;
    /// <summary>Bài học (cột "Bài" trong CSV/Sheet, vd "Bài 33"). Có thể để trống.</summary>
    public string lesson;
    public BioQuestion(string t, params string[] a) { text = t; answers = a; }
}

/// <summary>
/// Ngân hàng câu hỏi Sinh học 8. Muốn đổi / thêm câu hỏi: sửa danh sách bên dưới
/// (đáp án đúng luôn viết ĐẦU TIÊN). Game dùng tối đa BossFightManager.totalQuestions câu.
/// </summary>
public static class BioQuestionBank
{
    public static List<BioQuestion> Build() => new List<BioQuestion>
    {
        new BioQuestion("Cơ quan nào của hệ hô hấp là nơi diễn ra trao đổi khí giữa cơ thể và môi trường?", "Phổi", "Khí quản", "Thanh quản", "Mũi"),
        new BioQuestion("Đơn vị cấu tạo và chức năng của cơ thể người là gì?", "Tế bào", "Mô", "Cơ quan", "Hệ cơ quan"),
        new BioQuestion("Máu gồm huyết tương và thành phần nào?", "Các tế bào máu", "Chỉ có hồng cầu", "Bạch huyết", "Chất xơ"),
        new BioQuestion("Chức năng chính của hồng cầu là gì?", "Vận chuyển O2 và CO2", "Tiêu diệt vi khuẩn", "Làm đông máu", "Tạo kháng thể"),
        new BioQuestion("Tim người có bao nhiêu ngăn?", "4 ngăn", "2 ngăn", "3 ngăn", "5 ngăn"),
        new BioQuestion("Nhóm máu nào có thể truyền cho các nhóm khác theo nguyên tắc truyền máu cơ bản?", "Nhóm O", "Nhóm A", "Nhóm B", "Nhóm AB"),
        new BioQuestion("Enzim amilaza trong nước bọt biến đổi chất nào?", "Tinh bột thành đường mantôzơ", "Prôtêin thành axit amin", "Lipit thành glixerin", "Đường thành tinh bột"),
        new BioQuestion("Cơ quan nào hấp thụ phần lớn chất dinh dưỡng?", "Ruột non", "Dạ dày", "Ruột già", "Thực quản"),
        new BioQuestion("Đơn vị chức năng của thận gồm những phần nào?", "Cầu thận, nang cầu thận, ống thận", "Bể thận, niệu quản", "Bóng đái, ống đái", "Vỏ thận, tủy thận"),
        new BioQuestion("Tuyến nào tiết hoocmôn insulin điều hòa đường huyết?", "Tuyến tụy", "Tuyến giáp", "Tuyến yên", "Tuyến trên thận"),
        new BioQuestion("Bộ phận nào của não điều khiển sự thăng bằng và phối hợp vận động?", "Tiểu não", "Đại não", "Hành não", "Cầu não"),
        new BioQuestion("Tế bào que ở màng lưới giúp mắt nhìn trong điều kiện nào?", "Ánh sáng yếu", "Ánh sáng mạnh", "Phân biệt màu sắc", "Không cần ánh sáng"),
        new BioQuestion("Xương dài ra nhờ cấu trúc nào?", "Sụn tăng trưởng", "Màng xương", "Tủy đỏ", "Mô xương cứng"),
        new BioQuestion("Vitamin D có vai trò gì đối với xương?", "Giúp hấp thụ canxi", "Tạo hồng cầu", "Cung cấp năng lượng", "Làm đông máu"),
        new BioQuestion("Phản xạ có điều kiện khác phản xạ không điều kiện ở điểm nào?", "Hình thành trong đời sống nhờ học tập", "Bẩm sinh, di truyền", "Bền vững, không thay đổi", "Chỉ do tủy sống điều khiển"),
    };
}
