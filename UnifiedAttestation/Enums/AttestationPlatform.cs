using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace ShiftSoftware.UnifiedAttestation.Enums
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AttestationPlatform
    {
        [Description("iOS OS")]
        iOS = 1,
        [Description("Normal Android")]
        Android = 2,
        [Description("HMS Android")]
        Huawei = 3,
    }
}