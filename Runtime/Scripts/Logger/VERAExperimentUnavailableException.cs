using System;

namespace VERA
{
    /// <summary>
    /// Thrown when participant creation is blocked because the experiment is paused, inactive, or full.
    /// </summary>
    internal class VERAExperimentUnavailableException : Exception
    {
        public string ErrorCode { get; }
        public string ActivationStatus { get; }

        public VERAExperimentUnavailableException(string errorCode, string activationStatus, string message)
            : base(message)
        {
            ErrorCode = errorCode;
            ActivationStatus = activationStatus;
        }
    }
}
