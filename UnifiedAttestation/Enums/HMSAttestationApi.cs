using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace ShiftSoftware.UnifiedAttestation.Enums
{
    /// <summary>
    /// Selects which Huawei Mobile Services SafetyDetect API a Huawei attestation token is verified with.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum HMSAttestationApi
    {
        [Description("HMS SafetyDetect UserDetect")]
        UserDetect = 1,
        [Description("HMS SafetyDetect SysIntegrity")]
        SysIntegrity = 2,
    }
}
