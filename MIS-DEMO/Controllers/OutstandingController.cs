using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;
using MIS_DEMO.Models;
using MIS_DEMO.Models.ViewModels;
using MIS_DEMO.Services;

namespace MIS_DEMO.Controllers
{
    public class OutstandingController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IDateProvider _dateProvider;

        public OutstandingController(AppDbContext context, IDateProvider dateProvider)
        {
            _context = context;
            _dateProvider = dateProvider;
        }

        private class RawOutstandingData
        {
            public string DocNo { get; set; }
            public DateTime? RefDate { get; set; }
            public DateTime? FDeliveryDate { get; set; }

            public string CusCode { get; set; }
            public string CusName { get; set; }
            public decimal InvoiceAmt { get; set; }
            public decimal BalanceAmt { get; set; }
            public string RepCode { get; set; }
            public string RepName { get; set; }
            public string Team { get; set; }
            
        }

        [HttpGet]
        public IActionResult HierarchySummary(string? invoiceType = null, int? rangeDays = null, string? pdMode = null, string team = "All")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var sessionTeamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Login", "Account");

            // 1. Fetch Defaults
            var dbConfig = _context.MIS_DEFAULT_CONFIG.AsNoTracking().FirstOrDefault();
            int actualRangeDays = rangeDays ?? dbConfig?.AgingDefaultDays ?? 90;
            string actualInvoiceType = string.IsNullOrEmpty(invoiceType) ? (dbConfig?.OutstandInvType ?? "Delivered") : invoiceType;
            string actualPdMode = string.IsNullOrEmpty(pdMode) ? (dbConfig?.PdCheq ?? "Without PD") : pdMode;

            // 2. Get Allowed Reps
            var allowedRepCodes = GetAllowedRepCodes(userName, userType);
            if (!string.IsNullOrEmpty(salesRepCode) && !allowedRepCodes.Contains(salesRepCode))
                allowedRepCodes.Add(salesRepCode);

            bool isAdmin = userType == "DIRECTOR" && sessionTeamCode == "L006";

            // 3. Base Queries
            var outQ = _context.CUSTOMER_OUTSTANDING.AsNoTracking().Where(x => x.BalanceAmt > 0);
            var invQ = _context.CUSTOMER_INVOICE_MAIN.AsNoTracking().Where(x => x.Cancel == false);

            if (actualInvoiceType == "Delivered")
                invQ = invQ.Where(x => x.isFinalDelivery == true);
            else if (actualInvoiceType == "Non Delivered")
                invQ = invQ.Where(x => x.isFinalDelivery == false || x.isFinalDelivery == null);

            var salesQ = _context.VW_SALES_FACT.AsNoTracking();
            if (!isAdmin)
            {
                salesQ = salesQ.Where(x => allowedRepCodes.Contains(x.SalesRepCode));
            }
            if (team != "All")
            {
                salesQ = salesQ.Where(x => x.LocShort == team);
            }

            var salesHeaders = salesQ
                .GroupBy(x => x.InvoDocNo)
                .Select(g => new {
                    InvoNo = g.Key,
                    SalesRepCode = g.Max(x => x.SalesRepCode),
                    LocShort = g.Max(x => x.LocShort)
                });

            // Join data
            var rawList = (from o in outQ
                           join i in invQ on o.DocNo equals i.InvoDocNo
                           join s in salesHeaders on o.DocNo equals s.InvoNo
                           select new
                           {
                               DocNo = o.DocNo,
                               BalanceAmt = o.BalanceAmt,
                               RepCode = s.SalesRepCode
                           }).ToList();

            // Calculate PD Cheques
            var pdTotals = new Dictionary<string, decimal>();
            if (actualPdMode == "With PD")
            {
                pdTotals = (from t in _context.CUSTOMER_PAYMENT_TEMP.AsNoTracking()
                            join c in _context.CHEQUE.AsNoTracking() on t.ChequeRefNo equals c.ChequeRefNO
                            where t.isDeposited == false
                               && c.Deposit_Ref == null
                               && c.ChequeType == "CUSTOMER"
                               && c.Origin == "RECEIPT_PAYMENT"
                               && (t.Cancel == false || t.Cancel == null)
                            group t by t.DocNo into g
                            select new { DocNo = g.Key, PdAmt = g.Sum(x => x.PayAmt ?? 0m) })
                            .ToDictionary(x => x.DocNo ?? "", x => x.PdAmt);
            }

            // Aggregate by RepCode
            var repAggregates = rawList
                .GroupBy(x => x.RepCode)
                .Select(g => new
                {
                    RepCode = g.Key ?? "",
                    OutAmt = g.Sum(x => x.BalanceAmt),
                    PdAmt = g.Sum(x => pdTotals.TryGetValue(x.DocNo ?? "", out decimal pd) ? pd : 0m)
                }).ToList();

            // Get Human Names for Reps
            var repNamesDict = _context.SALES_REP.AsNoTracking()
                .Select(x => new { x.SalesRepCode, x.SalesRepName })
                .ToDictionary(x => x.SalesRepCode ?? "", x => x.SalesRepName ?? "Unknown");

            // ==========================================
            // Build the Flattened Hierarchy
            // ==========================================
            var vm = new HierarchySummaryViewModel
            {
                InvoiceType = actualInvoiceType,
                RangeDays = actualRangeDays,
                PdMode = actualPdMode,
                Team = team,
                GrandTotalOut = repAggregates.Sum(x => x.OutAmt),
                GrandTotalPd = repAggregates.Sum(x => x.PdAmt),
                GrandTotalGross = repAggregates.Sum(x => x.OutAmt - x.PdAmt),

                // Map directly to a flat list, sorted by highest gross debt
                Rows = repAggregates.Select(x => new HierarchyRepRow
                {
                    RepCode = x.RepCode,
                    RepName = repNamesDict.TryGetValue(x.RepCode, out var name) ? name : x.RepCode,
                    OutstandingAmt = x.OutAmt,
                    PdAmt = x.PdAmt
                })
                .OrderByDescending(x => x.GrossAmt)
                .ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult Index(string? invoiceType = null, int? rangeDays = null, string? pdMode = null, string team = "All", string? ageBracket = null, string? filterRepCode = null) // <-- ADDED filterRepCode
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var sessionTeamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Login", "Account");

            // ==========================================
            // 1. FETCH DEFAULTS FROM YOUR CONFIG TABLE
            // ==========================================
            var dbConfig = _context.MIS_DEFAULT_CONFIG.AsNoTracking().FirstOrDefault();

            int actualRangeDays = rangeDays ?? dbConfig?.AgingDefaultDays ?? 90;
            string actualInvoiceType = string.IsNullOrEmpty(invoiceType) ? (dbConfig?.OutstandInvType ?? "Delivered") : invoiceType;
            string actualPdMode = string.IsNullOrEmpty(pdMode) ? (dbConfig?.PdCheq ?? "Without PD") : pdMode;

            // Fetch dynamic range days
            var availableDays = _context.OUTSTANDING_DAYS.AsNoTracking().Select(x => x.Days).Distinct().OrderBy(x => x).ToList();
            if (!availableDays.Any()) availableDays = new List<int> { 30, 60, 90, 120, 150 };
            if (!availableDays.Contains(actualRangeDays)) actualRangeDays = availableDays.Max();
            // ==========================================

            // Get Outstandings
            var outQ = _context.CUSTOMER_OUTSTANDING.AsNoTracking().Where(x => x.BalanceAmt > 0);

            // Get Delivery Status
            var invQ = _context.CUSTOMER_INVOICE_MAIN.AsNoTracking().Where(x => x.Cancel == false);

            if (actualInvoiceType == "Delivered")
                invQ = invQ.Where(x => x.isFinalDelivery == true);
            else if (actualInvoiceType == "Non Delivered")
                invQ = invQ.Where(x => x.isFinalDelivery == false || x.isFinalDelivery == null);

            // Get Reps, Teams, and Security
            var salesQ = _context.VW_SALES_FACT.AsNoTracking();
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, sessionTeamCode);

            // Apply the specific team filter
            if (team != "All")
            {
                salesQ = salesQ.Where(x => x.LocShort == team);
            }

            // ==========================================
            // NEW: FILTER BY SPECIFIC REP (CLICKED FROM HIERARCHY)
            // ==========================================
            if (!string.IsNullOrEmpty(filterRepCode))
            {
                salesQ = salesQ.Where(x => x.SalesRepCode == filterRepCode);
            }
            // ==========================================

            var salesHeaders = salesQ
                .GroupBy(x => x.InvoDocNo)
                .Select(g => new {
                    InvoNo = g.Key,
                    SalesRepCode = g.Max(x => x.SalesRepCode),
                    SalesRepName = g.Max(x => x.SalesRepName),
                    CusName = g.Max(x => x.CusName),
                    LocShort = g.Max(x => x.LocShort)
                });

            // Safely Join all three tables together
            var rawList = (from o in outQ
                           join i in invQ on o.DocNo equals i.InvoDocNo
                           join s in salesHeaders on o.DocNo equals s.InvoNo
                           select new RawOutstandingData
                           {
                               DocNo = o.DocNo,
                               RefDate = o.RefDate,
                               FDeliveryDate = i.FDeliveryDate, // <-- Grab the Delivery Date!
                               CusCode = o.CusCode,
                               CusName = s.CusName,
                               InvoiceAmt = o.InvoiceAmt,
                               BalanceAmt = o.BalanceAmt,
                               RepCode = s.SalesRepCode,
                               RepName = s.SalesRepName,
                               Team = s.LocShort
                           }).ToList();

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
                                select new { DocNo = g.Key, PdAmt = g.Sum(x => x.PayAmt ?? 0m) })
                                .ToDictionary(x => x.DocNo ?? "", x => x.PdAmt);

                foreach (var item in rawList)
                {
                    if (pdTotals.TryGetValue(item.DocNo ?? "", out decimal pdAmt))
                    {
                        item.BalanceAmt -= pdAmt;
                    }
                }
                rawList = rawList.Where(x => x.BalanceAmt > 0).ToList();
            }

            var today = _dateProvider.Today;

            // Map to the View Model
            var mappedRows = rawList.Select(x => {

                // ---> THE FIX: Exact same conditional logic <---
                DateTime? targetDate = (actualInvoiceType == "Delivered")
                                        ? (x.FDeliveryDate ?? x.RefDate)
                                        : x.RefDate;

                int age = targetDate != null ? (today - targetDate.Value).Days : 0;

                return new OutstandingRow
                {
                    DocNo = x.DocNo,
                    RefDate = x.RefDate,
                    FDeliveryDate = x.FDeliveryDate,
                    CusCode = x.CusCode ?? "Unknown",
                    CusName = x.CusName ?? "Unknown Customer",
                    InvoiceAmt = x.InvoiceAmt,
                    BalanceAmt = x.BalanceAmt,
                    RepCode = x.RepCode ?? "N/A",
                    RepName = x.RepName ?? "Unknown Rep",
                    Team = x.Team ?? "N/A",
                    AgeDays = age,
                    AgeBracket = age > actualRangeDays ? "Above" : "Below"
                };
            });

            // ==========================================
            // 2. NEW: FILTER BY AGE BRACKET (IF CLICKED FROM KPI)
            // ==========================================
            if (!string.IsNullOrEmpty(ageBracket))
            {
                mappedRows = mappedRows.Where(x => x.AgeBracket == ageBracket);
            }
            // ==========================================

            // ==========================================
            // 3. CONDITIONAL SORTING LOGIC
            // ==========================================
            List<OutstandingRow> finalRows;
            if (actualInvoiceType == "Delivered")
            {
                // Sort by DeliveryDate (fallback to RefDate just in case DeliveryDate is somehow null)
                finalRows = mappedRows.OrderBy(x => x.FDeliveryDate ?? x.RefDate).ToList();
            }
            else
            {
                // Standard sort by RefDate
                finalRows = mappedRows.OrderBy(x => x.RefDate).ToList();
            }
            // ==========================================

            var vm = new OutstandingDetailsViewModel
            {
                SelectedTeam = team,
                AvailableDays = availableDays,
                InvoiceType = actualInvoiceType,
                RangeDays = actualRangeDays,
                PdMode = actualPdMode,
                Rows = finalRows, // <-- Pass the conditionally sorted (and filtered) list
                TotalOutstanding = finalRows.Sum(x => x.BalanceAmt)
            };

            return View(vm);
        }


        [HttpGet]
        public IActionResult InvoiceDetails(string invoiceNo)
        {
            if (string.IsNullOrEmpty(invoiceNo))
                return RedirectToAction("Index");

            // 1. Get Sales Line Items
            var salesLines = _context.VW_SALES_FACT
                .AsNoTracking()
                .Where(x => x.InvoDocNo == invoiceNo)
                .ToList();

            if (!salesLines.Any()) return RedirectToAction("Index");

            // 2. NEW: Get Return Line Items
            var returnLines = _context.VW_SALES_RETURN_FACT
                .AsNoTracking()
                .Where(x => x.InvoDocNo == invoiceNo)
                .ToList();

            // 3. Get Payment Lines (Safely handling NULL Receipt numbers with a LEFT JOIN)
            var paymentLines = (from cp in _context.CUSTOMER_PAYMENT.AsNoTracking()
                where cp.DocNo == invoiceNo
                    && (cp.Method == "RECEIPT_PAYMENT" || cp.Method == "ADVANCE SETTLE")
                    && cp.Cancel == false

                // --- THE FIX: Create a SQL LEFT JOIN ---
                join r in _context.RECEIPT.AsNoTracking() on cp.ReceiptNo equals r.ReceiptNo into receiptGroup
                from rg in receiptGroup.DefaultIfEmpty()
                    // ---------------------------------------

                select new
                {
                    ReceiptNo = cp.ReceiptNo ?? cp.PaymentNo,
                    PayDate = cp.PayDate,
                    Method = cp.Method,
                    PayType = cp.PayType,
                    ChequeRefNo = cp.ChequeRefNo,
                    CardRefNo = cp.CardRefNo,

                    // Safely check if the joined record exists before grabbing the amount
                    TotalPaidAmt = rg != null ? rg.TotalPaidAmt : cp.PayAmt,

                    PayAmt = cp.PayAmt
                }).ToList();

            // 4
            var settlementLines = _context.CUSTOMER_PAYMENT
                .AsNoTracking()
                .Where(x => x.DocNo == invoiceNo
                         && x.PayType == "CREDIT NOTE SETTLEMENT"
                         && x.Cancel == false)
                .ToList();

            var pdLines = (from cp in _context.CUSTOMER_PAYMENT_TEMP.AsNoTracking()
                           where cp.DocNo == invoiceNo
                              && cp.isDeposited == false
                              && (cp.Cancel == false || cp.Cancel == null)

                           // Left Join to get the total receipt amount
                           join r in _context.RECEIPT.AsNoTracking() on cp.ReceiptNo equals r.ReceiptNo into receiptGroup
                           from rg in receiptGroup.DefaultIfEmpty()

                           select new
                           {
                               ReceiptNo = cp.ReceiptNo ?? cp.PaymentNo,
                               PayDate = cp.PayDate,
                               Method = cp.Method,
                               PayType = cp.PayType,
                               ChequeRefNo = cp.ChequeRefNo,
                               CardRefNo = cp.CardRefNo,
                               TotalPaidAmt = rg != null ? rg.TotalPaidAmt : cp.PayAmt,
                               PayAmt = cp.PayAmt
                           }).ToList();

            // 5. Get Outstanding Balances
            var outstanding = _context.CUSTOMER_OUTSTANDING
                .AsNoTracking()
                .FirstOrDefault(x => x.DocNo == invoiceNo);

            var header = salesLines.First();

            // 6. Build the ViewModel
            var vm = new InvoiceDetailViewModel
            {
                InvoiceNo = invoiceNo,
                InvoiceDate = header.RefDate,
                Team = header.LocShort ?? "N/A",
                RepName = $"{header.SalesRepName} ({header.SalesRepCode})",
                CusName = $"{header.CusName} ({header.CusCode})",

                InvoiceAmount = outstanding != null ? outstanding.InvoiceAmt : 0m,
                BalanceAmount = outstanding != null ? outstanding.BalanceAmt : 0m,

                // Map Sold Items
                Items = salesLines.Select(x => new InvoiceItemRow
                {
                    ItemCode = x.ItemCode,
                    ItemName = x.ItemDescription,
                    Qty = x.Qty,
                    UnitPrice = x.SoldPrice,
                    Amount = x.LineTotal
                }).ToList(),

                // NEW: Map Returned Items
                ReturnedItems = returnLines.Select(x => new InvoiceItemRow
                {
                    ItemCode = x.ItemCode,
                    ItemName = x.Description,       // Note: Column is named Description here
                    Qty = x.Qty,
                    UnitPrice = x.ReturnedPrice,    // Note: Column is named ReturnedPrice here
                    Amount = x.LineTotal
                }).ToList(),

                // Map Payments
                Payments = paymentLines.Select(x => new PaymentItemRow
                {
                    ReceiptNo = x.ReceiptNo ?? "N/A",
                    PayDate = x.PayDate,
                    Type = $"{x.Method} ({x.PayType})",
                    RefNo = !string.IsNullOrEmpty(x.ChequeRefNo) ? x.ChequeRefNo : (x.CardRefNo ?? "-"),
                    ReceiptAmount = x.TotalPaidAmt ?? 0m,   // <--- Map the Total Receipt Amount
                    Amount = x.PayAmt ?? 0m                 // <--- Map the Set Off Amount
                }).ToList(),

                Settlements = settlementLines.Select(x => new SettlementItemRow
                {
                    SettlementNo = x.PaymentNo ?? "N/A",
                    Amount = x.ReturnAmt ?? x.PayAmt ?? 0m,   // Fallback to PayAmt if ReturnAmt is empty
                    SetOffAmount = x.PayAmt ?? 0m,
                    Note = x.Type ?? x.Method ?? "-",         // Grabbing the note/type
                    RefNo = !string.IsNullOrEmpty(x.ChequeRefNo) ? x.ChequeRefNo : (x.ReceiptNo ?? "-")
                }).ToList(),

                PdCheques = pdLines.Select(x => new PaymentItemRow
                {
                    ReceiptNo = x.ReceiptNo ?? "N/A",
                    PayDate = x.PayDate,
                    Type = $"{x.Method} ({x.PayType})",
                    RefNo = !string.IsNullOrEmpty(x.ChequeRefNo) ? x.ChequeRefNo : (x.CardRefNo ?? "-"),
                    ReceiptAmount = x.TotalPaidAmt ?? 0m,
                    Amount = x.PayAmt ?? 0m
                }).ToList()
            };

            return View(vm);
        }


        // ==========================================
        // PRIVATE SECURITY FILTER (Using VW_SALES_FACT)
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