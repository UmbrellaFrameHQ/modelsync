using System;
using System.Collections.Generic;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core.Exceptions
{
    public class PropertyNotFoundException : Exception
    {
        public PropertyNotFoundException(string message) : base(message) { }
    }
}
