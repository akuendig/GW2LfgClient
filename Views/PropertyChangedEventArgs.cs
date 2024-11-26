
using System;

namespace Gw2Lfg
{
    public class LfgPropertyChangedEventArgs<S, T>(S oldState, S newState, Func<S, T> lens) : System.EventArgs
    {
        public S OldState { get => oldState; }
        public S NewState { get => newState; }
        public T OldValue { get => lens(oldState); }
        public T NewValue { get => lens(newState); }
    }
}