using System;

namespace ShiftSoftware.UnifiedAttestation.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ValidateAttestationAttribute : Attribute
    {
        public bool WithReplayProtection { get;}
        public ValidateAttestationAttribute(bool withReplayProtection = false)
        {
            WithReplayProtection = withReplayProtection;
        }
    }
}
