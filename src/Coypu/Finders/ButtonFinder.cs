using System;
using Coypu.Drivers;

namespace Coypu.Finders
{
    internal class ButtonFinder : XPathQueryFinder
    {
        internal ButtonFinder(Driver driver, string locator, DriverScope scope, Options options) : base(driver, locator, scope, options) { }

        public override bool SupportsPartialTextMatching
        {
            get { return true; }
        }

        protected override Func<string, Options, string> GetQuery(XPath xpath)
        {
            return xpath.Button;
        }

        internal override string QueryDescription
        {
            get { return "button: " + Locator; }
        }

        
    }
}