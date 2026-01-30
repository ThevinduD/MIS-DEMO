using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;
using MIS_DEMO.Models;
using MIS_DEMO.Models.ViewModels;
using System.Diagnostics;

namespace MIS_DEMO.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("Username") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            var model = new DashboardViewModel();

            var today = new DateTime(2025, 11, 20);
            //var today = DateTime.Today;

            var dayStart = today;
            var dayEnd = today.AddDays(1);

            if (userType == "REP")
            {
                model.TodayTotalSales = _context.VW_SALES_FACT
                    .AsNoTracking()
                    .Where(x =>
                        x.SalesRepCode == salesRepCode &&
                        x.RefDate >= dayStart &&
                        x.RefDate < dayEnd)
                    .Sum(x => (decimal?)x.LineTotal) ?? 0;
            }

            else if (userType == "DIRECTOR")
            {
                // Director special case: L006 sees everything
                if (teamCode == "L006")
                {
                    model.TodayTotalSales = _context.VW_SALES_FACT
                        .AsNoTracking()
                        .Where(x => x.RefDate >= dayStart
                                    && x.RefDate < dayEnd)
                        .Sum(x => (decimal?)x.LineTotal) ?? 0;
                }
                else
                {
                    // Get team codes mapped to this Director
                    var teamCodes = _context.DIR_TEAM_MAP
                        .AsNoTracking()
                        .Where(x => x.UserNameDir == userName)
                        .Select(x => x.TeamCode)
                        .ToList();

                    // Optional: include his own TeamCode too (in case mapping table misses it)
                    if (!string.IsNullOrEmpty(teamCode) && !teamCodes.Contains(teamCode))
                        teamCodes.Add(teamCode);

                    if (!teamCodes.Any())
                    {
                        model.TodayTotalSales = 0;
                    }
                    else
                    {
                        // Filter by Pat_Name (contains teamcode as you said)
                        model.TodayTotalSales = _context.VW_SALES_FACT
                            .AsNoTracking()
                            .Where(x => teamCodes.Contains(x.Pat_Name)
                                        && x.RefDate >= dayStart
                                        && x.RefDate < dayEnd)
                            .Sum(x => (decimal?)x.LineTotal) ?? 0;
                    }
                }
            }

            else if (userType == "ASM")
            {
                // Step 1: Check if user is actually an SM
                var isSM = _context.WKF_MAP_SM_ASM
                    .AsNoTracking()
                    .Any(x => x.UserNameSM == userName);

                if (isSM)
                {
                    // SM logic
                    // 1. Get all ASMs assigned to this SM
                    var assignedASMs = _context.WKF_MAP_SM_ASM
                        .AsNoTracking()
                        .Where(x => x.UserNameSM == userName)
                        .Select(x => x.UserNameASM)
                        .ToList();

                    // Get ASM rep codes
                    var asmRepCodes = _context.WKF_USER_REP_MAP
                        .AsNoTracking()
                        .Where(x => assignedASMs.Contains(x.UserName))
                        .Select(x => x.SalesRepCode)
                        .ToList();

                    // 2. Get all REP codes under these ASMs
                    var repRepCodes = _context.WKF_MAP_REP_ASM
                        .AsNoTracking()
                        .Where(x => assignedASMs.Contains(x.UserName))
                        .Select(x => x.SalesRepCode)
                        .ToList();

                    var allRepCodes = asmRepCodes
                        .Union(repRepCodes)
                        .ToList();

                    if (!string.IsNullOrEmpty(salesRepCode))
                    {
                        allRepCodes.Add(salesRepCode);
                    }

                    // 4. Sum all sales
                    model.TodayTotalSales = _context.VW_SALES_FACT
                        .AsNoTracking()
                        .Where(
                            x => allRepCodes.Contains(x.SalesRepCode) &&
                            x.RefDate >= today &&
                            x.RefDate < today.AddDays(1))
                        .Sum(x => (decimal?)x.LineTotal) ?? 0;
                }
                else
                {
                    // ASM logic (existing)
                    var repCodes = _context.WKF_MAP_REP_ASM
                        .AsNoTracking()
                        .Where(x => x.UserName == userName)
                        .Select(x => x.SalesRepCode)
                        .ToList();

                    // Add ASM's own salesRepCode
                    if (!string.IsNullOrEmpty(salesRepCode) && !repCodes.Contains(salesRepCode))
                    {
                        repCodes.Add(salesRepCode);
                    }

                    // Sum sales for ASM + assigned REPs
                    model.TodayTotalSales = _context.VW_SALES_FACT
                        .AsNoTracking()
                        .Where(
                            x => repCodes.Contains(x.SalesRepCode) &&
                            x.RefDate >= today &&
                            x.RefDate < today.AddDays(1))
                        .Sum(x => (decimal?)x.LineTotal) ?? 0;
                }

            }

            return View(model);
        }

    }
}
