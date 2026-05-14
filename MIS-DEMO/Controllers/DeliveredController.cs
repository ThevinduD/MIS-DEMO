using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;
using MIS_DEMO.Models;
using MIS_DEMO.Models.ViewModels;
using MIS_DEMO.Services;

namespace MIS_DEMO.Controllers
{
    public class DeliveredController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IDateProvider _dateProvider;

        public DeliveredController(AppDbContext context, IDateProvider dateProvider)
        {
            _context = context;
            _dateProvider = dateProvider;
        }

        [HttpGet]
        public IActionResult Invoices()
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return RedirectToAction("Login", "Account");

            var today = _dateProvider.Today.Date;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);

            // Base: delivered invoices in THIS MONTH by FDeliveryDate
            IQueryable<CustomerInvoiceMain> q = _context.CUSTOMER_INVOICE_MAIN
                .AsNoTracking()
                .Where(x =>
                    x.Cancel == false &&
                    x.InvoiceAmt != 0 &&
                    x.isFinalDelivery == true &&
                    x.FDeliveryDate != null &&
                    x.FDeliveryDate >= monthStart &&
                    x.FDeliveryDate < nextMonthStart
                );

            // ==========================================
            // NEW UNIFIED SECURITY LOGIC
            // ==========================================
            if (userType == "DIRECTOR" && teamCode == "L006")
            {
                // Admin L006 sees everything - No filter applied
            }
            else
            {
                var repCodes = GetAllowedRepCodes(userName, userType);
                if (!string.IsNullOrEmpty(salesRepCode) && !repCodes.Contains(salesRepCode))
                    repCodes.Add(salesRepCode);

                // If they have no reps assigned, return empty view
                if (!repCodes.Any())
                    return View(new DeliveredInvoicesViewModel
                    {
                        MonthStart = monthStart,
                        MonthEndExclusive = nextMonthStart
                    });

                // Filter by the unified list of SalesRepCodes
                q = q.Where(x => repCodes.Contains(x.SalesRepCode));
            }
            // ==========================================

            // Join TEAM_MIS: Pat_Name(LocCode) -> LocShort (Just for display names)
            var qWithTeam =
                from inv in q
                join tm in _context.TEAM_MIS.AsNoTracking()
                    on inv.Pat_Name equals tm.LocCode into gj
                from tm in gj.DefaultIfEmpty()
                select new
                {
                    inv.InvoiceAmt,
                    Team = (tm != null && tm.LocShort != null && tm.LocShort != "")
                        ? tm.LocShort
                        : inv.Pat_Name
                };

            var rows = qWithTeam
                .GroupBy(x => x.Team)
                .Select(g => new DeliveredTeamRowVm
                {
                    Team = g.Key ?? "",
                    DeliveredCount = g.Count(),
                    DeliveredAmount = g.Sum(x => (decimal?)x.InvoiceAmt) ?? 0
                })
                .Where(x => x.Team != "")
                .OrderByDescending(x => x.DeliveredAmount)
                .ToList();

            var model = new DeliveredInvoicesViewModel
            {
                MonthStart = monthStart,
                MonthEndExclusive = nextMonthStart,
                Rows = rows,
                DeliveredCountTotal = rows.Sum(x => x.DeliveredCount),
                DeliveredAmountTotal = rows.Sum(x => x.DeliveredAmount),
            };

            ViewBag.Title = "Delivered Invoices - Values in K";
            ViewBag.SubTitle = $"{monthStart:yyyy-MM-dd} → {today:yyyy-MM-dd}";

            return View(model);
        }

        [HttpGet]
        public IActionResult TeamInvoices(string team)
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return RedirectToAction("Login", "Account");

            var today = _dateProvider.Today.Date;
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = thisMonthStart.AddMonths(1);

            // Base: delivered invoices in this month
            IQueryable<CustomerInvoiceMain> q = _context.CUSTOMER_INVOICE_MAIN
                .AsNoTracking()
                .Where(x =>
                    x.isFinalDelivery == true &&
                    x.Cancel == false &&
                    x.InvoiceAmt != 0 &&
                    x.FDeliveryDate != null &&
                    x.FDeliveryDate >= thisMonthStart &&
                    x.FDeliveryDate < nextMonthStart
                );

            // ==========================================
            // NEW UNIFIED SECURITY LOGIC
            // ==========================================
            if (userType == "DIRECTOR" && teamCode == "L006")
            {
                // Admin L006 sees everything - No filter applied
            }
            else
            {
                var repCodes = GetAllowedRepCodes(userName, userType);
                if (!string.IsNullOrEmpty(salesRepCode) && !repCodes.Contains(salesRepCode))
                    repCodes.Add(salesRepCode);

                if (!repCodes.Any())
                    return View("TeamInvoices", new TeamInvoicesViewModel { Team = team });

                q = q.Where(x => repCodes.Contains(x.SalesRepCode));
            }
            // ==========================================

            // Map Pat_Name -> LocShort
            var qWithTeam =
                from inv in q
                join tm in _context.TEAM_MIS.AsNoTracking()
                    on inv.Pat_Name equals tm.LocCode into gj
                from tm in gj.DefaultIfEmpty()
                select new
                {
                    inv.InvoDocNo,
                    inv.FDeliveryDate,
                    inv.InvoiceAmt,
                    inv.SalesRepCode,
                    Team = (tm != null && !string.IsNullOrEmpty(tm.LocShort))
                        ? tm.LocShort
                        : inv.Pat_Name
                };

            // Filter selected team
            qWithTeam = qWithTeam.Where(x => x.Team == team);

            // Build invoice header lookup from VW_SALES_FACT (deduplicated by invoice)
            var invoiceHeaders =
                _context.VW_SALES_FACT
                    .AsNoTracking()
                    .GroupBy(x => x.InvoDocNo)
                    .Select(g => new
                    {
                        InvoDocNo = g.Key,
                        CusName = g.Select(x => x.CusName).FirstOrDefault(),
                        CusCode = g.Select(x => x.CusCode).FirstOrDefault(),
                        SalesRepName = g.Select(x => x.SalesRepName).FirstOrDefault(),
                        SalesRepCode = g.Select(x => x.SalesRepCode).FirstOrDefault()
                    });

            const int MAX_ROWS = 1000;

            var rows =
                (from inv in qWithTeam
                 join hdr in invoiceHeaders
                     on inv.InvoDocNo equals hdr.InvoDocNo into hj
                 from hdr in hj.DefaultIfEmpty()
                 select new TeamInvoicesRowVm
                 {
                     RefDate = inv.FDeliveryDate,
                     InvoDocNo = inv.InvoDocNo,
                     CusName = hdr.CusName ?? "",
                     CusCode = hdr.CusCode ?? "",
                     SalesRepName = hdr.SalesRepName ?? "",
                     SalesRepCode = hdr.SalesRepCode ?? "",
                     Amount = inv.InvoiceAmt
                 })
                .OrderByDescending(x => x.Amount)
                .ToList();

            var model = new TeamInvoicesViewModel
            {
                Team = team,
                TotalRows = rows.Count,
                MaxRows = MAX_ROWS,
                IsTruncated = rows.Count > MAX_ROWS,
                Rows = rows.Take(MAX_ROWS).ToList(),
                TotalAmount = rows.Take(MAX_ROWS).Sum(x => x.Amount)
            };

            ViewBag.Title = $"Delivered Invoices – {team}";
            ViewBag.SubTitle = $"{thisMonthStart:yyyy-MM-dd} → {nextMonthStart.AddDays(-1):yyyy-MM-dd}";

            return View("TeamInvoices", model);
        }

        // ==========================================
        // PRIVATE SECURITY HELPER
        // ==========================================

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
    }
}