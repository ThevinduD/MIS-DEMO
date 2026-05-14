using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;
using MIS_DEMO.Models;
using MIS_DEMO.Models.ViewModels;
using MIS_DEMO.Services;
using System.Diagnostics;

namespace MIS_DEMO.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IDateProvider _dateProvider;

        public HomeController(AppDbContext context, IDateProvider dateProvider)
        {
            _context = context;
            _dateProvider = dateProvider;
        }

        // ==========================================
        // MAIN DASHBOARD LOAD
        // ==========================================
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("Username") == null)
                return RedirectToAction("Login", "Account");

            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            var model = new DashboardViewModel();
            var today = _dateProvider.Today;
            var dayStart = today.Date;
            var dayEnd = dayStart.AddDays(1);


            // 4) STOCK KPI
            var stockQuery = _context.VW_STOCK_TEAM_VALUE.AsNoTracking().Where(x => x.StockQty > 0);
            stockQuery = ApplyStockRoleFilter(stockQuery, userName, userType, salesRepCode, teamCode);

            // 1. Get ONLY the CASH prices from the new table
            var cashPrices = _context.ITEM_PRICE.AsNoTracking().Where(p => p.Type_Code == "CASH");

            // 2. Join the stock view to the cash prices and group by team
            var teamStock = (from s in stockQuery
                             join p in cashPrices on s.ItemRefNo equals p.ItemRefNo into pJoin
                             from p in pJoin.DefaultIfEmpty() // Left join so we don't lose stock if a price is missing
                             group new { s, p } by new { s.TeamCode, s.TeamName } into g
                             select new TeamStockRowVm
                             {
                                 TeamName = g.Key.TeamName ?? "UNASSIGNED",

                                 // THE FIX: Instead of checking 'p != null', we cast S_Price to nullable and use ?? 0m
                                 StockValue = g.Sum(x =>
                                     (decimal?)x.s.StockQty * (((decimal?)x.p.S_Price) ?? 0m)
                                 ) ?? 0,

                                 // THE FIX: Same here for the Expiring Soon calculation
                                 ExpiringSoonValue = g.Where(x => x.s.ExpiryDays >= 0 && x.s.ExpiryDays <= 60)
                                                      .Sum(x =>
                                                          (decimal?)x.s.StockQty * (((decimal?)x.p.S_Price) ?? 0m)
                                                      ) ?? 0
                             })
                             .OrderByDescending(x => x.StockValue)
                             .ToList();

            model.TeamStockValues = teamStock;
            model.TotalStockValue = teamStock.Sum(x => x.StockValue);
            model.TotalExpiringSoonValue = teamStock.Sum(x => x.ExpiringSoonValue);

            var dashboardLinks = _context.LINKS.AsNoTracking().ToList();
            ViewBag.DashboardLinks = dashboardLinks;


            return View(model);
        }


        // ==========================================
        // AJAX KPI LOADER
        // ==========================================

        [HttpGet]
        public IActionResult LoadSalesKpi(string team = "All", DateTime? filterDate = null, string category = "All")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return Unauthorized();

            // 1. Establish Date Ranges
            var today = filterDate ?? _dateProvider.Today.Date;
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var thisMonthEnd = thisMonthStart.AddMonths(1); // <--- NEW: Get the 1st of the NEXT month
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var lastMonthEnd = thisMonthStart;

            // 2. Fetch Available Categories for the Dropdown
            var availableCategories = _context.CUS_CATEGORY.AsNoTracking()
                                              .Where(c => !string.IsNullOrEmpty(c.CusCategoryName))
                                              .Select(c => c.CusCategoryName)
                                              .Distinct()
                                              .OrderBy(c => c)
                                              .ToList();

            // 3. Pre-Fetch valid Customer Codes if a specific Category is selected
            List<string> validCusCodes = null;
            if (category != "All")
            {
                validCusCodes = (from p in _context.PartnerDetails.AsNoTracking()
                                 join c in _context.CUS_CATEGORY.AsNoTracking() on p.CusCategoryCode equals c.CusCategoryCode
                                 where c.CusCategoryName == category
                                 select p.Pcode).Distinct().ToList();
            }

            // 4. Get Available Teams
            var baseTeamQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));
            baseTeamQ = ApplySalesRoleFilter(baseTeamQ, userName, userType, salesRepCode, teamCode);
            var availableTeams = baseTeamQ.Select(x => x.LocShort).Distinct().OrderBy(x => x).ToList();

            // 5. Prepare the Base Queries 
            // ---> FIX: Changed x.RefDate <= today to x.RefDate < thisMonthEnd so it fetches the whole month!
            var salesQ = _context.VW_SALES_FACT.AsNoTracking()
                .Where(x => x.RefDate >= lastMonthStart && x.RefDate < thisMonthEnd && x.LineTotal > 0);

            var returnQ = _context.VW_SALES_RETURN_FACT.AsNoTracking()
                .Where(x => x.RefDate >= lastMonthStart && x.RefDate < thisMonthEnd && x.LineTotal > 0);

            // --- APPLY CATEGORY FILTER TO SALES & RETURNS ---
            if (validCusCodes != null)
            {
                salesQ = salesQ.Where(x => validCusCodes.Contains(x.CusCode));
                returnQ = returnQ.Where(x => validCusCodes.Contains(x.CusCode));
            }
            // -----------------------------------------------------

            // 6. Apply Security & Team Filters
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);
            returnQ = ApplySalesRoleFilter(returnQ, userName, userType, salesRepCode, teamCode);

            if (team != "All")
            {
                salesQ = salesQ.Where(x => x.LocShort == team);
                returnQ = returnQ.Where(x => x.LocShort == team);
            }

            // 7. Execute Optimized SQL Aggregation
            var groupedSales = salesQ.GroupBy(x => x.RefDate)
                                     .Select(g => new { Date = g.Key, Total = g.Sum(x => (decimal?)x.LineTotal) ?? 0 })
                                     .ToList();

            var groupedReturns = returnQ.GroupBy(x => x.RefDate)
                                        .Select(g => new { Date = g.Key, Total = g.Sum(x => (decimal?)x.LineTotal) ?? 0 })
                                        .ToList();

            // ==========================================
            // TARGET CALCULATION (With Financial Year Logic)
            // ==========================================
            var targetBaseQ = from tm in _context.TARGET_MAIN.AsNoTracking()
                              join ts in _context.TARGET_MONTHS_REP_SPECIAL.AsNoTracking() on tm.TranNo equals ts.TranNo
                              select new { tm.TeamCode, tm.FromDate, tm.ToDate, ts.MonthNo, ts.TargetActual, ts.SalesRepCode };

            if (team != "All")
            {
                targetBaseQ = targetBaseQ.Where(x => x.TeamCode == team);
            }

            int currentCalMonth = thisMonthStart.Month;
            int lastCalMonth = lastMonthStart.Month;

            int currentFinMonth = currentCalMonth >= 4 ? currentCalMonth - 3 : currentCalMonth + 9;
            int lastFinMonth = lastCalMonth >= 4 ? lastCalMonth - 3 : lastCalMonth + 9;

            decimal currentTarget = targetBaseQ
                .Where(x => x.MonthNo == currentFinMonth && x.FromDate < thisMonthEnd && x.ToDate >= thisMonthStart)
                .Sum(x => (decimal?)x.TargetActual) ?? 0;

            decimal pastTarget = targetBaseQ
                .Where(x => x.MonthNo == lastFinMonth && x.FromDate <= lastMonthEnd && x.ToDate >= lastMonthStart)
                .Sum(x => (decimal?)x.TargetActual) ?? 0;


            // 8. Map to ViewModel using C#
            var vm = new SalesKpiAjaxModel
            {
                SelectedDate = today,
                SelectedTeam = team,
                SelectedCategory = category,
                AvailableCategories = availableCategories,
                AvailableTeams = availableTeams,

                Sales = new SalesMetric
                {
                    Today = groupedSales.Where(x => x.Date == today).Sum(x => x.Total),
                    ThisMonth = groupedSales.Where(x => x.Date >= thisMonthStart).Sum(x => x.Total),
                    LastMonth = groupedSales.Where(x => x.Date >= lastMonthStart && x.Date < lastMonthEnd).Sum(x => x.Total)
                },
                Returns = new SalesMetric
                {
                    Today = groupedReturns.Where(x => x.Date == today).Sum(x => x.Total),
                    ThisMonth = groupedReturns.Where(x => x.Date >= thisMonthStart).Sum(x => x.Total),
                    LastMonth = groupedReturns.Where(x => x.Date >= lastMonthStart && x.Date < lastMonthEnd).Sum(x => x.Total)
                },

                TargetThisMonth = currentTarget,
                TargetLastMonth = pastTarget
            };

            return PartialView("_SalesKpi", vm);
        }

        [HttpGet]
        public IActionResult LoadNonDeliveredKpi(int? rangeDays = null, string team = "All", string category = "All")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return Unauthorized();

            // ==========================================
            // 1. FETCH CONFIG & AVAILABLE DAYS
            // ==========================================
            var dbConfig = _context.MIS_DEFAULT_CONFIG.AsNoTracking().FirstOrDefault();

            // NEW: Point to NonDelDefDays
            int actualRangeDays = rangeDays ?? dbConfig?.NonDelDefDays ?? 45;

            // NEW: Point to NON_DELIVERED_DAYS table
            var availableDays = _context.NON_DELIVERED_DAYS.AsNoTracking()
                                        .Select(x => x.Days)
                                        .Distinct()
                                        .OrderBy(x => x)
                                        .ToList();

            if (!availableDays.Any()) availableDays = new List<int> { 15, 30, 45, 60, 90 };
            if (!availableDays.Contains(actualRangeDays)) actualRangeDays = availableDays.Max();

            // ==========================================
            // 1.5 FETCH CATEGORIES & CUSTOMER CODES
            // ==========================================
            var availableCategories = _context.CUS_CATEGORY.AsNoTracking()
                                              .Where(c => !string.IsNullOrEmpty(c.CusCategoryName))
                                              .Select(c => c.CusCategoryName)
                                              .Distinct()
                                              .OrderBy(c => c)
                                              .ToList();

            List<string> validCusCodes = null;
            if (category != "All")
            {
                validCusCodes = (from p in _context.PartnerDetails.AsNoTracking()
                                 join c in _context.CUS_CATEGORY.AsNoTracking() on p.CusCategoryCode equals c.CusCategoryCode
                                 where c.CusCategoryName == category
                                 select p.Pcode).Distinct().ToList();
            }

            // ==========================================
            // 2. TEAM & SECURITY LOGIC
            // ==========================================
            var salesQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);

            var availableTeams = salesQ.Select(x => x.LocShort).Distinct().OrderBy(x => x).ToList();
            var invoiceTeams = salesQ.GroupBy(x => x.InvoDocNo).Select(g => new { InvoNo = g.Key, Team = g.Max(x => x.LocShort) });

            if (team != "All")
            {
                invoiceTeams = invoiceTeams.Where(x => x.Team == team);
            }

            // ==========================================
            // 3. GET NON-DELIVERED INVOICES
            // ==========================================
            var pendingQuery = _context.CUSTOMER_INVOICE_MAIN.AsNoTracking()
                .Where(x => (x.isFinalDelivery == false || x.isFinalDelivery == null) && x.InvoiceAmt != 0 && x.Cancel == false);

            // --- APPLY CATEGORY FILTER ---
            if (validCusCodes != null)
            {
                pendingQuery = pendingQuery.Where(x => validCusCodes.Contains(x.CusCode));
            }
            // -----------------------------

            pendingQuery = ApplyInvoiceRoleFilter(pendingQuery, userName, userType, salesRepCode, teamCode);

            var targetInvoices = (from p in pendingQuery
                                  join t in invoiceTeams on p.InvoDocNo equals t.InvoNo
                                  select p).ToList();

            // ==========================================
            // 4. MATH & VIEW MODEL MAPPING
            // ==========================================
            var cutoffDate = _dateProvider.Today.Date.AddDays(-actualRangeDays);

            decimal underAmt = targetInvoices.Where(x => x.RefDate > cutoffDate).Sum(x => x.InvoiceAmt);
            decimal overAmt = targetInvoices.Where(x => x.RefDate <= cutoffDate).Sum(x => x.InvoiceAmt);

            var vm = new NonDeliveredKpiViewModel
            {
                SelectedTeam = team,
                AvailableTeams = availableTeams,
                SelectedCategory = category,
                AvailableCategories = availableCategories,
                RangeDays = actualRangeDays,     // <-- Passes DB default
                AvailableDays = availableDays,   // <-- Passes DB options
                UnderAmount = underAmt,
                OverAmount = overAmt
            };

            return PartialView("_NonDeliveredKpi", vm);
        }

        [HttpGet]
        public IActionResult LoadDeliveredKpi(string team = "All", string category = "All") // <-- Added category parameter
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return Unauthorized();

            // ==========================================
            // 0. FETCH CATEGORIES & CUSTOMER CODES
            // ==========================================
            var availableCategories = _context.CUS_CATEGORY.AsNoTracking()
                                              .Where(c => !string.IsNullOrEmpty(c.CusCategoryName))
                                              .Select(c => c.CusCategoryName)
                                              .Distinct()
                                              .OrderBy(c => c)
                                              .ToList();

            List<string> validCusCodes = null;
            if (category != "All")
            {
                validCusCodes = (from p in _context.PartnerDetails.AsNoTracking()
                                 join c in _context.CUS_CATEGORY.AsNoTracking() on p.CusCategoryCode equals c.CusCategoryCode
                                 where c.CusCategoryName == category
                                 select p.Pcode).Distinct().ToList();
            }

            // ==========================================
            // 1. TEAM & SECURITY LOGIC
            // ==========================================
            var salesQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);

            var availableTeams = salesQ.Select(x => x.LocShort).Distinct().OrderBy(x => x).ToList();
            var invoiceTeams = salesQ.GroupBy(x => x.InvoDocNo).Select(g => new { InvoNo = g.Key, Team = g.Max(x => x.LocShort) });

            if (team != "All")
            {
                invoiceTeams = invoiceTeams.Where(x => x.Team == team);
            }

            // ==========================================
            // 2. DELIVERED INVOICES FOR THIS MONTH
            // ==========================================
            var today = _dateProvider.Today;
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = thisMonthStart.AddMonths(1);

            var deliveredQ = _context.CUSTOMER_INVOICE_MAIN.AsNoTracking()
                .Where(x => x.isFinalDelivery == true && x.Cancel == false && x.InvoiceAmt != 0 && x.FDeliveryDate >= thisMonthStart && x.FDeliveryDate < nextMonthStart);

            // --- APPLY CATEGORY FILTER ---
            if (validCusCodes != null)
            {
                deliveredQ = deliveredQ.Where(x => validCusCodes.Contains(x.CusCode));
            }
            // -----------------------------

            deliveredQ = ApplyInvoiceRoleFilter(deliveredQ, userName, userType, salesRepCode, teamCode);

            // Join with invoiceTeams to apply the selected team filter!
            var targetInvoices = (from d in deliveredQ
                                  join t in invoiceTeams on d.InvoDocNo equals t.InvoNo
                                  select d).ToList();

            var vm = new DeliveredKpiViewModel
            {
                SelectedTeam = team,
                AvailableTeams = availableTeams,
                SelectedCategory = category, // <-- Map Category State
                AvailableCategories = availableCategories, // <-- Map Category List
                ThisMonthDeliveredTotal = targetInvoices.Sum(x => x.InvoiceAmt)
            };

            return PartialView("_DeliveredKpi", vm);
        }

        [HttpGet]
        public IActionResult LoadTeamWiseSalesKpi(int monthOffset = 0)
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return Unauthorized();

            var referenceDate = _dateProvider.Today.AddMonths(monthOffset);
            var thisMonthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1);
            var nextMonthStart = thisMonthStart.AddMonths(1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);

            var teamSalesQuery = _context.VW_SALES_FACT.AsNoTracking().Where(x => x.LineTotal > 0);
            var teamReturnQuery = _context.VW_SALES_RETURN_FACT.AsNoTracking().Where(x => x.LineTotal > 0);

            // Using the new helper method!
            teamSalesQuery = ApplySalesRoleFilter(teamSalesQuery, userName, userType, salesRepCode, teamCode);
            teamReturnQuery = ApplySalesRoleFilter(teamReturnQuery, userName, userType, salesRepCode, teamCode);

            var thisMonthSales = teamSalesQuery.Where(x => x.RefDate >= thisMonthStart && x.RefDate < nextMonthStart && !string.IsNullOrEmpty(x.LocShort))
                .GroupBy(x => x.LocShort).Select(g => new { LocShort = g.Key, Total = g.Sum(x => (decimal?)x.LineTotal) ?? 0 }).ToList();
            var thisMonthReturns = teamReturnQuery.Where(x => x.RefDate >= thisMonthStart && x.RefDate < nextMonthStart && !string.IsNullOrEmpty(x.LocShort))
                .GroupBy(x => x.LocShort).Select(g => new { LocShort = g.Key, Total = g.Sum(x => (decimal?)x.LineTotal) ?? 0 }).ToList();
            var lastMonthSales = teamSalesQuery.Where(x => x.RefDate >= lastMonthStart && x.RefDate < thisMonthStart && !string.IsNullOrEmpty(x.LocShort))
                .GroupBy(x => x.LocShort).Select(g => new { LocShort = g.Key, Total = g.Sum(x => (decimal?)x.LineTotal) ?? 0 }).ToList();
            var lastMonthReturns = teamReturnQuery.Where(x => x.RefDate >= lastMonthStart && x.RefDate < thisMonthStart && !string.IsNullOrEmpty(x.LocShort))
                .GroupBy(x => x.LocShort).Select(g => new { LocShort = g.Key, Total = g.Sum(x => (decimal?)x.LineTotal) ?? 0 }).ToList();

            var thisSalesDict = thisMonthSales.ToDictionary(x => x.LocShort, x => x.Total);
            var thisRetDict = thisMonthReturns.ToDictionary(x => x.LocShort, x => x.Total);
            var lastSalesDict = lastMonthSales.ToDictionary(x => x.LocShort, x => x.Total);
            var lastRetDict = lastMonthReturns.ToDictionary(x => x.LocShort, x => x.Total);

            var allTeams = new HashSet<string>(thisSalesDict.Keys.Concat(thisRetDict.Keys).Concat(lastSalesDict.Keys).Concat(lastRetDict.Keys));

            var teamWiseSalesList = allTeams
                .Select(team => new TeamWiseSaleRowVm
                {
                    LocShort = team,
                    ThisMonth = (thisSalesDict.GetValueOrDefault(team, 0)) - (thisRetDict.GetValueOrDefault(team, 0)),
                    LastMonth = (lastSalesDict.GetValueOrDefault(team, 0)) - (lastRetDict.GetValueOrDefault(team, 0))
                })
                .OrderByDescending(x => x.ThisMonth)
                .ToList();

            var vm = new TeamWiseSalesKpiViewModel
            {
                TeamWiseSales = teamWiseSalesList,
                TeamWiseThisMonthTotal = teamWiseSalesList.Sum(x => x.ThisMonth),
                TeamWiseLastMonthTotal = teamWiseSalesList.Sum(x => x.LastMonth),
                CurrentMonthOffset = monthOffset,
                ThisMonthLabel = thisMonthStart.ToString("MMM yyyy"),
                LastMonthLabel = lastMonthStart.ToString("MMM yyyy")
            };

            return PartialView("_TeamWiseSalesKPI", vm);
        }

        [HttpGet]
        public IActionResult LoadTopStockItemsKpi()
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return Unauthorized();

            // 1. Get Stock and Apply Security Filters
            var stockQuery = _context.VW_STOCK_TEAM_VALUE.AsNoTracking().Where(x => x.StockQty > 0);
            stockQuery = ApplyStockRoleFilter(stockQuery, userName, userType, salesRepCode, teamCode);

            // 2. Group by ItemID & Description, Sum the QUANTITY, Sort, and Take Top 10
            var topItems = stockQuery
                .GroupBy(x => new { x.ItemID, x.Description })
                .Select(g => new TopStockItemRow
                {
                    ItemName = g.Key.Description ?? "UNKNOWN ITEM",

                    TotalQuantity = g.Sum(x => (decimal?)x.StockQty) ?? 0
                })
                .OrderByDescending(x => x.TotalQuantity) // Sort by Quantity now
                .Take(10)
                .ToList();

            var vm = new TopStockItemsKpiAjaxModel
            {
                TopItems = topItems
            };

            return PartialView("_TopStockItemsKpi", vm);
        }

        [HttpGet]
        public IActionResult LoadTopPerformersKpi(int monthOffset = 0, bool isTop = true)
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return Unauthorized();

            // 1. Shift the date using the monthOffset
            var referenceDate = _dateProvider.Today.AddMonths(monthOffset);
            var currentMonthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1);
            var currentMonthEnd = currentMonthStart.AddMonths(1);

            var topSalesQ = _context.VW_SALES_FACT.AsNoTracking()
                .Where(x => x.RefDate >= currentMonthStart && x.RefDate < currentMonthEnd && x.LineTotal > 0);

            var topReturnQ = _context.VW_SALES_RETURN_FACT.AsNoTracking()
                .Where(x => x.RefDate >= currentMonthStart && x.RefDate < currentMonthEnd && x.LineTotal > 0);

            // 2. Apply Security Filters
            topSalesQ = ApplySalesRoleFilter(topSalesQ, userName, userType, salesRepCode, teamCode);
            topReturnQ = ApplySalesRoleFilter(topReturnQ, userName, userType, salesRepCode, teamCode);

            // 3. Aggregate (GROUP BY REP NAME ONLY)
            var repSales = topSalesQ
                .Where(x => !string.IsNullOrEmpty(x.SalesRepName))
                .GroupBy(x => x.SalesRepName) // <--- Removed Team Grouping
                .Select(g => new { Rep = g.Key, Total = g.Sum(x => (decimal?)x.LineTotal) ?? 0 })
                .ToList();

            var repReturns = topReturnQ
                .Where(x => !string.IsNullOrEmpty(x.SalesRepName))
                .GroupBy(x => x.SalesRepName) // <--- Removed Team Grouping
                .Select(g => new { Rep = g.Key, Total = g.Sum(x => (decimal?)x.LineTotal) ?? 0 })
                .ToList();

            // Dictionaries now just use the Rep string as the key
            var repSalesDict = repSales.ToDictionary(x => x.Rep ?? "", x => x.Total);
            var repRetDict = repReturns.ToDictionary(x => x.Rep ?? "", x => x.Total);

            var allRepKeys = new HashSet<string>(repSalesDict.Keys.Concat(repRetDict.Keys));

            // Calculate all Net Sales unsorted
            var allRepsUnsorted = allRepKeys.Select(repName => new TopRepSaleRow
            {
                Team = "-", // We can just pass a dash or leave it empty since we won't show it
                RepName = repName,
                NetSale = repSalesDict.GetValueOrDefault(repName, 0) - repRetDict.GetValueOrDefault(repName, 0)
            });

            // 4. Sort based on the toggle (isTop)
            var topRepsList = isTop
                ? allRepsUnsorted.OrderByDescending(x => x.NetSale).Take(10).ToList() // Best performers
                : allRepsUnsorted.OrderBy(x => x.NetSale).Take(10).ToList();          // Lowest performers

            // 5. Build the VM for the Partial View
            var vm = new TopPerformersKpiViewModel
            {
                TopReps = topRepsList,
                CurrentMonthOffset = monthOffset,
                MonthLabel = currentMonthStart.ToString("MMM yyyy"),
                IsTop = isTop
            };

            return PartialView("_TopPerformersKPI", vm);
        }

        [HttpGet]
        public IActionResult LoadOutstandingKpi(string? invoiceType = null, int? rangeDays = null, string? pdMode = null, string team = "All")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return Unauthorized();

            // ==========================================
            // 1. NEW: TEAM DROPDOWN & SECURITY LOGIC
            // ==========================================
            var salesQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));

            var dbConfig = _context.MIS_DEFAULT_CONFIG.AsNoTracking().FirstOrDefault();
            int actualRangeDays = rangeDays ?? dbConfig?.AgingDefaultDays ?? 90;
            string actualInvoiceType = string.IsNullOrEmpty(invoiceType) ? (dbConfig?.OutstandInvType ?? "Delivered") : invoiceType;
            string actualPdMode = string.IsNullOrEmpty(pdMode) ? (dbConfig?.PdCheq ?? "Without PD") : pdMode;

            // Apply security (Make sure ApplySalesRoleFilter is copied into this controller!)
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);

            // Extract available teams for the dropdown
            var availableTeams = salesQ.Select(x => x.LocShort).Distinct().OrderBy(x => x).ToList();

            var availableDays = _context.OUTSTANDING_DAYS
                .AsNoTracking()
                .Select(x => x.Days)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (!availableDays.Any())
            {
                availableDays = new List<int> { 30, 60, 90, 120, 150 };
            }

            if (!availableDays.Contains(actualRangeDays))
            {
                rangeDays = availableDays.Max();
            }

            // Group by Invoice so we can map each invoice to its team
            var invoiceTeams = salesQ.GroupBy(x => x.InvoDocNo).Select(g => new { InvoNo = g.Key, Team = g.Max(x => x.LocShort) });

            // Apply the dropdown filter if they picked a specific team
            if (team != "All")
            {
                invoiceTeams = invoiceTeams.Where(x => x.Team == team);
            }
            // ==========================================

            // 2. Get Base Outstanding (Only active debt)
            var outQ = _context.CUSTOMER_OUTSTANDING.AsNoTracking().Where(x => x.BalanceAmt > 0);

            // 3. Get Invoices to apply Delivery Status and Security Filters
            var invQ = _context.CUSTOMER_INVOICE_MAIN.AsNoTracking().Where(x => x.Cancel == false);
            invQ = ApplyInvoiceRoleFilter(invQ, userName, userType, salesRepCode, teamCode);

            if (actualInvoiceType == "Delivered")
                invQ = invQ.Where(x => x.isFinalDelivery == true);
            else if (actualInvoiceType == "Non Delivered")
                invQ = invQ.Where(x => x.isFinalDelivery == false || x.isFinalDelivery == null);



            // 4. Join them together into our mutable DebtItem list
            var debtList = (from o in outQ
                            join i in invQ on o.DocNo equals i.InvoDocNo
                            join t in invoiceTeams on o.DocNo equals t.InvoNo // <-- THIS APPLIES THE TEAM FILTER!
                            select new DebtItem
                            {
                                DocNo = o.DocNo,
                                RefDate = o.RefDate,
                                FDeliveryDate = i.FDeliveryDate, // <-- NEW: Grab the Delivery Date
                                BalanceAmt = o.BalanceAmt
                            }).ToList();

            // 5. Handle "With PD" Logic (Deducting Uncleared Cheques)
            if (actualPdMode == "With PD")
            {
                var pdTotals = (from t in _context.CUSTOMER_PAYMENT_TEMP.AsNoTracking()
                                join c in _context.CHEQUE.AsNoTracking() on t.ChequeRefNo equals c.ChequeRefNO
                                where t.isDeposited == false
                                   && c.Deposit_Ref == null
                                   && c.ChequeType == "CUSTOMER"
                                   && c.Origin == "RECEIPT_PAYMENT"
                                   && (t.Cancel == false || t.Cancel == null)
                                group t by t.DocNo into g
                                select new
                                {
                                    DocNo = g.Key,
                                    PdAmt = g.Sum(x => x.PayAmt ?? 0m)
                                }).ToDictionary(x => x.DocNo ?? "", x => x.PdAmt);

                foreach (var debt in debtList)
                {
                    if (pdTotals.TryGetValue(debt.DocNo ?? "", out decimal pdAmt))
                    {
                        debt.BalanceAmt -= pdAmt; // Deduct the cheque from the outstanding balance
                    }
                }

                // Remove any invoices that are now fully "paid" by the pending cheques
                debtList = debtList.Where(x => x.BalanceAmt > 0).ToList();
            }

            // 6. Ageing Calculation
            var today = _dateProvider.Today;

            var agedDebtList = debtList.Select(x => {
                // Use Delivery Date if explicitly requested, otherwise fallback to standard RefDate
                DateTime? targetDate = (actualInvoiceType == "Delivered")
                                        ? (x.FDeliveryDate ?? x.RefDate)
                                        : x.RefDate;

                int age = targetDate != null ? (today - targetDate.Value).Days : 0;

                return new
                {
                    x.BalanceAmt,
                    IsAbove = age > actualRangeDays
                };
            }).ToList();

            var belowItems = agedDebtList.Where(x => !x.IsAbove).ToList();
            var aboveItems = agedDebtList.Where(x => x.IsAbove).ToList();


            var vm = new OutstandingKpiViewModel
            {
                SelectedTeam = team,
                AvailableTeams = availableTeams,
                AvailableDays = availableDays,     // <-- ADD THE DB DAYS TO THE VIEW MODEL

                InvoiceType = actualInvoiceType,
                RangeDays = actualRangeDays,
                PdMode = actualPdMode,

                BelowCount = belowItems.Count,
                BelowAmount = belowItems.Sum(x => x.BalanceAmt),

                AboveCount = aboveItems.Count,
                AboveAmount = aboveItems.Sum(x => x.BalanceAmt)
            };

            return PartialView("_OutstandingKpi", vm);
        }

        [HttpGet]
        public IActionResult LoadCollectionKpi(string team = "All", DateTime? filterDate = null, string? outMode = null, string? freezeMode = null, string category = "All")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return Unauthorized();

            // ==========================================
            // 1. DB DEFAULTS & FREEZE DATES
            // ==========================================
            var dbConfig = _context.MIS_DEFAULT_CONFIG.AsNoTracking().FirstOrDefault();
            string actualOutMode = string.IsNullOrEmpty(outMode) ? (dbConfig?.CollectionType ?? "All") : outMode;
            string actualFreezeMode = string.IsNullOrEmpty(freezeMode) ? (dbConfig?.CollectionFreeze ?? "Freeze(month)") : freezeMode;

            var systemToday = _dateProvider.Today.Date;
            var systemMonthStart = new DateTime(systemToday.Year, systemToday.Month, 1);

            // Calculate Monday of the current week
            int daysSinceMonday = (int)systemToday.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysSinceMonday < 0) daysSinceMonday += 7; // Handle Sunday (0) shifting to 6 days ago
            var systemWeekStart = systemToday.AddDays(-daysSinceMonday).Date;

            var selectedDate = filterDate ?? systemToday;
            var thisMonthStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
            var thisMonthEnd = new DateTime(selectedDate.Year, selectedDate.Month, DateTime.DaysInMonth(selectedDate.Year, selectedDate.Month));
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var lastMonthEnd = thisMonthStart;

            // 2. Fetch Available Categories
            var availableCategories = _context.CUS_CATEGORY.AsNoTracking()
                                                  .Where(c => !string.IsNullOrEmpty(c.CusCategoryName))
                                                  .Select(c => c.CusCategoryName)
                                                  .Distinct().OrderBy(c => c).ToList();

            // 3. Pre-Fetch valid Customer Codes
            List<string> validCusCodes = null;
            if (category != "All")
            {
                validCusCodes = (from p in _context.PartnerDetails.AsNoTracking()
                                 join c in _context.CUS_CATEGORY.AsNoTracking() on p.CusCategoryCode equals c.CusCategoryCode
                                 where c.CusCategoryName == category
                                 select p.Pcode).Distinct().ToList();
            }

            // 4. Security Gatekeeper
            var salesQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);

            var availableTeams = salesQ.Select(x => x.LocShort).Distinct().OrderBy(x => x).ToList();
            if (team != "All") salesQ = salesQ.Where(x => x.LocShort == team);

            var secureDocsQ = salesQ.Select(x => x.InvoDocNo).Distinct();

            // ==========================================
            // DATABASE HITS FOR MATRIX
            // ==========================================

            // 1. DIRECT AND CREDIT
            var directAndCreditPayments = (from cp in _context.CUSTOMER_PAYMENT.AsNoTracking()
                                           where secureDocsQ.Contains(cp.DocNo)
                                              && cp.PayDate >= lastMonthStart && cp.PayDate <= thisMonthEnd // <-- CHANGED HERE
                                              && cp.Cancel != true
                                              && (cp.PayType == "DIRECT PAYMENT" || cp.PayType == "CREDIT NOTE SETTLEMENT")
                                              && (validCusCodes == null || validCusCodes.Contains(cp.CusCode))
                                           select new { cp.PayType, cp.PayAmt, cp.PayDate }).ToList();

            // 2. COLLECTED CHEQUES
            var collectedCheques = (from cpt in _context.CUSTOMER_PAYMENT_TEMP.AsNoTracking()
                                    where secureDocsQ.Contains(cpt.DocNo)
                                       && cpt.PayDate >= lastMonthStart && cpt.PayDate <= thisMonthEnd // <-- CHANGED HERE
                                       && cpt.PayType == "CHEQUE PAYMENT"
                                       && (validCusCodes == null || validCusCodes.Contains(cpt.CusCode))
                                       && cpt.isDeposited == false
                                       && cpt.Cancel != true
                                    select new { cpt.PayAmt, cpt.PayDate }).ToList();

            // 3. DEPOSITED CHEQUES
            var depositedChequesList = (from cp in _context.CUSTOMER_PAYMENT.AsNoTracking()
                                        join c in _context.CHEQUE.AsNoTracking() on cp.ChequeRefNo equals c.ChequeRefNO
                                        where secureDocsQ.Contains(cp.DocNo)
                                           && cp.Cancel != true
                                           && cp.PayType == "CHEQUE PAYMENT"
                                           && c.ChequeStatus == "DEPOSITED"
                                           && c.Deposit_Ref != null
                                           && c.OwnerType == "CUSTOMER"
                                           && c.Deposit_Date >= lastMonthStart && c.Deposit_Date <= thisMonthEnd // <-- CHANGED HERE
                                           && (validCusCodes == null || validCusCodes.Contains(cp.CusCode))
                                        select new { cp.PayAmt, c.Deposit_Date }).ToList();

            // 4. RETURNED CHEQUES
            var returnedChequesList = (from cp in _context.CUSTOMER_PAYMENT.AsNoTracking()
                                       join c in _context.CHEQUE.AsNoTracking() on cp.ReceiptNo equals c.ReceiptVoucherNo
                                       where secureDocsQ.Contains(cp.DocNo)
                                          && cp.Cancel == false
                                          && cp.PayType == "CHEQUE PAYMENT"
                                          && c.ChequeStatus == "RETURNED"
                                          && c.OwnerCode == cp.CusCode
                                          && c.Returned_Date >= lastMonthStart && c.Returned_Date <= thisMonthEnd // <-- CHANGED HERE
                                          && (validCusCodes == null || validCusCodes.Contains(cp.CusCode))
                                       select new { cp.PayAmt, c.Returned_Date }).ToList();

            var nonDepSum = (from c in _context.CHEQUE.AsNoTracking()
                             join cp in _context.CUSTOMER_PAYMENT.AsNoTracking() on c.ChequeRefNO equals cp.ChequeRefNo
                             where secureDocsQ.Contains(cp.DocNo)
                                && c.ChequeRefDate <= selectedDate && c.Deposit_Ref == null
                                && c.OwnerType == "CUSTOMER"
                                && cp.Cancel != true
                                && (validCusCodes == null || validCusCodes.Contains(cp.CusCode))
                             select cp.PayAmt).Sum() ?? 0;

            var unrealizedTotalSum = (from cpt in _context.CUSTOMER_PAYMENT_TEMP.AsNoTracking()
                                      where secureDocsQ.Contains(cpt.DocNo)
                                         && cpt.Cancel != true
                                         && cpt.PayType == "CHEQUE PAYMENT"
                                         && (validCusCodes == null || validCusCodes.Contains(cpt.CusCode))
                                         && cpt.isDeposited == false
                                      select cpt.PayAmt).Sum() ?? 0;

            // ==========================================
            // IN-MEMORY MATH FOR MATRIX
            // ==========================================

            decimal dirToday = 0, dirThis = 0, dirLast = 0;
            decimal credToday = 0, credThis = 0, credLast = 0;

            foreach (var p in directAndCreditPayments)
            {
                if (p.PayType == "DIRECT PAYMENT")
                {
                    if (p.PayDate == selectedDate) dirToday += p.PayAmt ?? 0;
                    if (p.PayDate >= thisMonthStart) dirThis += p.PayAmt ?? 0;
                    if (p.PayDate >= lastMonthStart && p.PayDate < lastMonthEnd) dirLast += p.PayAmt ?? 0;
                }
                else if (p.PayType == "CREDIT NOTE SETTLEMENT")
                {
                    if (p.PayDate == selectedDate) credToday += p.PayAmt ?? 0;
                    if (p.PayDate >= thisMonthStart) credThis += p.PayAmt ?? 0;
                    if (p.PayDate >= lastMonthStart && p.PayDate < lastMonthEnd) credLast += p.PayAmt ?? 0;
                }
            }

            decimal colToday = 0, colThis = 0, colLast = 0;
            foreach (var p in collectedCheques)
            {
                if (p.PayDate == selectedDate) colToday += p.PayAmt ?? 0;
                if (p.PayDate >= thisMonthStart) colThis += p.PayAmt ?? 0;
                if (p.PayDate >= lastMonthStart && p.PayDate < lastMonthEnd) colLast += p.PayAmt ?? 0;
            }

            decimal depToday = 0, depThis = 0, depLast = 0;
            foreach (var p in depositedChequesList)
            {
                if (p.Deposit_Date == selectedDate) depToday += p.PayAmt ?? 0;
                if (p.Deposit_Date >= thisMonthStart) depThis += p.PayAmt ?? 0;
                if (p.Deposit_Date >= lastMonthStart && p.Deposit_Date < lastMonthEnd) depLast += p.PayAmt ?? 0;
            }

            decimal rtnToday = 0, rtnThis = 0, rtnLast = 0;
            foreach (var r in returnedChequesList)
            {
                if (r.Returned_Date == selectedDate) rtnToday += r.PayAmt ?? 0;
                if (r.Returned_Date >= thisMonthStart) rtnThis += r.PayAmt ?? 0;
                if (r.Returned_Date >= lastMonthStart && r.Returned_Date < lastMonthEnd) rtnLast += r.PayAmt ?? 0;
            }

            // ==========================================
            // SYSTEM-LOCKED OUTSTANDING & REALIZED MATH 
            // ==========================================

            var sysDirect = (from cp in _context.CUSTOMER_PAYMENT.AsNoTracking()
                             where secureDocsQ.Contains(cp.DocNo)
                                && cp.PayType == "DIRECT PAYMENT" && cp.PayDate >= systemMonthStart && cp.PayDate <= systemToday && cp.Cancel != true
                                && (validCusCodes == null || validCusCodes.Contains(cp.CusCode))
                             select cp.PayAmt).Sum() ?? 0;

            var sysDeposited = (from cp in _context.CUSTOMER_PAYMENT.AsNoTracking()
                                join c in _context.CHEQUE.AsNoTracking() on cp.ChequeRefNo equals c.ChequeRefNO
                                where secureDocsQ.Contains(cp.DocNo)
                                   && cp.PayType == "CHEQUE PAYMENT" && c.Deposit_Date >= systemMonthStart && c.Deposit_Date <= systemToday
                                   && c.ChequeStatus == "DEPOSITED" && c.Deposit_Ref != null && cp.Cancel != true
                                   && c.OwnerType == "CUSTOMER"
                                   && (validCusCodes == null || validCusCodes.Contains(cp.CusCode))
                                select cp.PayAmt).Sum() ?? 0;

            decimal systemMonthRealizedTotal = sysDirect + sysDeposited;

            var rawOutstandingQuery = (from o in _context.CUSTOMER_OUTSTANDING.AsNoTracking()
                                       join v in secureDocsQ on o.DocNo equals v
                                       join inv in _context.CUSTOMER_INVOICE_MAIN.AsNoTracking() on o.DocNo equals inv.InvoDocNo into invJoin
                                       from inv in invJoin.DefaultIfEmpty()
                                       where o.BalanceAmt > 0
                                          && (validCusCodes == null || validCusCodes.Contains(o.CusCode))
                                       select new
                                       {
                                           o.BalanceAmt,
                                           o.RefDate,

                                           // ---> THE NEW LOGIC: Pull directly from Invoice! Fallback to 0 if null. <---
                                           CreditDays = inv != null ? (inv.CreditDays ?? 0) : 0,

                                           DeliveryDate = inv != null ? inv.FDeliveryDate : (DateTime?)null
                                       });

            if (actualOutMode == "Delivered")
            {
                rawOutstandingQuery = rawOutstandingQuery.Where(x => x.DeliveryDate != null);
            }

            var rawOutstanding = rawOutstandingQuery.ToList();

            // 5. DETERMINE THE TARGET DATE BASED ON ACTUAL FREEZE MODE
            DateTime outstandingTargetDate;
            if (actualFreezeMode == "Freeze(month)")
                outstandingTargetDate = systemMonthStart;
            else if (actualFreezeMode == "Freeze(week)")
                outstandingTargetDate = systemWeekStart;
            else
                outstandingTargetDate = systemToday; // NonFreeze

            // This exact calculation stays the same, but now it uses the Invoice's own CreditDays!
            decimal netOutSum = rawOutstanding
                .Where(x => x.RefDate.HasValue && x.RefDate.Value.AddDays(x.CreditDays) <= outstandingTargetDate)
                .Sum(x => x.BalanceAmt);

            // ==========================================
            // RETURN VIEW MODEL
            // ==========================================
            var vm = new CollectionKpiAjaxModel
            {
                SelectedDate = selectedDate,
                OutstandingTargetDate = outstandingTargetDate,
                SelectedTeam = team,
                OutstandingMode = actualOutMode,
                FreezeMode = actualFreezeMode,
                SelectedCategory = category,
                AvailableCategories = availableCategories,
                AvailableTeams = availableTeams,

                DirectCash = new CollectionMetric { Today = dirToday, ThisMonth = dirThis, LastMonth = dirLast },
                ChequeCollected = new CollectionMetric { Today = colToday, ThisMonth = colThis, LastMonth = colLast },
                ChequeDeposited = new CollectionMetric { Today = depToday, ThisMonth = depThis, LastMonth = depLast },
                NonDepositChequeSum = nonDepSum,

                CreditNotes = new CollectionMetric { Today = credToday, ThisMonth = credThis, LastMonth = credLast },
                ReturnCheques = new CollectionMetric { Today = rtnToday, ThisMonth = rtnThis, LastMonth = rtnLast },

                SystemMonthRealized = systemMonthRealizedTotal,
                NetOutstanding = netOutSum,
                UnrealizedTotal = unrealizedTotalSum
            };

            return PartialView("_CollectionKpi", vm);
        }

        [HttpGet]
        public IActionResult LoadLoginKpi()
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return Unauthorized();

            var today = _dateProvider.Today.Date;
            var tomorrow = today.AddDays(1);

            var logQuery = _context.LOGIN_LOGS.AsNoTracking()
                .Where(x => x.LoginTime >= today && x.LoginTime < tomorrow);

            logQuery = ApplyLoginRoleFilter(logQuery, userName, userType, teamCode);

            var vm = new LoginKpiAjaxModel
            {
                TodayLoginCount = logQuery.Count()
            };

            return PartialView("_LoginKpi", vm);
        }

        [HttpGet]
        public IActionResult LoginHistory(DateTime? filterDate)
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Login", "Account");

            var targetDate = filterDate ?? _dateProvider.Today.Date;
            var nextDate = targetDate.AddDays(1);

            var logQuery = _context.LOGIN_LOGS.AsNoTracking()
                .Where(x => x.LoginTime >= targetDate && x.LoginTime < nextDate);

            logQuery = ApplyLoginRoleFilter(logQuery, userName, userType, teamCode);

            var vm = new LoginHistoryViewModel
            {
                SelectedDate = targetDate,
                Logs = logQuery.OrderByDescending(x => x.LoginTime).ToList()
            };

            return View(vm);
        }


        // ==========================================
        // PRIVATE HELPER METHODS (The Clean Up)
        // ==========================================

        private IQueryable<T> ApplySalesRoleFilter<T>(IQueryable<T> query, string userName, string userType, string salesRepCode, string teamCode) where T : class
        {
            // 1. REP (Stays exactly the same)
            if (userType == "REP")
            {
                return query.Where(e => EF.Property<string>(e, "SalesRepCode") == salesRepCode);
            }

            // 2. ADMIN (L006 sees everything)
            if (userType == "DIRECTOR" && teamCode == "L006")
            {
                return query;
            }

            // 3. EVERYONE ELSE (SM, ASM, DIRECTOR Not L006)
            // They all now use the new GetAllowedRepCodes logic!
            if (userType == "ASM" || userType == "SM" || userType == "OTHER" || (userType == "DIRECTOR" && teamCode != "L006"))
            {
                var repCodes = GetAllowedRepCodes(userName, userType);

                // Include their direct SalesRepCode from session just in case
                if (!string.IsNullOrEmpty(salesRepCode) && !repCodes.Contains(salesRepCode))
                {
                    repCodes.Add(salesRepCode);
                }

                return query.Where(e => repCodes.Contains(EF.Property<string>(e, "SalesRepCode")));
            }

            return query;
        }

        private IQueryable<CustomerInvoiceMain> ApplyInvoiceRoleFilter(IQueryable<CustomerInvoiceMain> query, string userName, string userType, string salesRepCode, string teamCode)
        {
            // 1. REP
            if (userType == "REP")
            {
                return query.Where(x => x.SalesRepCode == salesRepCode);
            }

            // 2. ADMIN (L006)
            if (userType == "DIRECTOR" && teamCode == "L006")
            {
                return query;
            }

            // 3. EVERYONE ELSE (SM, ASM, DIRECTOR Not L006)
            if (userType == "ASM" || userType == "SM" || userType == "OTHER" || (userType == "DIRECTOR" && teamCode != "L006"))
            {
                var repCodes = GetAllowedRepCodes(userName, userType);
                if (!string.IsNullOrEmpty(salesRepCode) && !repCodes.Contains(salesRepCode)) repCodes.Add(salesRepCode);

                return query.Where(x => repCodes.Contains(x.SalesRepCode));
            }

            return query;
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

        private IQueryable<LoginLog> ApplyLoginRoleFilter(IQueryable<LoginLog> query, string userName, string userType, string teamCode)
        {
            // 1. REP: Can only see their own logins
            if (userType == "REP")
            {
                return query.Where(x => x.Username == userName);
            }

            // 2. ADMIN (L006): Sees everyone
            if (userType == "DIRECTOR" && teamCode == "L006")
            {
                return query;
            }

            // 3. EVERYONE ELSE (SM, ASM, DIRECTOR Not L006)
            if (userType == "ASM" || userType == "SM" || userType == "OTHER" || (userType == "DIRECTOR" && teamCode != "L006"))
            {
                // ---> THE FIX: We pass BOTH userName and userType here <---
                var allowedRepCodes = GetAllowedRepCodes(userName, userType);

                // Convert the allowed SalesRepCodes into Usernames
                var allowedUsernames = _context.WKF_USER_REP_MAP.AsNoTracking()
                    .Where(x => allowedRepCodes.Contains(x.SalesRepCode))
                    .Select(x => x.UserName)
                    .ToList();

                allowedUsernames.Add(userName); // Include their own username so they can see their own logins

                return query.Where(x => allowedUsernames.Contains(x.Username));
            }

            return query;
        }

        private List<string> GetAllowedRepCodes(string userName, string userType)
        {

            var cachedReps = HttpContext.Session.GetString("MyAllowedReps");
            if (!string.IsNullOrEmpty(cachedReps))
            {
                return cachedReps.Split(',').ToList(); // Instantly return from memory!
            }

            var repCodes = new List<string>();

            if (userType == "REP")
            {
                return repCodes;
            }

            else if (userType == "ASM" || userType == "OTHER")
            {
                // First, check if this user is actually an SM by looking them up in the mapping table
                var assignedASMs = _context.WKF_MAP_SM_ASM
                    .Where(x => x.UserNameSM == userName)
                    .Select(x => x.UserNameASM)
                    .ToList();

                if (assignedASMs.Any())
                {
                    // ---> THEY ARE AN SM <---
                    repCodes = _context.WKF_MAP_REP_ASM_MIS
                        .Where(x => assignedASMs.Contains(x.UserName) || x.UserName == userName)
                        .Select(x => x.SalesRepCode)
                        .Distinct()
                        .ToList();
                }
                else
                {
                    // ---> THEY ARE A NORMAL ASM (or OTHER) <---
                    repCodes = _context.WKF_MAP_REP_ASM_MIS
                        .Where(x => x.UserName == userName)
                        .Select(x => x.SalesRepCode)
                        .Distinct()
                        .ToList();
                }
            }

            else if (userType == "DIRECTOR")
            {
                var assignedUserNames = _context.WKF_MAP_ASM_DIR
                    .Where(x => x.UserNameDir == userName)
                    .Select(x => x.UserNameAsm)
                    .ToList();

                repCodes = _context.WKF_MAP_REP_ASM_MIS
                    .Where(x => assignedUserNames.Contains(x.UserName))
                    .Select(x => x.SalesRepCode)
                    .Distinct()
                    .ToList();
            }

            // Save the final list to the Session so we never hit the DB again for this user!
            if (repCodes.Any())
            {
                HttpContext.Session.SetString("MyAllowedReps", string.Join(",", repCodes));
            }

            return repCodes;
        }

        public class DebtItem
        {
            public string DocNo { get; set; }
            public DateTime? RefDate { get; set; }
            public DateTime? FDeliveryDate { get; set; }
            public decimal BalanceAmt { get; set; }
            
        }
    }
}