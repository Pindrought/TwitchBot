using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot
{
    internal class ThreadSafeBool
    {
        //Nothing fancy here. Maybe should've just used volatile. Don't really care enough to change it.
        //I am only really using this to signal when a sound should be skipped from the main thread to the secondary thread that plays the sounds.
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
