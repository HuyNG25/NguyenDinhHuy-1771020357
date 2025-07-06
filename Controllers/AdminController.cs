using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenDinhHuy.Models;

namespace NguyenDinhHuy.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // === Trang chính cho Admin ===
        public IActionResult Dashboard()
        {
            return View();
        }

        // === 1. Danh sách đơn xin nghỉ phép ===
        public IActionResult LeaveRequests()
        {
            var requests = _context.LeaveRequests
                .Include(r => r.User)
                .OrderByDescending(r => r.RequestDate)
                .ToList();

            return View(requests);
        }

        [HttpGet]
        public IActionResult LeaveRequestsToReview()
        {
            var role = HttpContext.Session.GetString("Role");
            if (string.IsNullOrEmpty(role) || role != "Admin")
            {
                return RedirectToAction("Login", "Account");
            }

            var pendingRequests = _context.LeaveRequests
                .Where(l => l.Status == "Chờ duyệt")
                .ToList();

            return View(pendingRequests);
        }

        public IActionResult Approve(int id)
        {
            var request = _context.LeaveRequests.FirstOrDefault(r => r.Id == id);
            if (request != null && request.Status == "Chờ duyệt")
            {
                var balance = _context.LeaveBalances.FirstOrDefault(b => b.UserId == request.UserId && b.Year == DateTime.Now.Year);
                if (balance == null)
                {
                    balance = new LeaveBalance
                    {
                        UserId = request.UserId,
                        Year = DateTime.Now.Year,
                        UsedDays = 0
                    };
                    _context.LeaveBalances.Add(balance);
                    _context.SaveChanges();
                }

                if (balance.UsedDays + request.TotalDays > 24)
                {
                    TempData["Error"] = $"Không thể duyệt đơn. Người dùng đã vượt quá số ngày phép cho phép trong năm.";
                    return RedirectToAction("LeaveRequests");
                }

                request.Status = "Approved";
                balance.UsedDays += request.TotalDays;

                _context.Notifications.Add(new Notification
                {
                    UserId = request.UserId,
                    Message = $"Đơn xin nghỉ từ {request.FromDate:dd/MM/yyyy} đến {request.ToDate:dd/MM/yyyy} đã được duyệt.",
                    IsRead = false,
                    SentDate = DateTime.Now
                });

                _context.SaveChanges();
            }

            return RedirectToAction("LeaveRequests");
        }

        public IActionResult Reject(int id)
        {
            var request = _context.LeaveRequests.FirstOrDefault(r => r.Id == id);
            if (request != null && request.Status == "Chờ duyệt")
            {
                request.Status = "Rejected";

                _context.Notifications.Add(new Notification
                {
                    UserId = request.UserId,
                    Message = $"Đơn xin nghỉ từ {request.FromDate:dd/MM/yyyy} đến {request.ToDate:dd/MM/yyyy} đã bị từ chối.",
                    IsRead = false,
                    SentDate = DateTime.Now
                });

                _context.SaveChanges();
            }

            return RedirectToAction("LeaveRequests");
        }

        // === 2. Quản lý tài khoản nhân viên ===
        public IActionResult UserAccounts()
        {
            var users = _context.Users.Where(u => u.Role == "Employee").ToList();
            return View(users);
        }

        public IActionResult Activate(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                user.IsActive = true;
                _context.SaveChanges();
            }
            return RedirectToAction("UserAccounts");
        }

        // === 3. Quản lý số dư ngày phép ===
        public IActionResult LeaveBalances()
        {
            var balances = _context.LeaveBalances.Include(b => b.User).ToList();
            return View(balances);
        }

        public IActionResult SendWarning(int id)
        {
            var balance = _context.LeaveBalances.Include(b => b.User).FirstOrDefault(b => b.Id == id);
            if (balance != null && balance.UsedDays > 12)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = balance.UserId,
                    Message = $"Cảnh báo: Bạn đã sử dụng {balance.UsedDays} ngày nghỉ trong năm nay.",
                    IsRead = false,
                    SentDate = DateTime.Now
                });

                _context.SaveChanges();
            }

            return RedirectToAction("LeaveBalances");
        }

        // === 4. Đăng xuất ===
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }
    }
}

