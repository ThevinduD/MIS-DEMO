using System;
using System.Collections.Generic;
using System.Linq;

namespace MIS_DEMO.Models.ViewModels.Collection
{
    public class ReceiptGroupRow
    {
        public string ReceiptNo { get; set; }
        public string Team { get; set; }
        public decimal TotalAmount { get; set; }
        public string ChequeRefNo { get; set; }
        public string ChequeNo { get; set; }
        public DateTime? RealizedDate { get; set; }
        public DateTime? DepositedDate { get; set; }
        public DateTime? PayDate { get; set; }
    }

    public class CollectionDetailsViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string SelectedTeam { get; set; } = "All";
        public List<string> AvailableTeams { get; set; } = new();

        public List<ReceiptGroupRow> DirectPayments { get; set; } = new();
        public List<ReceiptGroupRow> ChequesCollected { get; set; } = new();
        public List<ReceiptGroupRow> ChequesDeposited { get; set; } = new();
        public List<ReceiptGroupRow> NonDepositedCheques { get; set; } = new();
        public List<ReceiptGroupRow> PostDatedCheques { get; set; }

        public decimal GrandTotal => DirectPayments.Sum(x => x.TotalAmount) +
                                     ChequesCollected.Sum(x => x.TotalAmount) +
                                     ChequesDeposited.Sum(x => x.TotalAmount) +
                                     NonDepositedCheques.Sum(x => x.TotalAmount);
    }

    public class ReceiptInvoiceRow
    {
        public string InvoiceNo { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public decimal InvoiceTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public string PayType { get; set; }
    }

    public class ReceiptDetailsViewModel
    {
        public string ReceiptNo { get; set; }
        public string Team { get; set; }
        public List<ReceiptInvoiceRow> Invoices { get; set; } = new();
        public decimal TotalPaid => Invoices.Sum(x => x.PaidAmount);
    }
}