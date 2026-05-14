using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;
using MIS_DEMO.Models;
using MIS_DEMO.Models.ViewModels;
using MIS_DEMO.Services;

namespace MIS_DEMO.Controllers
{
    public class NonDeliveredController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IDateProvider _dateProvider;

        public NonDeliveredController(
            AppDbContext context,
            IDateProvider dateProvider)
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
            var cutoff45 = today.AddDays(-45);

            // Base: pending invoices
            IQueryable<CustomerInvoiceMain> q = _context.CUSTOMER_INVOICE_MAIN
                .AsNoTracking()
                .Where(x =>
                    (x.isFinalDelivery == false || x.isFinalDelivery == null) &&
                    x.InvoiceAmt != 0 &&
                    x.Cancel == false
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
                    return View(new NonDeliveredInvoicesViewModel { CutoffDate = cutoff45 });

                q = q.Where(x => repCodes.Contains(x.SalesRepCode));
            }
            // ==========================================

            // Join TEAM_MIS to convert Pat_Name(LocCode) -> LocShort
            var qWithTeam =
                from inv in q
                join tm in _context.TEAM_MIS.AsNoTracking()
                    on inv.Pat_Name equals tm.LocCode into gj
                from tm in gj.DefaultIfEmpty()
                select new
                {
                    inv.RefDate,
                    inv.InvoiceAmt,
                    Team = (tm != null && tm.LocShort != null && tm.LocShort != "")
                            ? tm.LocShort
                            : inv.Pat_Name // fallback if not mapped
                };

            // Under 45 days
            var under = qWithTeam
                .Where(x => x.RefDate > cutoff45)
                .GroupBy(x => x.Team)
                .Select(g => new
                {
                    Team = g.Key,
                    Cnt = g.Count(),
                    Amt = g.Sum(x => (decimal?)x.InvoiceAmt) ?? 0
                })
                .ToList();

            // Over 45 days
            var over = qWithTeam
                .Where(x => x.RefDate <= cutoff45)
                .GroupBy(x => x.Team)
                .Select(g => new
                {
                    Team = g.Key,
                    Cnt = g.Count(),
                    Amt = g.Sum(x => (decimal?)x.InvoiceAmt) ?? 0
                })
                .ToList();

            var underDict = under.ToDictionary(x => x.Team ?? "", x => (x.Cnt, x.Amt));
            var overDict = over.ToDictionary(x => x.Team ?? "", x => (x.Cnt, x.Amt));

            var allTeams = new HashSet<string>(underDict.Keys.Concat(overDict.Keys));

            var rows = allTeams
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => new NonDeliveredTeamRowVm
                {
                    Team = t,
                    Under45Count = underDict.TryGetValue(t, out var u) ? u.Cnt : 0,
                    Under45Amount = underDict.TryGetValue(t, out var u2) ? u2.Amt : 0,
                    Over45Count = overDict.TryGetValue(t, out var o) ? o.Cnt : 0,
                    Over45Amount = overDict.TryGetValue(t, out var o2) ? o2.Amt : 0
                })
                .OrderBy(x => x.Team)
                .ToList();

            var model = new NonDeliveredInvoicesViewModel
            {
                CutoffDate = cutoff45,
                Rows = rows,
                Under45CountTotal = rows.Sum(x => x.Under45Count),
                Under45AmountTotal = rows.Sum(x => x.Under45Amount),
                Over45CountTotal = rows.Sum(x => x.Over45Count),
                Over45AmountTotal = rows.Sum(x => x.Over45Amount),
            };

            ViewBag.Title = "Invoice Delivery - Values in 1LK";
            ViewBag.SubTitle = $"Cutoff: {cutoff45:yyyy-MM-dd}";

            return View(model);
        }

        [HttpGet]
        public IActionResult TeamInvoices(string team, string bucket, string? invoice, string? rep, string? customer)
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userType))
                return RedirectToAction("Login", "Account");

            // validate inputs
            team = (team ?? "").Trim();
            bucket = (bucket ?? "").Trim().ToLower();

            if (string.IsNullOrWhiteSpace(team))
                return RedirectToAction("Invoices");

            if (bucket != "under45" && bucket != "over45")
                bucket = "under45";

            var today = _dateProvider.Today.Date;
            var cutoff45 = today.AddDays(-45);

            // -----------------------------
            // Base: pending invoices
            // -----------------------------
            IQueryable<CustomerInvoiceMain> q = _context.CUSTOMER_INVOICE_MAIN
                .AsNoTracking()
                .Where(x =>
                    (x.isFinalDelivery == false || x.isFinalDelivery == null) &&
                    x.InvoiceAmt != 0 &&
                    x.Cancel == false
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
                    return View("TeamInvoices", new TeamInvoicesViewModel
                    {
                        Team = team,
                        Bucket = bucket,
                        CutoffDate = cutoff45
                    });

                q = q.Where(x => repCodes.Contains(x.SalesRepCode));
            }
            // ==========================================

            // -----------------------------
            // SALES header lookup (1 row per invoice) for CusName + SalesRepName
            // -----------------------------
            var salesHeaderQ = _context.VW_SALES_FACT
                .AsNoTracking()
                .Where(x => x.InvoDocNo != null && x.InvoDocNo != "")
                .GroupBy(x => x.InvoDocNo)
                .Select(g => new
                {
                    InvoDocNo = g.Key,
                    CusName = g.Select(x => x.CusName).FirstOrDefault(),
                    SalesRepName = g.Select(x => x.SalesRepName).FirstOrDefault(),
                });

            // -----------------------------
            // Join: invoice -> TEAM_MIS (LocShort) + join sales header
            // -----------------------------
            var baseQ =
                from inv in q
                join tm in _context.TEAM_MIS.AsNoTracking()
                    on inv.Pat_Name equals tm.LocCode into teamJoin
                from tm in teamJoin.DefaultIfEmpty()

                join sh in salesHeaderQ
                    on inv.InvoDocNo equals sh.InvoDocNo into salesJoin
                from sh in salesJoin.DefaultIfEmpty()

                select new
                {
                    inv.RefDate,
                    inv.InvoiceAmt,
                    inv.InvoDocNo,
                    inv.CusCode,
                    inv.SalesRepCode,

                    CusName = sh != null ? sh.CusName : null,
                    SalesRepName = sh != null ? sh.SalesRepName : null,

                    Team = (tm != null && tm.LocShort != null && tm.LocShort != "")
                            ? tm.LocShort
                            : inv.Pat_Name
                };

            // -----------------------------
            // Filter by selected TEAM (LocShort)
            // -----------------------------
            baseQ = baseQ.Where(x => x.Team == team);

            // -----------------------------
            // Filter by bucket (under/over 45)
            // -----------------------------
            if (bucket == "under45")
                baseQ = baseQ.Where(x => x.RefDate > cutoff45);
            else
                baseQ = baseQ.Where(x => x.RefDate <= cutoff45);

            // -----------------------------
            // Optional search filters (invoice/rep/customer)
            // -----------------------------
            string invF = (invoice ?? "").Trim().ToLower();
            string repF = (rep ?? "").Trim().ToLower();
            string cusF = (customer ?? "").Trim().ToLower();

            if (!string.IsNullOrEmpty(invF))
                baseQ = baseQ.Where(x => x.InvoDocNo != null && x.InvoDocNo.ToLower().Contains(invF));

            if (!string.IsNullOrEmpty(repF))
                baseQ = baseQ.Where(x =>
                    (x.SalesRepName != null && x.SalesRepName.ToLower().Contains(repF)) ||
                    (x.SalesRepCode != null && x.SalesRepCode.ToLower().Contains(repF))
                );

            if (!string.IsNullOrEmpty(cusF))
                baseQ = baseQ.Where(x =>
                    (x.CusName != null && x.CusName.ToLower().Contains(cusF)) ||
                    (x.CusCode != null && x.CusCode.ToLower().Contains(cusF))
                );

            // -----------------------------
            // Performance safety: TOP 1000
            // -----------------------------
            const int MAX_ROWS = 1000;

            int totalRows = baseQ.Count();

            var rows = baseQ
                .OrderByDescending(x => x.RefDate)
                .ThenByDescending(x => x.InvoiceAmt)
                .Take(MAX_ROWS)
                .ToList()
                .Select(x => new TeamInvoicesRowVm
                {
                    InvoDocNo = x.InvoDocNo ?? "",
                    RefDate = x.RefDate,
                    Amount = x.InvoiceAmt,
                    CusCode = x.CusCode ?? "",
                    CusName = x.CusName ?? x.CusCode ?? "",
                    SalesRepCode = x.SalesRepCode ?? "",
                    SalesRepName = x.SalesRepName ?? x.SalesRepCode ?? ""
                })
                .ToList();

            var vm = new TeamInvoicesViewModel
            {
                Team = team,
                Bucket = bucket,
                CutoffDate = cutoff45,

                Invoice = invoice,
                Rep = rep,
                Customer = customer,

                TotalRows = totalRows,
                MaxRows = MAX_ROWS,
                IsTruncated = totalRows > MAX_ROWS,

                TotalAmount = rows.Sum(r => r.Amount),
                Rows = rows
            };

            ViewBag.Title = $"Pending Invoices - {team}";
            ViewBag.SubTitle = bucket == "under45"
                ? $"Less than 45 days (>{cutoff45:yyyy-MM-dd})"
                : $"More than 45 days (≤{cutoff45:yyyy-MM-dd})";

            return View("TeamInvoices", vm);
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