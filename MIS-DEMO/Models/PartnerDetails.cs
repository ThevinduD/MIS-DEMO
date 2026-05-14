using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("PartnerDetails")]
    public class PartnerDetails
    {
        public int refno { get; set; }
        public bool ptype { get; set; }

        [Key] // Based on your SQL Primary Key constraint
        public string Pcode { get; set; }

        public string AltCode { get; set; }
        public string Barcode { get; set; }
        public string CustCat { get; set; }
        public string Pname { get; set; }
        public string Address { get; set; }
        public string Address2 { get; set; }
        public string Address3 { get; set; }
        public string Town { get; set; }
        public string District { get; set; }
        public string TelNo { get; set; }
        public string MobNo { get; set; }
        public string FaxNo { get; set; }
        public string Email { get; set; }
        public string Web { get; set; }
        public string Nationality { get; set; }
        public string Merital { get; set; }
        public DateTime DOB { get; set; }
        public DateTime DOW { get; set; }
        public string NICno { get; set; }
        public string BRNo { get; set; }
        public string IntroBy { get; set; }
        public DateTime IntroDate { get; set; }
        public string ContactPerson { get; set; }
        public string ContactPersonDesig { get; set; }
        public string ContactPersonMob { get; set; }

        // Heads up: This is a string in SQL, which causes a join issue!
        public string CreditPeriod { get; set; }

        public decimal CreditLimit { get; set; }
        public int NoMembers { get; set; }
        public string LoyaltyType { get; set; }
        public bool IsParentUser { get; set; }
        public decimal PointPrecntage { get; set; }
        public decimal AvaPoint { get; set; }
        public string KeyParentPcode { get; set; }
        public string ParentPcode { get; set; }
        public bool confirm { get; set; }
        public decimal availabledispoint { get; set; }
        public string CusCategoryCode { get; set; }
        public decimal MaxSaleslimit { get; set; }
        public bool IsCompanyCCUser { get; set; }
        public string Filename { get; set; }

        // Nullable fields
        public bool? IsUpload { get; set; }
        public string? City { get; set; }
        public bool? isNonTrading { get; set; }
        public int? Area { get; set; }
    }
}