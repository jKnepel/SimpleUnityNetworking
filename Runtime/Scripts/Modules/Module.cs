using System;

namespace jKnepel.SimpleUnityNetworking.Modules
{
    public abstract class Module : IDisposable
    {
        ~Module()
        {
            Dispose(false);
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);
        
#if UNITY_EDITOR
        public abstract void ModuleGUI();
#endif
        
    }
}
