using p4gpc.tinyadditions.Configuration.Implementation;
using p4gpc.tinyadditions.Utilities;
using System.Collections.Generic;

namespace p4gpc.tinyadditions.Configuration
{
    public class InternalConfig : Configurable<InternalConfig>
    {
        public List<SigScanResults> SigScanResults { get; set; } = new List<SigScanResults>();

    }
}
