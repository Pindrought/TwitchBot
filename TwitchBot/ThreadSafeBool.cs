using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot
{
    internal class ThreadSafeBool
    {
        private readonly object _locker = new object();
        protected bool _bool = false;
        public bool Value
        {
            get
            {
                lock (_locker)
                    return _bool;
            }
            set
            {
                lock (_locker)
                    _bool = value;
            }
        }
    }
}
