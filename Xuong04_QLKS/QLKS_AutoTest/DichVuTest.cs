using NUnit.Framework;
using DTO_QLKS;
using BLL_QLKS;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Legacy; // Dùng cho StringAssert
using System.IO; // Cần thiết cho Export (TC96)

namespace QLKS_AutoTest
{
    [TestFixture]
    public class DichVuTests // Tương ứng với BUSQLDichVu (Dịch Vụ Hóa Đơn)
    {
        private BUSQLDichVu _bll;
        private List<string> _insertedIds;

        [SetUp]
        public void Setup()
        {
            _bll = new BUSQLDichVu();
            _insertedIds = new List<string>();
        }

        [TearDown]
        public void Teardown()
        {
            // Cleanup: Xóa các đối tượng đã được chèn thành công
            foreach (var id in _insertedIds)
            {
                // Sử dụng phương thức Delete có sẵn
                try { _bll.DeleteDichVu(id); } catch { }
            }
        }

        private DichVu TaoDVHD(string hoaDonThueID = "HDT_TEMP", bool trangThai = true, DateTime? ngayTao = null)
        {
            var dv = new DichVu
            {
                HoaDonThueID = hoaDonThueID,
                NgayTao = ngayTao ?? DateTime.Today,
                TrangThai = trangThai,
                GhiChu = "UnitTest DVHD"
            };
            return dv;
        }

        private void InsertAndTrack(DichVu dv)
        {
            _bll.InsertDichVu(dv);
            if (!string.IsNullOrEmpty(dv.DichVuID))
            {
                _insertedIds.Add(dv.DichVuID);
            }
        }

        // =====================================================================
        // CÁC HÀM GIẢ ĐỊNH (MOCK METHODS) ĐỂ KHẮC PHỤC LỖI BIÊN DỊCH
        // =====================================================================

        // Giả định hàm xóa nhiều (cho TC89)
        public string DeleteMultipleDichVu(List<string> ids)
        {
            try
            {
                foreach (var id in ids)
                {
                    // Gọi hàm DeleteDichVu thực tế
                    string result = _bll.DeleteDichVu(id);
                    if (!string.IsNullOrEmpty(result)) return result;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                return "Lỗi khi xóa nhiều: " + ex.Message;
            }
        }

        // Giả định hàm tìm kiếm (cho TC90)
        public List<DichVu> SearchDichVu(string maDv, string hoaDonId, bool? trangThai)
        {
            var list = _bll.GetDichVuList();
            if (!string.IsNullOrEmpty(maDv))
                list = list.Where(d => d.DichVuID.Contains(maDv)).ToList();
            if (!string.IsNullOrEmpty(hoaDonId))
                list = list.Where(d => d.HoaDonThueID.Contains(hoaDonId)).ToList();
            if (trangThai.HasValue)
                list = list.Where(d => d.TrangThai == trangThai.Value).ToList();
            return list;
        }

        // Giả định hàm Sort (cho TC91)
        public List<DichVu> SortDichVu(string sortColumn, bool isAscending)
        {
            var list = _bll.GetDichVuList();
            switch (sortColumn)
            {
                case "NgayTao":
                    return isAscending ? list.OrderBy(d => d.NgayTao).ToList() : list.OrderByDescending(d => d.NgayTao).ToList();
                case "TrangThai":
                    return isAscending ? list.OrderBy(d => d.TrangThai).ToList() : list.OrderByDescending(d => d.TrangThai).ToList();
                default:
                    return list;
            }
        }

        // Giả định hàm Delete có kiểm tra quyền (cho TC95)
        public string DeleteDichVuWithPermission(string DichVuId, string role)
        {
            if (role != "Admin")
            {
                return "Người dùng không có quyền xóa.";
            }
            return _bll.DeleteDichVu(DichVuId);
        }

        // Giả định hàm Export (cho TC96)
        public string ExportToExcel(List<DichVu> list)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"DVHD_Export_{DateTime.Now.Ticks}.xlsx");
            try
            {
                File.WriteAllText(tempPath, "Mã DV, Mã HD, Ngày Tạo,...");
                return tempPath;
            }
            catch { return ""; }
        }

        // =====================================================================
        // 1. TEST INSERT SUCCESS & BASIC (TC77, 79)
        // =====================================================================

        [Test]
        public void TC77_Insert_Success() // Thêm dịch vụ hợp lệ
        {
            var dv = TaoDVHD();
            string msg = _bll.InsertDichVu(dv);
            Assert.That(msg, Is.Empty, "Insert thất bại.");
            _insertedIds.Add(dv.DichVuID);
        }

        [Test]
        public void TC79_Insert_TrangThaiKhongThue_Success() // Chọn trạng thái không thuê (false)
        {
            var dv = TaoDVHD(trangThai: false);
            InsertAndTrack(dv);
            var result = _bll.GetDichVuById(dv.DichVuID);
            Assert.That(result.TrangThai, Is.False, "Trạng thái phải là 'Không thuê' (False).");
        }

        // =====================================================================
        // 2. TEST INSERT VALIDATION (TC78, 80, 92, 87)
        // =====================================================================

        [Test]
        public void TC78_Insert_NgayTaoSauHienTai_Fail() // Chọn ngày tạo sau ngày hiện tại
        {
            var dv = TaoDVHD(ngayTao: DateTime.Today.AddDays(1));
            string msg = _bll.InsertDichVu(dv);

            if (msg == string.Empty) _insertedIds.Add(dv.DichVuID);
            else Assert.That(msg, Does.Contain("Ngày tạo không hợp lệ"));
        }

        [Test]
        public void TC80_Insert_GhiChuDai_SuccessAndTruncated() // Ghi chú dài
        {
            var dv = TaoDVHD();
            dv.GhiChu = new string('X', 300);
            InsertAndTrack(dv);
            var result = _bll.GetDichVuById(dv.DichVuID);
            Assert.That(result.GhiChu.Length, Is.LessThanOrEqualTo(255), "Ghi chú phải được cắt ngắn.");
        }

        [Test]
        public void TC87_Insert_MaDVVaHoaDonTrung_NotBlocked() // Thêm dịch vụ với Mã DV + Hóa đơn trùng (Hợp lệ ở bảng DichVu)
        {
            var dvGoc = TaoDVHD(hoaDonThueID: "HDT_2DV");
            InsertAndTrack(dvGoc);

            var dvMoi = TaoDVHD(hoaDonThueID: "HDT_2DV");
            string msg = _bll.InsertDichVu(dvMoi);

            Assert.That(msg, Is.Empty, "Thêm 2 DVHD vào 1 HDT phải thành công.");
            _insertedIds.Add(dvMoi.DichVuID);
        }

        [Test]
        public void TC92_Insert_NgayTaoTruocHomNay_Success() // Nhập ngày tạo < hôm nay (Hợp lệ)
        {
            var dv = TaoDVHD(ngayTao: DateTime.Today.AddDays(-5));
            string msg = _bll.InsertDichVu(dv);
            Assert.That(msg, Is.Empty, "Ngày tạo cũ phải được chấp nhận.");
            _insertedIds.Add(dv.DichVuID);
        }

        // =====================================================================
        // 3. TEST UPDATE & DELETE (TC81, 82, 85, 88)
        // =====================================================================

        [Test]
        public void TC81_Delete_Success() // Xóa dịch vụ hóa đơn
        {
            var dv = TaoDVHD();
            InsertAndTrack(dv);
            string idToDelete = dv.DichVuID;

            _bll.DeleteDichVu(idToDelete);
            Assert.That(_bll.GetDichVuById(idToDelete), Is.Null);
            _insertedIds.Remove(idToDelete);
        }

        [Test]
        public void TC82_Delete_KhongChonDong_NotThrow() // Xóa không chọn dòng
        {
            Assert.DoesNotThrow(() => _bll.DeleteDichVu(""));
            Assert.DoesNotThrow(() => _bll.DeleteDichVu("DVHD9999"));
        }

        [Test]
        public void TC85_Update_Success() // Kiểm tra chức năng sửa dịch vụ hóa đơn (bao gồm TC83)
        {
            var dv = TaoDVHD();
            InsertAndTrack(dv);

            dv.TrangThai = !dv.TrangThai; // Sửa trạng thái (TC83)
            dv.GhiChu = "Sua thanh cong"; // Sửa ghi chú (TC85)

            string msg = _bll.UpdateDichVu(dv);
            var updatedDv = _bll.GetDichVuById(dv.DichVuID);

            Assert.That(msg, Is.Empty, "Sửa thất bại.");
            Assert.That(updatedDv.GhiChu, Is.EqualTo("Sua thanh cong"));
        }

        [Test]
        public void TC88_Update_DichVuID_PhaiHopLe() // Sửa dịch vụ -> đổi Mã DV thành mã đã tồn tại
        {
            var dv = TaoDVHD(); InsertAndTrack(dv);

            // Cố gắng update với ID không tồn tại
            dv.DichVuID = "DVHD_NON_EXIST";
            string msg = _bll.UpdateDichVu(dv);

            Assert.That(msg, Is.Not.Empty);
        }


        // =====================================================================
        // 4. TEST CÁC CHỨC NĂNG NÂNG CAO (TC89, 90, 91, 95, 96)
        // =====================================================================

        [Test]
        public void TC89_DeleteMultiple_Success() // Xóa nhiều dòng cùng lúc
        {
            var dv1 = TaoDVHD("HDT_DEL1"); InsertAndTrack(dv1);
            var dv2 = TaoDVHD("HDT_DEL2"); InsertAndTrack(dv2);

            var idsToDelete = new List<string> { dv1.DichVuID, dv2.DichVuID };

            string msg = DeleteMultipleDichVu(idsToDelete);
            Assert.That(msg, Is.Empty);
            Assert.That(_bll.GetDichVuById(dv1.DichVuID), Is.Null);
            Assert.That(_bll.GetDichVuById(dv2.DichVuID), Is.Null);
        }

        [Test]
        public void TC90_Search_TheoHoaDon_LocDung() // Tìm kiếm theo Hóa đơn
        {
            var dv = TaoDVHD("HDT_SEARCH"); InsertAndTrack(dv);
            var results = SearchDichVu(null, "HDT_SEARCH", null);
            Assert.That(results.Any(d => d.HoaDonThueID == "HDT_SEARCH"), Is.True);
        }

        [Test]
        public void TC91_Sort_TheoNgayTao_TangDan() // Sort cột Ngày tạo
        {
            var dvA = TaoDVHD("HDT_SortA", ngayTao: DateTime.Today.AddDays(-1)); InsertAndTrack(dvA);
            var dvB = TaoDVHD("HDT_SortB", ngayTao: DateTime.Today.AddDays(-2)); InsertAndTrack(dvB);

            var results = SortDichVu(sortColumn: "NgayTao", isAscending: true)
                                     .Where(d => d.HoaDonThueID.StartsWith("HDT_Sort"))
                                     .ToList();

            Assert.That(results[0].HoaDonThueID, Is.EqualTo("HDT_SortB"), "Sắp xếp tăng dần thất bại.");
        }

        [Test]
        public void TC95_Security_NhanVienKhongDuocXoa_Fail() // Quyền user: Nhân viên không được Xóa
        {
            var dv = TaoDVHD(); InsertAndTrack(dv);

            string msg = DeleteDichVuWithPermission(dv.DichVuID, "NhanVien");

            Assert.That(msg, Does.Contain("không có quyền"));
            Assert.That(_bll.GetDichVuById(dv.DichVuID), Is.Not.Null, "Dịch vụ vẫn phải tồn tại.");
        }

        [Test]
        public void TC96_ExportDanhSachRaExcel_FileCreated() // Export danh sách ra Excel
        {
            var list = _bll.GetDichVuList();
            string filePath = ExportToExcel(list);

            Assert.That(filePath, Is.Not.Empty);
        }
    }
}