using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MIS_DEMO.Data;
using MIS_DEMO.Models;
using MIS_DEMO.Models.ViewModels;
using MIS_DEMO.Services;
using System.Globalization;


namespace MIS_DEMO.Controllers
{
    public class SalesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SalesAccessService _salesAccessService;
        private readonly IMemoryCache _cache;


        public SalesController(
            AppDbContext context,
            SalesAccessService salesAccessService,
            IMemoryCache cache)
        {
            _context = context;
            _salesAccessService = salesAccessService;
            _cache = cache;
        }

        public IActionResult Details()
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return RedirectToAction("Login", "Account");

            // DEV date
            var today = new DateTime(2025, 11, 20);
            var yesterday = today.AddDays(-1);
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = thisMonthStart.AddMonths(1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);

            //  cache key (no need teamCode now, since access is resolved into repCodes)
            var cacheKey = $"salesSummary:{userName}:{userType}:{salesRepCode}:{today:yyyyMMdd}";
            if (_cache.TryGetValue(cacheKey, out SalesSummaryViewModel cachedModel))
                return View(cachedModel);

            //  1) Get access rep codes for ALL types (REP/ASM/SM/Director)
            var repCodes = _salesAccessService.GetAccessibleRepCodes(userType, userName, salesRepCode);

            //  2) Detect "L006 director = ALL"
            var isAll = repCodes.Count == 1 && repCodes[0] == "__ALL__";

            //  3) Build base queries
            var salesQuery = _context.VW_SALES_FACT
                .AsNoTracking()
                .Where(x => x.LineTotal > 0);

            var returnQuery = _context.VW_SALES_RETURN_FACT
                .AsNoTracking()
                .Where(x => x.LineTotal > 0);

            //  4) Apply rep filter only when NOT ALL
            if (!isAll)
            {
                if (!repCodes.Any())
                    return View(new SalesSummaryViewModel());

                salesQuery = salesQuery.Where(x => repCodes.Contains(x.SalesRepCode));
                returnQuery = returnQuery.Where(x => repCodes.Contains(x.SalesRepCode));
            }

            //  5) Calculate totals
            var model = new SalesSummaryViewModel
            {
                TodaySales = salesQuery
                    .Where(x => x.RefDate >= today && x.RefDate < today.AddDays(1))
                    .Sum(x => (decimal?)x.LineTotal) ?? 0,

                YesterdaySales = salesQuery
                    .Where(x => x.RefDate >= yesterday && x.RefDate < today)
                    .Sum(x => (decimal?)x.LineTotal) ?? 0,

                ThisMonthSales = salesQuery
                    .Where(x => x.RefDate >= thisMonthStart && x.RefDate < nextMonthStart)
                    .Sum(x => (decimal?)x.LineTotal) ?? 0,

                LastMonthSales = salesQuery
                    .Where(x => x.RefDate >= lastMonthStart && x.RefDate < thisMonthStart)
                    .Sum(x => (decimal?)x.LineTotal) ?? 0,

                TodayReturns = returnQuery
                    .Where(x => x.RefDate >= today && x.RefDate < today.AddDays(1))
                    .Sum(x => (decimal?)x.LineTotal) ?? 0,

                YesterdayReturns = returnQuery
                    .Where(x => x.RefDate >= yesterday && x.RefDate < today)
                    .Sum(x => (decimal?)x.LineTotal) ?? 0,

                ThisMonthReturns = returnQuery
                    .Where(x => x.RefDate >= thisMonthStart && x.RefDate < nextMonthStart)
                    .Sum(x => (decimal?)x.LineTotal) ?? 0,

                LastMonthReturns = returnQuery
                    .Where(x => x.RefDate >= lastMonthStart && x.RefDate < thisMonthStart)
                    .Sum(x => (decimal?)x.LineTotal) ?? 0
            };

            _cache.Set(cacheKey, model, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            });

            return View(model);
        }



        public IActionResult PeriodDetails(string period, string? from, string? to)
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return RedirectToAction("Login", "Account");

            var repCodes = _salesAccessService.GetAccessibleRepCodes(userType, userName, salesRepCode);
            if (!repCodes.Any())
                return View("TodayDetails", new TodaySalesDetailsViewModel());

            // Dev date (replace with DateTime.Today later)
            var baseToday = new DateTime(2025, 11, 20);

            DateTime startDate;
            DateTime endDateExclusive;
            string title;

            var p = (period ?? "").ToLower();

            if (p == "select")
            {
                // If user hasn't selected dates yet, show a default range (example: last 7 days)
                if (!DateTime.TryParse(from, out var fromDt) || !DateTime.TryParse(to, out var toDt))
                {
                    startDate = baseToday.AddDays(-6).Date;
                    endDateExclusive = baseToday.AddDays(1).Date; // inclusive UX
                    title = "Custom Range Details";
                }
                else
                {
                    // inclusive end-date UX: to=2025-11-20 means include the full day
                    startDate = fromDt.Date;
                    endDateExclusive = toDt.Date.AddDays(1);
                    title = "Custom Range Details";
                }
            }
            else
            {
                switch (p)
                {
                    case "yesterday":
                        startDate = baseToday.AddDays(-1);
                        endDateExclusive = baseToday;
                        title = "Yesterday Details";
                        break;

                    case "thismonth":
                        startDate = new DateTime(baseToday.Year, baseToday.Month, 1);
                        endDateExclusive = startDate.AddMonths(1);
                        title = "This Month Details";
                        break;

                    case "lastmonth":
                        var thisMonthStart = new DateTime(baseToday.Year, baseToday.Month, 1);
                        startDate = thisMonthStart.AddMonths(-1);
                        endDateExclusive = thisMonthStart;
                        title = "Last Month Details";
                        break;

                    case "today":
                    default:
                        startDate = baseToday;
                        endDateExclusive = baseToday.AddDays(1);
                        title = "Today Details";
                        break;
                }
            }

            var model = new TodaySalesDetailsViewModel
            {
                Date = startDate,
                FromDate = startDate,
                ToDate = endDateExclusive.AddDays(-1) // for UI display
            };

            // SALES
            var salesQuery = _context.VW_SALES_FACT
                .AsNoTracking()
                .Where(x => repCodes.Contains(x.SalesRepCode)
                            && x.RefDate >= startDate
                            && x.RefDate < endDateExclusive
                            && x.LineTotal > 0);

            model.SalesTotal = salesQuery.Sum(x => (decimal?)x.LineTotal) ?? 0;

            model.SalesLines = salesQuery
                .Select(x => new SalesLineViewModel
                {
                    RefDate = x.RefDate,
                    CusCode = x.CusCode,
                    CusName = x.CusName,
                    InvoDocNo = x.InvoDocNo,
                    ItemCode = x.ItemCode,
                    Description = x.ItemDescription,
                    Qty = x.Qty,
                    SoldPrice = x.SoldPrice,
                    LineTotal = x.LineTotal,
                    SupName = x.SupName,
                    SalesRepCode = x.SalesRepCode,
                    SalesRepName = x.SalesRepName
                })
                .OrderByDescending(x => x.LineTotal)
                .Take(300)
                .ToList();

            // RETURNS
            var returnQuery = _context.VW_SALES_RETURN_FACT
                .AsNoTracking()
                .Where(x => repCodes.Contains(x.SalesRepCode)
                            && x.RefDate >= startDate
                            && x.RefDate < endDateExclusive
                            && x.LineTotal > 0);

            model.ReturnTotal = returnQuery.Sum(x => (decimal?)x.LineTotal) ?? 0;

            model.ReturnLines = returnQuery
                .Select(x => new ReturnLineViewModel
                {
                    RefDate = x.RefDate,
                    CusCode = x.CusCode,
                    CusName = x.CusName,
                    RtnDocNo = x.RtnDocNo,
                    InvoDocNo = x.InvoDocNo,
                    ItemCode = x.ItemCode,
                    Description = x.Description,
                    Qty = x.Qty,
                    ReturnedPrice = x.ReturnedPrice,
                    LineTotal = x.LineTotal,
                    SupName = x.SupName,
                    SalesRepCode = x.SalesRepCode,
                    SalesRepName = x.SalesRepName
                })
                .OrderByDescending(x => x.LineTotal)
                .Take(300)
                .ToList();

            ViewBag.PeriodTitle = title;
            ViewBag.PeriodRange = $"{startDate:yyyy-MM-dd} → {endDateExclusive.AddDays(-1):yyyy-MM-dd}";
            ViewBag.Period = p; // so cshtml can show range form only for select

            return View("TodayDetails", model);
        }


        [HttpGet]
        public IActionResult ProductWiseData(string? period, string? from, string? to, string metric = "value")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return Unauthorized();

            var cacheKey = $"pie:{userName}:{userType}:{salesRepCode}:{period}:{from}:{to}:{metric}";

            if (_cache.TryGetValue(cacheKey, out PieChartDataVm cached))
            {
                return Json(cached);
            }

            var repCodes = _salesAccessService.GetAccessibleRepCodes(userType, userName, salesRepCode);
            if (!repCodes.Any())
                return Json(new PieChartDataVm());

            // DEV base date (change to DateTime.Today later)
            var baseToday = new DateTime(2025, 11, 20);

            DateTime startDate;
            DateTime endDateExclusive;

            // If "from/to" provided => use range mode
            bool hasFrom = DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fromDate);

            bool hasTo = DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var toDate);

            if (hasFrom && hasTo)
            {
                startDate = fromDate.Date;
                endDateExclusive = toDate.Date.AddDays(1); // inclusive end-date UX
            }
            else
            {
                // Otherwise use "period"
                switch ((period ?? "today").ToLower())
                {
                    case "yesterday":
                        startDate = baseToday.AddDays(-1);
                        endDateExclusive = baseToday;
                        break;

                    case "thismonth":
                        startDate = new DateTime(baseToday.Year, baseToday.Month, 1);
                        endDateExclusive = startDate.AddMonths(1);
                        break;

                    case "lastmonth":
                        var thisMonthStart = new DateTime(baseToday.Year, baseToday.Month, 1);
                        startDate = thisMonthStart.AddMonths(-1);
                        endDateExclusive = thisMonthStart;
                        break;

                    case "today":
                    default:
                        startDate = baseToday;
                        endDateExclusive = baseToday.AddDays(1);
                        break;
                }
            }

            var query = _context.VW_SALES_FACT
                .AsNoTracking()
                .Where(x => repCodes.Contains(x.SalesRepCode)
                            && x.RefDate >= startDate
                            && x.RefDate < endDateExclusive
                            && x.LineTotal > 0
                            && !string.IsNullOrEmpty(x.ItemDescription));

            var grouped = query
                .GroupBy(x => x.ItemDescription)
                .Select(g => new
                {
                    Label = g.Key,
                    Value = metric == "qty"
                        ? (decimal)(g.Sum(x => (decimal?)x.Qty) ?? 0)
                        : (g.Sum(x => (decimal?)x.LineTotal) ?? 0)
                })
                .ToList();

            var grandTotal = grouped.Sum(x => x.Value);

            var top10 = grouped
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList();

            var top10Total = top10.Sum(x => x.Value);
            var othersTotal = grandTotal - top10Total;

            var labels = top10.Select(x => x.Label).ToList();
            var values = top10.Select(x => x.Value).ToList();

            if (othersTotal > 0)
            {
                labels.Add("Others");
                values.Add(othersTotal);
            }

            var result = new PieChartDataVm
            {
                Labels = labels,
                Values = values
            };

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            });

            return Json(result);
        }


    }
}
