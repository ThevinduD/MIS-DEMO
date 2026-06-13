using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;
using MIS_DEMO.Models.ViewModels;
using MIS_DEMO.Models;
using MIS_DEMO.Services;
using Microsoft.AspNetCore.Http; // Added for Session

namespace MIS_DEMO.Controllers
{
    public class StockController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IDateProvider _dateProvider;

        public StockController(AppDbContext context, IDateProvider dateProvider)
        {
            _context = context;
            _dateProvider = dateProvider;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult TeamDetails(string teamCode, string teamName)
        {

            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var userTeamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return RedirectToAction("Login", "Account");

            var stockQuery = _context.VW_STOCK_TEAM_VALUE
                .AsNoTracking()
                .Where(x => x.StockQty > 0 &&
                           (x.TeamName == teamName))
                .AsQueryable();

            // ----- ROLE FILTERING -----
            if (userType == "REP")
            {
                var allowedSupCodes = from ra in _context.WKF_MAP_REP_ASM
                                      join sa in _context.SUPPLIER_ASM on ra.UserName equals sa.ASMCODE
                                      where ra.SalesRepCode == salesRepCode
                                      select sa.SUPCODE;

                stockQuery = stockQuery.Where(x => allowedSupCodes.Contains(x.SupCode) && x.TeamCode != "L002");
            }
            else if (userType == "ASM" || userType == "SM" || userType == "OTHER")
            {
                var supCodes = _context.SUPPLIER_ASM
                    .Where(x => x.ASMCODE == userName)
                    .Select(x => x.SUPCODE);

                stockQuery = stockQuery.Where(x => supCodes.Contains(x.SupCode) && x.TeamCode != "L002");
            }
            else if (userType == "DIRECTOR" && userTeamCode != "L006")
            {
                var teamCodes = _context.DIR_TEAM_MAP
                    .Where(x => x.UserNameDir == userName)
                    .Select(x => x.TeamCode);

                stockQuery = stockQuery.Where(x => teamCodes.Contains(x.TeamCode));
            }

            // 3. Project to ViewModel
            var lines = stockQuery
                .Select(x => new StockDetailLine
                {
                    ItemID = x.ItemID,
                    BatchNo = x.BatchNo,
                    Description = x.Description,
                    SupName = x.SupName,
                    StockQty = x.StockQty,
                    CostPrice = x.CostPrice,
                    StockValue = x.StockValue ?? 0,
                    ExpiryDate = x.ExpiryDate,
                    ShipmentDate = x.ShipmentDate
                    
                })
                .OrderByDescending(x => x.StockValue)
                .ToList();

            var vm = new TeamStockDetailsViewModel
            {
                // Use the cleaned URL parameters, fallback to database if they somehow got lost
                TeamCode = teamCode,
                TeamName = teamName,
                StockLines = lines,
                TotalStockQty = lines.Sum(x => x.StockQty),
                TotalStockValue = lines.Sum(x => x.StockValue)
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult ItemDetails()
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Login", "Account");

            // 1. Get Base Stock
            var stockQuery = _context.VW_STOCK_TEAM_VALUE.AsNoTracking().Where(x => x.StockQty > 0);

            // ==========================================
            // ---> THE NEW LOGIC <---
            // If the user is NOT a Director, force the CostPrice > 0 rule
            // ==========================================
            if (userType != "DIRECTOR")
            {
                stockQuery = stockQuery.Where(x => x.CostPrice > 0);
            }

            // Apply standard Security Filters
            stockQuery = ApplyStockRoleFilter(stockQuery, userName, userType, salesRepCode, teamCode);

            // 2. Group by Item, Sum Quantity, and Sort (NO .Take(10) limit!)
            var allItems = stockQuery
                .GroupBy(x => new { x.ItemID, x.Description })
                .Select(g => new TopStockItemRow
                {
                    ItemName = g.Key.Description ?? "UNKNOWN ITEM",
                    TotalQuantity = g.Sum(x => (decimal?)x.StockQty) ?? 0
                })
                .OrderByDescending(x => x.TotalQuantity)
                .ToList();

            var vm = new TopStockItemsKpiAjaxModel
            {
                TopItems = allItems
            };

            return View(vm);
        }

        private IQueryable<StockTeamValue> ApplyStockRoleFilter(IQueryable<StockTeamValue> query, string userName, string userType, string salesRepCode, string teamCode)
        {
            if (userType == "REP")
            {
                var allowedSupCodes = from ra in _context.WKF_MAP_REP_ASM
                                      join sa in _context.SUPPLIER_ASM on ra.UserName equals sa.ASMCODE
                                      where ra.SalesRepCode == salesRepCode
                                      select sa.SUPCODE;
                return query.Where(x => allowedSupCodes.Contains(x.SupCode) && x.TeamCode != "L002");
            }
            if (userType == "ASM" || userType == "SM" || userType == "OTHER")
            {
                var supCodes = _context.SUPPLIER_ASM.Where(x => x.ASMCODE == userName).Select(x => x.SUPCODE);
                return query.Where(x => supCodes.Contains(x.SupCode) && x.TeamCode != "L002");
            }
            if (userType == "DIRECTOR" && teamCode != "L006")
            {
                var teamCodes = _context.DIR_TEAM_MAP.Where(x => x.UserNameDir == userName).Select(x => x.TeamCode);
                return query.Where(x => teamCodes.Contains(x.TeamCode));
            }
            return query;
        }


    }
}