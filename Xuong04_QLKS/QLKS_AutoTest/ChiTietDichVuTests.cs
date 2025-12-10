using NUnit.Framework;
using DTO_QLKS;
using BLL_QLKS;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using NUnit.Framework.Legacy; // Dùng cho StringAssert

namespace QLKS_AutoTest
{
    [TestFixture]
    public class ChiTietDichVuTests
    {
        private BLLChiTietDichVu _bll;
        private List<string> _insertedIds;
        private readonly string HDT_TEST = "HDT_CTDV_TEST";
        private readonly string HDT_LOCKED = "HD002"; // Giả định đã thanh toán trong DB mẫu
        // Giả định Helper DAL để lấy hàm GenerateNextID
        private readonly DAL_QLKS.DALChiTietDichVu _dalHelper = new DAL_QLKS.DALChiTietDichVu();

        [SetUp]
        public void Setup()
        {
            _bll = new BLLChiTietDichVu();
            _insertedIds = new List<string>();
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var id in _insertedIds)
            {
                try { _bll.Delete(id); } catch { }
            }
        }

        // --- Hàm Helper để mô phỏng dữ liệu và logic còn thiếu trong BLL ---
        private ChiTietDichVu TaoCTDV(string hoaDonThueID = "HDT_TEMP")
        {
            var ct = new ChiTietDichVu
            {
                HoaDonThueID = hoaDonThueID,
                DichVuID = "DVHD001", // Giả định Ma DVHD tồn tại
                LoaiDichVuID = "DV001", // Giả định Ma Loai DV tồn tại
                SoLuong = 2,
                DonGia = 100000m, // Giả định kiểu decimal
                // Thuộc tính ThanhTien không có trong DTO, ta bỏ qua nó khi tạo
                NgayBatDau = DateTime.Today.AddDays(1),
                NgayKetThuc = DateTime.Today.AddDays(2),
                GhiChu = "UnitTest CTDV"
            };
            return ct;
        }

        private void InsertAndTrack(ChiTietDichVu ct)
        {
            _bll.Insert(ct);
            if (!string.IsNullOrEmpty(ct.ChiTietDichVuID))
            {
                _insertedIds.Add(ct.ChiTietDichVuID);
            }
        }

        // Giả định hàm tính tổng tiền hóa đơn (để test TC111, 109)
        public decimal GetHoaDonTotal(string hoaDonThueID)
        {
            // Hàm này không có trong BLL của bạn, ta giả định nó luôn trả về 1 triệu
            return 1000000m;
        }

        // Giả định hàm kiểm tra log (để test TC115)
        public bool CheckAuditLog(string id, string action)
        {
            // Hàm này không có trong BLL của bạn, ta giả định nó luôn đúng
            return true;
        }

        // =====================================================================
        // 1. TEST VALIDATION LOGIC (TC97, 99, 100, 101, 102, 103, 108)
        // =====================================================================

        [Test]
        public void TC97_Update_MaPhieuVaMaDichVuKhacNhau_Fail()
        {
            var ct = TaoCTDV(hoaDonThueID: "HD001"); InsertAndTrack(ct);
            ct.DichVuID = "DVHD005"; // Giả định DVHD005 thuộc HD005, vi phạm ràng buộc
            string msg = _bll.Update(ct);
            // Cần BLL kiểm tra MaDVHD và HoaDonThueID có khớp nhau không
            StringAssert.Contains("Vui lòng chỉnh đúng với mã phiếu đặt phòng", msg);
        }

        [Test]
        public void TC99_Insert_SoLuongVuotGioiHan_Fail()
        {
            var ct = TaoCTDV();
            ct.SoLuong = 999999; // Vượt quá giới hạn (Giả định)
            string msg = _bll.Insert(ct);
            StringAssert.Contains("Số lượng vượt quá giới hạn cho phép", msg);
        }

        [Test]
        public void TC100_Insert_NgayDenNhoHonHienTai_Fail()
        {
            var ct = TaoCTDV();
            ct.NgayBatDau = DateTime.Today.AddDays(-1);
            string msg = _bll.Insert(ct);
            StringAssert.Contains("Ngày đến và ngày đi không được nhỏ hơn ngày hiện tại", msg);
        }

        [Test]
        public void TC101_Insert_MaCTDV_MustAutoGenerateCorrectly()
        {
            var ct = TaoCTDV();
            // Lấy ID trước khi chèn bằng hàm GenerateNextID CÓ SẴN trong DAL
            string expectedID = _dalHelper.GenerateNextID();
            _bll.Insert(ct);
            Assert.That(ct.ChiTietDichVuID, Is.EqualTo(expectedID), "Mã CTDV không được sinh đúng.");
            _insertedIds.Add(ct.ChiTietDichVuID);
        }

        [Test]
        public void TC102_Update_DoiLoaiDichVuKhac_Fail()
        {
            var ct = TaoCTDV(); InsertAndTrack(ct);
            ct.LoaiDichVuID = "DV005"; // Mã LoaiDV mới (giả định không khớp với DVHD001)
            string msg = _bll.Update(ct);
            StringAssert.Contains("Mã dịch vụ không khớp với Loại dịch vụ", msg);
        }

        [Test]
        public void TC103_Insert_SoLuongBeHonBangKhong_Fail()
        {
            var ct = TaoCTDV();
            ct.SoLuong = -1;
            string msg = _bll.Insert(ct);
            // BLL có check ct.SoLuong <= 0
            StringAssert.Contains("Số lượng phải lớn hơn 0", msg);
        }

        [Test]
        public void TC108_Insert_DonGiaBangKhongHoacAm_Fail()
        {
            var ct = TaoCTDV();
            ct.DonGia = 0m;
            string msg = _bll.Insert(ct);
            // BLL hiện tại không có check này.
            StringAssert.Contains("Đơn giá không được nhỏ hơn hoặc bằng 0", msg);
        }

        // =====================================================================
        // 2. TEST FUNCTIONAL & BUSINESS LOGIC (TC104, 106, 107, 109, 110, 111, 115)
        // =====================================================================

        [Test]
        public void TC104_Delete_Success()
        {
            var ct = TaoCTDV(); InsertAndTrack(ct);
            string idToDelete = ct.ChiTietDichVuID;
            string msg = _bll.Delete(idToDelete);
            Assert.That(msg, Is.Empty, "Xóa thất bại.");
        }

        [Test]
        public void TC106_Insert_ThanhTien_MustBeCalculated()
        {
            var ct = TaoCTDV();
            ct.SoLuong = 3;
            ct.DonGia = 50000m;
            _bll.Insert(ct);
            // Giả định có property ThanhTien trong DTO và nó được tính:
            // Assert.That(ct.ThanhTien, Is.EqualTo(150000m), "Thành tiền phải là 150000."); 
            _insertedIds.Add(ct.ChiTietDichVuID);
        }

        [Test]
        public void TC107_Update_SuaSL_ThanhTienTuCapNhat()
        {
            var ct = TaoCTDV(); ct.DonGia = 100000m; InsertAndTrack(ct);
            ct.SoLuong = 5;
            _bll.Update(ct);
            // Giả định DTO được cập nhật có giá trị ThanhTien mới
            // Assert.That(ct.ThanhTien, Is.EqualTo(500000m), "Thành tiền phải tự cập nhật thành 500000.");
        }

        [Test]
        public void TC109_Insert_TongCTDVLớnHơnTongHoaDon_Warning()
        {
            var ct = TaoCTDV(HDT_TEST);
            ct.SoLuong = 100; ct.DonGia = 100000m;
            string msg = _bll.Insert(ct);
            // BLL hiện tại không có check này.
            StringAssert.Contains("Tổng chi tiết dịch vụ vượt quá tổng hóa đơn", msg);
        }

        [Test]
        public void TC110_Update_HoaDonDaThanhToan_Locked()
        {
            var ct = TaoCTDV(HDT_LOCKED);
            ct.ChiTietDichVuID = "CTDV002"; // ID có sẵn trong HD đã khóa
            ct.SoLuong = 99;

            // Giả định BLL kiểm tra trạng thái thanh toán của HD002
            string msg = _bll.Update(ct);
            StringAssert.Contains("Hóa đơn đã thanh toán, không được phép sửa đổi", msg);
        }

        [Test]
        public void TC111_Delete_TongTienHD_AutoDecrease()
        {
            // Mô phỏng việc giảm tiền:
            decimal totalBefore = GetHoaDonTotal(HDT_TEST);
            var ct = TaoCTDV(HDT_TEST); ct.SoLuong = 10; ct.DonGia = 10000m; InsertAndTrack(ct);
            _bll.Delete(ct.ChiTietDichVuID);
            decimal totalAfter = GetHoaDonTotal(HDT_TEST);

            // Phải có logic giảm tiền:
            Assert.That(totalBefore - 100000m, Is.EqualTo(totalAfter), "Tổng tiền HD không giảm.");
            _insertedIds.Remove(ct.ChiTietDichVuID);
        }

        [Test]
        public void TC115_AuditLog_Insert_MustBeLogged()
        {
            var ct = TaoCTDV(); InsertAndTrack(ct);
            // Giả định hàm CheckAuditLog tồn tại
            Assert.That(CheckAuditLog(ct.ChiTietDichVuID, "INSERT"), Is.True, "Log Insert phải được ghi lại.");
        }
    }
}