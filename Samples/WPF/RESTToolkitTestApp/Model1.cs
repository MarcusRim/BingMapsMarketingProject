using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;

namespace RESTToolkitTestApp
{
    public partial class Model1 : DbContext
    {
        public Model1()
            : base("name=LocationContext")
        {
        }

        public virtual DbSet<Property> Properties { get; set; }

        public virtual DbSet<EF_TestTable> EF_TestTable { get; set; }

        public virtual DbSet<Companies> Companies { get; set; }

        public virtual DbSet<PropertyTenants> PropertyTenants { get; set; }

        public virtual DbSet<D1PropertyStatusCodes> D1PropertyStatusCodes { get; set; }
        
        public virtual DbSet<D1TargetTypes> D1TargetTypes { get; set; }

        public virtual DbSet<D1ClientStatusCodes> D1ClientStatusCodes { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
        }
    }
}
