using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTS.DbDescriptionUpdater
{
    public class DbColumnMetaAttribute : Attribute
    {
        private string _description;

        public virtual string Description
        {
            get { return _description; }
            set { _description = value; }
        }
    }

    public class DbTableMetaAttribute : Attribute
    {
        private string _description;

        public virtual string Description
        {
            get { return _description; }
            set { _description = value; }
        }
    }

}
