namespace RESTToolkitTestApp
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Companies
    {
        public int ID { get; set; }

        [Required]
        [StringLength(500)]
        public string CompanyName { get; set; }

        [Required]
        [StringLength(500)]
        public string CompanyName_Formal { get; set; }

        [StringLength(500)]
        public string Address_Full { get; set; }

        public int? IndustryTypeID { get; set; }

        public int? EmploymentCodeID { get; set; }

        public int? RevenueCodeID { get; set; }

        public string URL { get; set; }

        [StringLength(50)]
        public string PrimaryPhone { get; set; }

        public DateTime Created { get; set; }

        public DateTime? Updated { get; set; }

        public string Notes { get; set; }

        [StringLength(50)]
        public string RecordSource { get; set; }

        public int? RS_RowID { get; set; }
    }
}
