using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.tinyadditions.Utilities
{
    public class SigScanResults
    {
        /// <summary>
        /// The name of the function that was scanned for
        /// </summary>
        public string Function { get; set; }
        /// <summary>
        /// The pattern that was scanned for
        /// </summary>
        public string Pattern { get; set; }
        /// <summary>
        /// The hash of P4G.exe at the time of the scan
        /// </summary>
        public string ExeHash { get; set; }
        /// <summary>
        /// Whether the scan successfully found an address
        /// </summary>
        public bool WasSuccessful { get; set; }
        /// <summary>
        /// The address that was found
        /// </summary>
        public long Address { get; set; }

        /// <summary>
        /// Stores information about a sig scan's results
        /// </summary>
        /// <param name="Pattern">The pattern that was scanned for</param>
        /// <param name="Function">The name of the function that was scanned for</param>
        /// <param name="ExeHash">The hash of P4G.exe at the time of the scan</param>
        /// <param name="Address">The address that was found as a result of the scan</param>
        /// <param name="WasSuccessful">Whether the scan successfully found an address</param>
        public SigScanResults(string Function, string Pattern, string ExeHash, bool WasSuccessful, long Address)
        {
            this.Pattern = Pattern;
            this.Function = Function;
            this.ExeHash = ExeHash;
            this.WasSuccessful = WasSuccessful;
            this.Address = Address;
        }
    }
}
