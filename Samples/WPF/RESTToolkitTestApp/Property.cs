namespace RESTToolkitTestApp
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Property
    {
        public int ID { get; set; }

        [StringLength(500)]
        public string Nickname { get; set; }

        [Required]
        [StringLength(500)]
        public string Address_Full { get; set; }

        [StringLength(50)]
        public string Address1 { get; set; }

        [StringLength(50)]
        public string Address2 { get; set; }

        [StringLength(50)]
        public string City { get; set; }

        [StringLength(50)]
        public string State { get; set; }

        [StringLength(50)]
        public string Zip { get; set; }

        public int? SF { get; set; }

        public int? Units { get; set; }

        public int? RegionID { get; set; }

        public int? NAICS_ID { get; set; }

        [StringLength(50)]
        public string BuildYear { get; set; }

        public DateTime Created { get; set; }

        public DateTime? Updated { get; set; }

        public string Notes { get; set; }

        [StringLength(50)]
        public string RecordSource { get; set; }

        public int? RS_RowID { get; set; }

        public string Lattitude { get; set; }

        public string Longitude { get; set; }

        public string LocalPhoneNumber { get; set; }

        public string PropertyStatus { get; set; }
    }
}
