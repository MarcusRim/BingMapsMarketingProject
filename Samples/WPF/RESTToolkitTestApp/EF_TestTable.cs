namespace RESTToolkitTestApp
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class EF_TestTable
    {
        public int ID { get; set; }

        [StringLength(50)]
        public string Nickname { get; set; }

        [StringLength(500)]
        public string Formatted_Address { get; set; }

        [StringLength(50)]
        public string Entity_Name { get; set; }

        [StringLength(500)]
        public string URL { get; set; }

        [StringLength(50)]
        public string Phone { get; set; }

        [StringLength(50)]
        public string Type { get; set; }

        public string Lattitude { get; set; }

        public string Longitude { get; set; }
    }
}
