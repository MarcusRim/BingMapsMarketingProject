namespace RESTToolkitTestApp
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class PropertyTenants
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int PropertyID { get; set; }

        [Key]
        [Column(Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int CompanyID { get; set; }

        [StringLength(500)]
        public string SubAddress { get; set; }

        public int? PrimaryContactID { get; set; }

        public int? OccupancyTypeID { get; set; }

        public DateTime Created { get; set; }

        public DateTime? Updated { get; set; }

        public string Notes { get; set; }
        public string TargetType { get; set; }
        public string ClientStatus { get; set; }
    }
}
