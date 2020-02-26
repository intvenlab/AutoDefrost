using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDefrost
{
    class DataStorage
    {

        public virtual float dpm_airtemp { get; set; }
        public virtual float dpm_dewpoint { get; set; }
        public virtual float dpm_surfacetemp { get; set; }
        public virtual float dpm_rh { get; set; }
        public virtual string dpm_sn { get; set; }


    }
}
