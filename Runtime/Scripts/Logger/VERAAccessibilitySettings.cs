using System;

namespace VERA
{
    /// <summary>
    /// Accessibility settings stored for a participant on the VERA server.
    /// </summary>
    [Serializable]
    public class VERAAccessibilitySettings
    {
        public bool useSwitchControls;

        /// <summary>
        /// Number of physical switch inputs the participant uses (2 or 4).
        /// When 2, only next + select hardware inputs are active; previous and back
        /// are handled via on-screen UI. Defaults to 4 when unset / legacy payloads.
        /// </summary>
        public int switchInputCount;

        public VERASwitchBindingSet switchBindings;
        public string textSize;
        public bool reduceMotion;
        public bool darkMode;

        /// <summary>
        /// True when the participant prefers the 2-input (next + select) scheme.
        /// </summary>
        public bool UsesTwoInputControls => switchInputCount == 2;
    }

    /// <summary>
    /// Switch-control bindings for VLAT navigation actions.
    /// </summary>
    [Serializable]
    public class VERASwitchBindingSet
    {
        public VERASwitchBinding previous;
        public VERASwitchBinding next;
        public VERASwitchBinding select;
        public VERASwitchBinding back;
    }

    /// <summary>
    /// A single switch-control binding path and display label.
    /// </summary>
    [Serializable]
    public class VERASwitchBinding
    {
        public string label;
        public string bindingPath;
    }

    [Serializable]
    internal class VERAAccessibilitySettingsResponse
    {
        public bool success;
        public VERAAccessibilitySettings accessibilitySettings;
    }
}
