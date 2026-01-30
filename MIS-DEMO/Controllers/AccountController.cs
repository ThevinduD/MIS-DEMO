using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;
using System;

namespace MIS_DEMO.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("Username") != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            if (HttpContext.Session.GetString("Username") != null)
            {
                return RedirectToAction("Index", "Home");
            }

            var user = _context.USERS
                .AsNoTracking()
                .FirstOrDefault(u =>
                    u.UserName == username &&
                    u.Password == password);

            if (user == null)
            {
                ViewBag.Error = "Invalid username or password";
                return View();
            }

            var repMap = _context.WKF_USER_REP_MAP
                .AsNoTracking()
                .FirstOrDefault(r => r.UserName == user.UserName);

            HttpContext.Session.SetString("Username", user.UserName);
            HttpContext.Session.SetString("RealName", user.Description);

            HttpContext.Session.SetString("UserType", repMap?.Type ?? "");
            HttpContext.Session.SetString("SalesRepCode", repMap?.SalesRepCode ?? "");
            HttpContext.Session.SetString("TeamCode", repMap?.TeamCode ?? "");

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
