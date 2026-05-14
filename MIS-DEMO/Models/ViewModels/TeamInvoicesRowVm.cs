namespace MIS_DEMO.Models.ViewModels
{
    public class TeamInvoicesRowVm
    {
        public string InvoDocNo { get; set; } = "";
        public DateTime? RefDate { get; set; }
        public decimal Amount { get; set; }

        public string CusCode { get; set; } = "";
        public string CusName { get; set; } = "";

        public string SalesRepCode { get; set; } = "";
        public string SalesRepName { get; set; } = "";
    }

    public class TeamInvoicesViewModel
    {
        public string Team { get; set; } = "";          // LocShort
        public string Bucket { get; set; } = "";        // under45 / over45
        public DateTime CutoffDate { get; set; }

        public string? Invoice { get; set; }
        public string? Rep { get; set; }
        public string? Customer { get; set; }

        public int TotalRows { get; set; }
        public int MaxRows { get; set; }
        public bool IsTruncated { get; set; }

        public decimal TotalAmount { get; set; }

        public List<TeamInvoicesRowVm> Rows { get; set; } = new();
    }
}