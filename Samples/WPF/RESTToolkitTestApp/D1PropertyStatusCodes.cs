namespace RESTToolkitTestApp
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class D1PropertyStatusCodes
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int CodeID { get; set; }

        [Key]
        [Column(Order = 1)]
        [StringLength(50)]
        public string CodeName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Key]
        [Column(Order = 2)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int OrderBy { get; set; }
    }
}
