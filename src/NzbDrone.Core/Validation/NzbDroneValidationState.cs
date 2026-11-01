namespace NzbDrone.Core.Validation
{
    public class NzbDroneValidationState
    {
        public static NzbDroneValidationState Warning = new NzbDroneValidationState { IsWarning = true };

        public bool IsWarning { get; set; }
        public string MessageId { get; set; }
        public object MessageParameters { get; set; }

        public static NzbDroneValidationState ForMessage(string messageId, object messageParameters = null)
        {
            return new NzbDroneValidationState
            {
                MessageId = messageId,
                MessageParameters = messageParameters
            };
        }
    }
}
