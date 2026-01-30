using Microsoft.EntityFrameworkCore;
using MIS_DEMO.Data;

namespace MIS_DEMO.Services
{
    public class SalesAccessService
    {
        private readonly AppDbContext _context;

        public SalesAccessService(AppDbContext context)
        {
            _context = context;
        }

        public List<string> GetAccessibleRepCodes(string userType, string userName, string? salesRepCode)
        {
            // ---------------- DIRECTOR ----------------
            if (userType == "DIRECTOR")
            {
                // Get Director's TeamCode from WKF_USER_REP_MAP (same table you already use)
                var dirTeamCode = _context.WKF_USER_REP_MAP
                    .AsNoTracking()
                    .Where(x => x.UserName == userName)
                    .Select(x => x.TeamCode)
                    .FirstOrDefault();

                // L006 => ALL access (can't represent as repCodes list safely)
                // We'll return a marker token.
                if (dirTeamCode == "L006")
                    return new List<string> { "__ALL__" };

                // 1) Get mapped ASM/SM usernames under this Director
                var mappedAsmOrSmUsers = _context.WKF_MAP_ASM_DIR
                    .AsNoTracking()
                    .Where(x => x.UserNameDir == userName)
                    .Select(x => x.UserNameAsm)
                    .Distinct()
                    .ToList();

                // 2) From mapped usernames, detect which ones are SMs
                var smUsers = _context.WKF_MAP_SM_ASM
                    .AsNoTracking()
                    .Where(x => mappedAsmOrSmUsers.Contains(x.UserNameSM))
                    .Select(x => x.UserNameSM)
                    .Distinct()
                    .ToList();

                // 3) Expand SMs -> ASMs under those SMs
                var asmsUnderSms = _context.WKF_MAP_SM_ASM
                    .AsNoTracking()
                    .Where(x => smUsers.Contains(x.UserNameSM))
                    .Select(x => x.UserNameASM)
                    .Distinct()
                    .ToList();

                // 4) Build the full ASM username set:
                // - mapped ASMs directly (those not SMs)
                // - plus ASMs under mapped SMs
                var allAsmUsers = mappedAsmOrSmUsers
                    .Union(asmsUnderSms)
                    .Distinct()
                    .ToList();

                // 5) Get SalesRepCodes of those ASMs/SMs themselves (they sell!)
                var asmSmOwnRepCodes = _context.WKF_USER_REP_MAP
                    .AsNoTracking()
                    .Where(x => allAsmUsers.Contains(x.UserName))
                    .Select(x => x.SalesRepCode)
                    .Where(code => !string.IsNullOrEmpty(code))
                    .Distinct()
                    .ToList();

                // 6) Get REP SalesRepCodes under those ASMs
                var repCodesUnderAsms = _context.WKF_MAP_REP_ASM
                    .AsNoTracking()
                    .Where(x => allAsmUsers.Contains(x.UserName))
                    .Select(x => x.SalesRepCode)
                    .Where(code => !string.IsNullOrEmpty(code))
                    .Distinct()
                    .ToList();

                // 7) Combine everything
                var finalCodes = asmSmOwnRepCodes
                    .Union(repCodesUnderAsms)
                    .Distinct()
                    .ToList();

                // 8) Add Director's own SalesRepCode (he sells + returns too)
                if (!string.IsNullOrEmpty(salesRepCode) && !finalCodes.Contains(salesRepCode))
                    finalCodes.Add(salesRepCode);

                return finalCodes;
            }

            // ---------------- REP ----------------
            if (userType == "REP" && !string.IsNullOrEmpty(salesRepCode))
                return new List<string> { salesRepCode };

            // ---------------- ASM / SM ----------------
            if (userType == "ASM")
            {
                var isSM = _context.WKF_MAP_SM_ASM
                    .AsNoTracking()
                    .Any(x => x.UserNameSM == userName);

                if (isSM)
                {
                    var asmUserNames = _context.WKF_MAP_SM_ASM
                        .AsNoTracking()
                        .Where(x => x.UserNameSM == userName)
                        .Select(x => x.UserNameASM)
                        .ToList();

                    var asmRepCodes = _context.WKF_USER_REP_MAP
                        .AsNoTracking()
                        .Where(x => asmUserNames.Contains(x.UserName))
                        .Select(x => x.SalesRepCode)
                        .Where(code => !string.IsNullOrEmpty(code))
                        .ToList();

                    var repRepCodes = _context.WKF_MAP_REP_ASM
                        .AsNoTracking()
                        .Where(x => asmUserNames.Contains(x.UserName))
                        .Select(x => x.SalesRepCode)
                        .Where(code => !string.IsNullOrEmpty(code))
                        .ToList();

                    var codes = asmRepCodes
                        .Union(repRepCodes)
                        .Distinct()
                        .ToList();

                    if (!string.IsNullOrEmpty(salesRepCode) && !codes.Contains(salesRepCode))
                        codes.Add(salesRepCode);

                    return codes;
                }
                else
                {
                    var codes = _context.WKF_MAP_REP_ASM
                        .AsNoTracking()
                        .Where(x => x.UserName == userName)
                        .Select(x => x.SalesRepCode)
                        .Where(code => !string.IsNullOrEmpty(code))
                        .Distinct()
                        .ToList();

                    if (!string.IsNullOrEmpty(salesRepCode) && !codes.Contains(salesRepCode))
                        codes.Add(salesRepCode);

                    return codes;
                }
            }

            return new List<string>();
        }

    }
}

