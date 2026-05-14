namespace MIS_DEMO.Models.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TodayTotalSales { get; set; }
        public decimal TodayTotalReturns { get; set; } 
        public decimal TodayNetSales => TodayTotalSales - TodayTotalReturns;
        public decimal NonDeliveredUnder45 { get; set; }
        public decimal NonDeliveredOver45 { get; set; }
        public decimal ThisMonthDeliveredTotal { get; set; }
        public List<TeamWiseSaleRowVm> TeamWiseSales { get; set; } = new();
        public decimal TeamWiseThisMonthTotal { get; set; }
        public decimal TeamWiseLastMonthTotal { get; set; }

        public decimal TotalStockValue { get; set; } = 0;
        public decimal TotalExpiringSoonValue { get; set; }
        public List<TeamStockRowVm> TeamStockValues { get; set; } = new();
        public List<TopRepSaleRow> TopReps { get; set; } = new();
    }

    public class TeamStockRowVm
    {
        public string TeamCode { get; set; }
        public string TeamName { get; set;  }
        public decimal StockValue { get; set; }
        public decimal ExpiringSoonValue { get; set; }
    }

    public class TopRepSaleRow
    {
        public string Team { get; set; }
        public string RepName { get; set; }
        public decimal NetSale { get; set; }
    }

    public class TopPerformersKpiViewModel
    {
        public List<TopRepSaleRow> TopReps { get; set; } = new();
        public int CurrentMonthOffset { get; set; }
        public string MonthLabel { get; set; }
        public bool IsTop { get; set; } = true; // NEW: Tracks the toggle state
    }

    // Put this at the bottom of DashboardViewModel.cs
    public class SalesMetric
    {
        public decimal Today { get; set; }
        public decimal ThisMonth { get; set; }
        public decimal LastMonth { get; set; }
    }

    public class SalesKpiAjaxModel
    {
        public DateTime SelectedDate { get; set; }
        public string SelectedTeam { get; set; } = "All";
        public List<string> AvailableTeams { get; set; } = new();

        public SalesMetric Sales { get; set; } = new();
        public SalesMetric Returns { get; set; } = new();

        public SalesMetric NetSales => new SalesMetric
        {
            Today = Sales.Today - Returns.Today,
            ThisMonth = Sales.ThisMonth - Returns.ThisMonth,
            LastMonth = Sales.LastMonth - Returns.LastMonth
        };

        public decimal TargetThisMonth { get; set; }
        public decimal TargetLastMonth { get; set; }

        public decimal AchievementThisMonth => NetSales.ThisMonth;
        public decimal AchievementLastMonth => NetSales.LastMonth;

        public decimal RunRateThisMonth => TargetThisMonth > 0 ? (AchievementThisMonth / TargetThisMonth) * 100 : 0;
        public decimal RunRateLastMonth => TargetLastMonth > 0 ? (AchievementLastMonth / TargetLastMonth) * 100 : 0;

        public string SelectedCategory { get; set; } = "All";
        public List<string> AvailableCategories { get; set; } = new();
    }


    
    public class TopStockItemRow
    {
        public string ItemName { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalQuantity { get; set; } // Changed from TotalValue
    }

    public class TopStockItemsKpiAjaxModel
    {
        public List<TopStockItemRow> TopItems { get; set; } = new();
    }


    public class LoginKpiAjaxModel
    {
        public int TodayLoginCount { get; set; }
    }

    public class LoginHistoryViewModel
    {
        public DateTime SelectedDate { get; set; }
        public List<LoginLog> Logs { get; set; } = new();
    }

    public class CollectionMetric
    {
        public decimal Today { get; set; }
        public decimal ThisMonth { get; set; }
        public decimal LastMonth { get; set; }
    }

    public class CollectionKpiAjaxModel
    {
        public DateTime SelectedDate { get; set; }
        public DateTime OutstandingTargetDate { get; set; }
        public string SelectedTeam { get; set; } = "All";
        public string OutstandingMode { get; set; } = "All";
        public string FreezeMode { get; set; } = "NonFreeze"; // <--- ADD THIS LINE
        public string SelectedCategory { get; set; } = "All";
        public List<string> AvailableCategories { get; set; } = new();
        public List<string> AvailableTeams { get; set; } = new();
        public CollectionMetric DirectCash { get; set; } = new();
        public CollectionMetric ChequeCollected { get; set; } = new();
        public CollectionMetric ChequeDeposited { get; set; } = new();
        public decimal NonDepositChequeSum { get; set; }
        public decimal TotalCollectedToday => DirectCash.Today + ChequeCollected.Today;

        public CollectionMetric ReturnCheques { get; set; } = new();
        public CollectionMetric CreditNotes { get; set; } = new();


        public CollectionMetric RealizedCollection => new CollectionMetric
        {
            Today = DirectCash.Today + ChequeDeposited.Today,
            ThisMonth = DirectCash.ThisMonth + ChequeDeposited.ThisMonth,
            LastMonth = DirectCash.LastMonth + ChequeDeposited.LastMonth
        };

        public CollectionMetric GrossCollection => new CollectionMetric
        {
            Today = DirectCash.Today + ChequeCollected.Today + ChequeDeposited.Today,
            ThisMonth = DirectCash.ThisMonth + ChequeCollected.ThisMonth + ChequeDeposited.ThisMonth,
            LastMonth = DirectCash.LastMonth + ChequeCollected.LastMonth + ChequeDeposited.LastMonth
        };

        public decimal NetOutstanding { get; set; }
        public decimal SystemMonthRealized { get; set; }
        public decimal ToBeNetCollect => NetOutstanding - SystemMonthRealized;
        public decimal RunRate => NetOutstanding > 0 ? (SystemMonthRealized / NetOutstanding) * 100 : 0;


        public decimal UnrealizedTotal { get; set; }
        public decimal ToBeGrossOnDate => ToBeNetCollect - UnrealizedTotal;
        public decimal GrossRate => NetOutstanding > 0 ? ((SystemMonthRealized + UnrealizedTotal) / NetOutstanding) * 100 : 0;



    }

}
