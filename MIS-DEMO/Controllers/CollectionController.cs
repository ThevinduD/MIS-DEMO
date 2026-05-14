using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;
using MIS_DEMO.Models.ViewModels;
using MIS_DEMO.Models.ViewModels.Collection;
using MIS_DEMO.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MIS_DEMO.Controllers
{
    public class CollectionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IDateProvider _dateProvider;

        public CollectionController(AppDbContext context, IDateProvider dateProvider)
        {
            _context = context;
            _dateProvider = dateProvider;
        }

        [HttpGet]
        public IActionResult Details(DateTime? fromDate, DateTime? toDate, string team = "All")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Login", "Account");

            // Set defaults
            var today = _dateProvider.Today.Date;
            var startDate = fromDate ?? today;
            var endDate = toDate ?? today;

            // 1. Security Gatekeeper & Fetch Teams into RAM (Option 2 Performance Fix)
            var salesQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);

            var availableTeams = salesQ.Select(x => x.LocShort).Distinct().OrderBy(x => x).ToList();

            // .ToList() runs the complex View ONCE and saves it to RAM, stopping the SQL Timeout
            var invoiceTeamsList = salesQ
                .GroupBy(x => x.InvoDocNo)
                .Select(g => new { InvoNo = g.Key, Team = g.Max(x => x.LocShort) })
                .ToList();

            if (team != "All")
            {
                invoiceTeamsList = invoiceTeamsList.Where(x => x.Team == team).ToList();
            }

            // Convert to a super-fast Dictionary (Updated to be Case-Insensitive and ignore spaces!)
            var teamDict = invoiceTeamsList
                .Where(x => !string.IsNullOrEmpty(x.InvoNo))
                .ToDictionary(
                    x => x.InvoNo.Trim(),
                    x => x.Team,
                    StringComparer.OrdinalIgnoreCase // This makes "INV123" equal to "inv123"
                );


            // 2. Fetch Base Data Fast, Then Group in Memory

            // ==========================================
            // DIRECT CASH (With PayDate)
            // ==========================================
            var directDb = _context.CUSTOMER_PAYMENT.AsNoTracking()
                .Where(cp => cp.PayType == "DIRECT PAYMENT" && cp.PayDate >= startDate && cp.PayDate <= endDate && cp.Cancel == false)
                // 1. Fetch PayDate from the database
                .Select(cp => new { cp.ReceiptNo, cp.DocNo, cp.PayAmt, cp.PayDate })
                .ToList();

            var directList = directDb
                .Where(cp => cp.DocNo != null && teamDict.ContainsKey(cp.DocNo.Trim()))
                // 2. Add PayDate to the GroupBy key
                .GroupBy(cp => new {
                    ReceiptNo = cp.ReceiptNo ?? "N/A",
                    Team = teamDict[cp.DocNo.Trim()],
                    cp.PayDate
                })
                .Select(g => new ReceiptGroupRow
                {
                    ReceiptNo = g.Key.ReceiptNo,
                    Team = g.Key.Team,
                    PayDate = g.Key.PayDate,
                    TotalAmount = g.Sum(x => x.PayAmt) ?? 0
                }).OrderByDescending(x => x.TotalAmount).ToList();


            // ==========================================
            // CHEQUES COLLECTED (With ChequeNo)
            // ==========================================
            var collectedDb = (from cpt in _context.CUSTOMER_PAYMENT_TEMP.AsNoTracking()
                               join c in _context.CHEQUE.AsNoTracking() on cpt.ChequeRefNo equals c.ChequeRefNO into cJoin
                               from c in cJoin.DefaultIfEmpty()
                               where cpt.PayType == "CHEQUE PAYMENT"
                                  && cpt.PayDate >= startDate
                                  && cpt.PayDate <= endDate
                                  && cpt.Cancel != true
                                  && cpt.isDeposited == false
                               select new
                               {
                                   cpt.ReceiptNo,
                                   cpt.DocNo,
                                   cpt.PayAmt,
                                   cpt.ChequeRefNo,
                                   cpt.PayDate,
                                   ChequeNo = c != null ? c.ChequeNo : "-", // <-- 1. Fetch ChequeNo safely
                                   RealizedDate = c != null ? c.RealizedDate : (DateTime?)null
                               }).ToList();

            var collectedList = collectedDb
                .Where(cpt => cpt.DocNo != null && teamDict.ContainsKey(cpt.DocNo.Trim()))
                .GroupBy(cpt => new {
                    ReceiptNo = cpt.ReceiptNo ?? "N/A",
                    Team = teamDict[cpt.DocNo.Trim()],
                    ChequeRefNo = cpt.ChequeRefNo ?? "N/A",
                    ChequeNo = cpt.ChequeNo, // <-- 2. Add to GroupBy
                    cpt.RealizedDate,
                    cpt.PayDate
                })
                .Select(g => new ReceiptGroupRow
                {
                    ReceiptNo = g.Key.ReceiptNo,
                    Team = g.Key.Team,
                    ChequeRefNo = g.Key.ChequeRefNo,
                    ChequeNo = g.Key.ChequeNo, // <-- 3. Map to ViewModel
                    RealizedDate = g.Key.RealizedDate,
                    PayDate = g.Key.PayDate,
                    TotalAmount = g.Sum(x => x.PayAmt) ?? 0
                }).OrderByDescending(x => x.TotalAmount).ToList();


            // ==========================================
            // CHEQUES DEPOSITED (With ChequeNo & Trim Fix)
            // ==========================================
            var depositedDb = (from cp in _context.CUSTOMER_PAYMENT.AsNoTracking()
                               join c in _context.CHEQUE.AsNoTracking() on cp.ChequeRefNo equals c.ChequeRefNO
                               where cp.PayType == "CHEQUE PAYMENT" && c.Deposit_Date >= startDate && c.Deposit_Date <= endDate
                                  && c.ChequeStatus == "DEPOSITED" && c.Deposit_Ref != null && cp.Cancel == false
                               select new
                               {
                                   cp.ReceiptNo,
                                   cp.DocNo,
                                   cp.PayAmt,
                                   cp.ChequeRefNo,
                                   c.ChequeNo, // <-- 1. Fetch ChequeNo
                                   c.Deposit_Date
                               }).ToList();

            var depositedList = depositedDb
                .Where(cp => cp.DocNo != null && teamDict.ContainsKey(cp.DocNo.Trim())) // Trim added for safety
                .GroupBy(cp => new {
                    ReceiptNo = cp.ReceiptNo ?? "N/A",
                    Team = teamDict[cp.DocNo.Trim()], // Trim added for safety
                    ChequeRefNo = cp.ChequeRefNo ?? "N/A",
                    ChequeNo = cp.ChequeNo ?? "-", // <-- 2. Add to GroupBy
                    cp.Deposit_Date
                })
                .Select(g => new ReceiptGroupRow
                {
                    ReceiptNo = g.Key.ReceiptNo,
                    Team = g.Key.Team,
                    ChequeRefNo = g.Key.ChequeRefNo,
                    ChequeNo = g.Key.ChequeNo, // <-- 3. Map to ViewModel
                    DepositedDate = g.Key.Deposit_Date,
                    TotalAmount = g.Sum(x => x.PayAmt) ?? 0
                }).OrderByDescending(x => x.TotalAmount).ToList();




            var vm = new CollectionDetailsViewModel
            {
                FromDate = startDate,
                ToDate = endDate,
                SelectedTeam = team,
                AvailableTeams = availableTeams,
                DirectPayments = directList,
                ChequesCollected = collectedList,
                ChequesDeposited = depositedList,
                
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult ReceiptDetails(string receiptNo, string team = "All")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(receiptNo))
                return RedirectToAction("Login", "Account");

            // 1. Security Gatekeeper: Get all invoices this user is allowed to see
            var salesQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);
            var validInvoices = salesQ.Select(x => x.InvoDocNo).Distinct();

            // 2. Find standard payments tied to this Receipt
            var stdPayments = (from cp in _context.CUSTOMER_PAYMENT.AsNoTracking()
                               join v in validInvoices on cp.DocNo equals v // Security Join
                               join inv in _context.CUSTOMER_INVOICE_MAIN.AsNoTracking() on cp.DocNo equals inv.InvoDocNo
                               where cp.ReceiptNo == receiptNo && cp.Cancel == false
                               select new ReceiptInvoiceRow
                               {
                                   InvoiceNo = cp.DocNo,
                                   InvoiceDate = inv.RefDate,
                                   InvoiceTotal = inv.InvoiceAmt,
                                   PaidAmount = cp.PayAmt ?? 0,
                                   PayType = cp.PayType
                               }).ToList();

            // 3. Find pending cheques tied to this Receipt
            var tempPayments = (from cp in _context.CUSTOMER_PAYMENT_TEMP.AsNoTracking()
                                join v in validInvoices on cp.DocNo equals v // Security Join
                                join inv in _context.CUSTOMER_INVOICE_MAIN.AsNoTracking() on cp.DocNo equals inv.InvoDocNo
                                where cp.ReceiptNo == receiptNo && (cp.Cancel == false || cp.Cancel == null)
                                select new ReceiptInvoiceRow
                                {
                                    InvoiceNo = cp.DocNo,
                                    InvoiceDate = inv.RefDate,
                                    InvoiceTotal = inv.InvoiceAmt,
                                    PaidAmount = cp.PayAmt ?? 0,
                                    PayType = "PENDING CHEQUE"
                                }).ToList();

            // Combine them
            var combined = stdPayments.Concat(tempPayments).OrderByDescending(x => x.PaidAmount).ToList();

            var vm = new ReceiptDetailsViewModel
            {
                ReceiptNo = receiptNo,
                Team = team,
                Invoices = combined
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult NetOutstandingDetails(
        string team = "All", DateTime? filterDate = null, string? outMode = null, string? freezeMode = null, string category = "All",
        string? searchInvo = null, string? searchRep = null, string? searchCustomer = null) // <-- ADDED SEARCH PARAMETERS
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Login", "Account");

            // 1. Setup Dates & Freeze Logic (Same as KPI)
            var systemToday = _dateProvider.Today.Date;
            var systemMonthStart = new DateTime(systemToday.Year, systemToday.Month, 1);
            int daysSinceMonday = (int)systemToday.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysSinceMonday < 0) daysSinceMonday += 7;
            var systemWeekStart = systemToday.AddDays(-daysSinceMonday).Date;

            var selectedDate = filterDate ?? systemToday;

            string actualFreezeMode = freezeMode ?? "Freeze(month)";
            DateTime outstandingTargetDate = systemToday; // Default
            if (actualFreezeMode == "Freeze(month)") outstandingTargetDate = systemMonthStart;
            if (actualFreezeMode == "Freeze(week)") outstandingTargetDate = systemWeekStart;

            // 2. Pre-Fetch Categories (Same as KPI)
            List<string> validCusCodes = null;
            if (category != "All")
            {
                validCusCodes = (from p in _context.PartnerDetails.AsNoTracking()
                                 join c in _context.CUS_CATEGORY.AsNoTracking() on p.CusCategoryCode equals c.CusCategoryCode
                                 where c.CusCategoryName == category
                                 select p.Pcode).Distinct().ToList();
            }

            // 3. Security Gatekeeper & Sales Data
            var salesQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);

            // Grab teams BEFORE filtering by the selected team, so the dropdown has all options
            var availableTeams = salesQ.Select(x => x.LocShort).Distinct().OrderBy(x => x).ToList();

            if (team != "All") salesQ = salesQ.Where(x => x.LocShort == team);

            var secureDocsQ = salesQ.Select(x => x.InvoDocNo).Distinct();

            var salesHeaders = salesQ
                .GroupBy(x => x.InvoDocNo)
                .Select(g => new {
                    InvoNo = g.Key,
                    Team = g.Max(x => x.LocShort),
                    RepName = g.Max(x => x.SalesRepName),
                    CusName = g.Max(x => x.CusName)
                });

            // 4. Fetch the Raw Data
            var rawQuery = (from o in _context.CUSTOMER_OUTSTANDING.AsNoTracking()
                            join p in _context.PartnerDetails.AsNoTracking() on o.CusCode equals p.Pcode
                            join m in _context.MasCreditType.AsNoTracking() on p.CreditPeriod equals m.credittypeky.ToString()
                            join s in salesHeaders on o.DocNo equals s.InvoNo
                            join inv in _context.CUSTOMER_INVOICE_MAIN.AsNoTracking() on o.DocNo equals inv.InvoDocNo into invJoin
                            from inv in invJoin.DefaultIfEmpty()
                            where o.BalanceAmt > 0
                               && secureDocsQ.Contains(o.DocNo)
                               && (validCusCodes == null || validCusCodes.Contains(o.CusCode))
                            select new
                            {
                                InvoNo = o.DocNo,
                                InvDate = o.RefDate,
                                DelDate = inv != null ? inv.FDeliveryDate : (DateTime?)null,
                                CreditDays = m.creditdays, // Fallback to 0 if null
                                Team = s.Team,
                                RepName = s.RepName,
                                Customer = s.CusName,
                                InvAmount = o.InvoiceAmt,
                                Balance = o.BalanceAmt
                            });

            // ==========================================
            // ---> APPLY THE NEW SEARCH FILTERS <---
            // ==========================================
            if (!string.IsNullOrEmpty(searchInvo))
                rawQuery = rawQuery.Where(x => x.InvoNo.Contains(searchInvo));

            if (!string.IsNullOrEmpty(searchRep))
                rawQuery = rawQuery.Where(x => x.RepName.Contains(searchRep));

            if (!string.IsNullOrEmpty(searchCustomer))
                rawQuery = rawQuery.Where(x => x.Customer.Contains(searchCustomer));

            if (outMode == "Delivered")
            {
                rawQuery = rawQuery.Where(x => x.DelDate != null);
            }

            var rawData = rawQuery.ToList();

            // 5. In-Memory Math: Due Date & Aging
            var mappedRows = rawData.Select(x => {
                // Core Logic: Use Delivery Date. Fallback to InvDate if null.
                DateTime? baseDateForAging = x.DelDate ?? x.InvDate;
                DateTime? dueDate = baseDateForAging?.AddDays(x.CreditDays);

                int aging = 0;
                if (dueDate.HasValue)
                {
                    // Date Diff from Due date (Positive means overdue, Negative means not due yet)
                    aging = (systemToday - dueDate.Value).Days;
                }

                return new NetOutRow
                {
                    InvoNo = x.InvoNo,
                    InvDate = x.InvDate,
                    DelDate = x.DelDate,
                    CreditPeriod = x.CreditDays,
                    Team = x.Team,
                    RepName = x.RepName,
                    Customer = x.Customer,
                    InvAmount = x.InvAmount,
                    Balance = x.Balance,
                    DueDate = dueDate,
                    Aging = aging
                };
            })
            // ONLY keep rows where the DueDate is on or before the Target Date (This is the definition of "Net Out")
            .Where(x => x.DueDate.HasValue && x.DueDate.Value <= outstandingTargetDate)
            .OrderByDescending(x => x.Aging)
            .ToList();

            var vm = new NetOutstandingDetailsViewModel
            {
                SelectedTeam = team,
                SelectedDate = selectedDate,
                OutstandingMode = outMode,
                FreezeMode = freezeMode,
                SelectedCategory = category,

                // ---> Map the new filter properties to pass back to the View <---
                SearchInvo = searchInvo,
                SearchRep = searchRep,
                SearchCustomer = searchCustomer,
                AvailableTeams = availableTeams,

                Rows = mappedRows,
                TotalNetOut = mappedRows.Sum(x => x.Balance)
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult InvoiceDetails(string invoNo)
        {
            var userName = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Login", "Account");

            // Fetch all lines for this specific invoice
            var salesLines = _context.VW_SALES_FACT
                .AsNoTracking()
                .Where(x => x.InvoDocNo == invoNo)
                .ToList();

            if (!salesLines.Any())
            {
                return NotFound("Invoice not found or no items exist for this document.");
            }

            // Grab the header info from the very first line (since Customer/Rep are the same for the whole invoice)
            var headerInfo = salesLines.First();

            // Map the line items
            var mappedLines = salesLines.Select(x => new InvoiceLineItem
            {
                ItemCode = x.ItemCode,
                Description = x.ItemDescription,
                Qty = x.Qty,
                UnitPrice = x.SoldPrice,
                LineTotal = x.LineTotal
            }).ToList();

            var vm = new InvoiceDetailsViewModel
            {
                InvoNo = invoNo,
                RefDate = headerInfo.RefDate,
                CustomerName = headerInfo.CusName ?? "Unknown Customer",
                RepName = headerInfo.SalesRepName ?? "Unknown Rep",
                Team = headerInfo.LocShort ?? "N/A",
                Lines = mappedLines,
                TotalAmount = mappedLines.Sum(x => x.LineTotal)
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult UnrealizedDetails(string team = "All", string category = "All")
        {
            var userName = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");
            var salesRepCode = HttpContext.Session.GetString("SalesRepCode");
            var teamCode = HttpContext.Session.GetString("TeamCode");

            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Login", "Account");

            // 1. Fetch Categories
            List<string> validCusCodes = null;
            if (category != "All")
            {
                validCusCodes = (from p in _context.PartnerDetails.AsNoTracking()
                                 join c in _context.CUS_CATEGORY.AsNoTracking() on p.CusCategoryCode equals c.CusCategoryCode
                                 where c.CusCategoryName == category
                                 select p.Pcode).Distinct().ToList();
            }

            // 2. Security Gatekeeper & Sales Data (to get Team/Rep info)
            var salesQ = _context.VW_SALES_FACT.AsNoTracking().Where(x => !string.IsNullOrEmpty(x.LocShort));
            salesQ = ApplySalesRoleFilter(salesQ, userName, userType, salesRepCode, teamCode);

            var availableTeams = salesQ.Select(x => x.LocShort).Distinct().OrderBy(x => x).ToList();

            if (team != "All") salesQ = salesQ.Where(x => x.LocShort == team);

            var secureDocsQ = salesQ.Select(x => x.InvoDocNo).Distinct();

            var salesHeaders = salesQ
                .GroupBy(x => x.InvoDocNo)
                .Select(g => new {
                    InvoNo = g.Key,
                    Team = g.Max(x => x.LocShort),
                    RepName = g.Max(x => x.SalesRepName),
                    CusName = g.Max(x => x.CusName)
                });

            // 3. Fetch the Raw Unrealized Data
            var rawQuery = (from cpt in _context.CUSTOMER_PAYMENT_TEMP.AsNoTracking()
                            join s in salesHeaders on cpt.DocNo equals s.InvoNo

                            // ---> NEW: LEFT JOIN the CHEQUE table safely <---
                            join c in _context.CHEQUE.AsNoTracking() on cpt.ChequeRefNo equals c.ChequeRefNO into cJoin
                            from c in cJoin.DefaultIfEmpty()

                            where secureDocsQ.Contains(cpt.DocNo)
                               && cpt.Cancel != true
                               && cpt.isDeposited == false
                               && cpt.PayType == "CHEQUE PAYMENT"
                               && (validCusCodes == null || validCusCodes.Contains(cpt.CusCode))
                            select new UnrealizedRow
                            {
                                ReceiptNo = cpt.ReceiptNo,
                                ChequeRefNo = cpt.ChequeRefNo,

                                // ---> NEW: Safely map the Cheque Number <---
                                ChequeNo = c != null ? c.ChequeNo : "-",

                                PayDate = cpt.PayDate,
                                InvoNo = cpt.DocNo,
                                Team = s.Team,
                                RepName = s.RepName,
                                Customer = s.CusName,
                                Amount = cpt.PayAmt ?? 0
                            }).OrderByDescending(x => x.Amount).ToList();

            // 4. Build Datalists for JS Filters
            var availableInvoices = rawQuery.Select(x => x.InvoNo).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
            var availableReps = rawQuery.Select(x => x.RepName).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
            var availableCustomers = rawQuery.Select(x => x.Customer).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();

            var vm = new UnrealizedDetailsViewModel
            {
                SelectedTeam = team,
                SelectedCategory = category,
                AvailableTeams = availableTeams,
                AvailableInvoices = availableInvoices,
                AvailableReps = availableReps,
                AvailableCustomers = availableCustomers,
                Rows = rawQuery,
                TotalAmount = rawQuery.Sum(x => x.Amount)
            };

            return View(vm);
        }


        // ==========================================
        // PRIVATE HELPER METHODS (Copied from Home for security)
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