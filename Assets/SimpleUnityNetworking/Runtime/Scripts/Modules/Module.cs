using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        
        public abstract string Name { get; }
        
#if UNITY_EDITOR
        public abstract void ModuleGUI();
#endif
        
    }
}
